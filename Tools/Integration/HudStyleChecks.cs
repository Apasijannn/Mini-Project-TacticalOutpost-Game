using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Outpost.UI;
using Outpost.Player;
using Outpost.Enemy;
using Outpost.Audio;

public static class HudStyleChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static async Task Until(Func<bool> ready,string message)
    {
        float end=Time.realtimeSinceStartup+25;
        while(!ready()){Check(Application.isPlaying&&Time.realtimeSinceStartup<end,message);await Task.Delay(15);}
    }
    static async Task LoadGame()
    {
        Time.timeScale=1;var load=SceneManager.LoadSceneAsync(GameMenu.GameScene);
        await Until(()=>load.isDone&&PlayerHealth.Instance!=null&&WaveDirector.Instance!=null&&MinimapController.Instance!=null,"Game did not initialize.");
        await Task.Delay(200);WaveDirector.Instance.StopAllCoroutines();
    }
    static TMP_Text Text(Transform parent,string name)=>parent.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name==name);
    static void Layout()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.renderMode==RenderMode.ScreenSpaceOverlay&&c.gameObject.activeInHierarchy))
        foreach(var label in canvas.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled))
        {
            label.ForceMeshUpdate();Check(!label.isTextOverflowing,"Clipped HUD label: "+label.name+" / "+label.text);
            Vector3[] corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);
            Check(corners.All(c=>c.x>=-1&&c.x<=Screen.width+1&&c.y>=-1&&c.y<=Screen.height+1),"Label outside screen: "+label.name);
        }
        var border=MinimapController.Instance.map.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="BorderLine");
        Check(border.enabled&&border.color==HudStyle.Amber&&border.sprite.texture.GetPixel(128,128).a==0&&border.sprite.texture.GetPixel(253,128).a>.5f,"Circular map border is missing or opaque in the middle.");
        Check(MinimapController.Instance.map.Find("BLACKOUT map frame")==null,"Minimap still has a rectangular frame.");
        var fund=MinimapController.Instance.ms.Fund_Ima.rectTransform;
        Check(Vector3.Distance(border.rectTransform.position,fund.TransformPoint(fund.rect.center))<1f&&Mathf.Abs(border.rectTransform.rect.width-fund.rect.width*.9f-4)<.1f,"Ring does not follow the visible map circle.");
        var frame=border.rectTransform;
        var points=new Vector3[4];frame.GetWorldCorners(points);
        Check(points.All(p=>p.x>=0&&p.x<=Screen.width&&p.y>=0&&p.y<=Screen.height),"Minimap border is outside screen.");
    }
    static async Task Capture(string name){Layout();ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/"+name+".png");await Task.Delay(120);}
    static async Task Press(Keyboard kb,Key key)
    {
        InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(60);
        InputSystem.QueueStateEvent(kb,new KeyboardState(key));await Task.Delay(100);
        InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(50);
    }
    public static async Task<object> Main()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        var original=Keyboard.current;var kb=InputSystem.AddDevice<Keyboard>("BlackoutHudCheck");InputSystem.EnableDevice(kb);
        try
        {
            await LoadGame();
            var hp=PlayerHealth.Instance;var hud=hp.GetComponent<FpsHud>();var director=WaveDirector.Instance;
            var manager=hp.GetComponentInChildren<WeaponManager>(true);
            Check(Text(hud.transform,"Ammo").font==GameMenu.Instance.monoFont,"Ammo typography does not match menu.");
            Check(Text(director.transform,"Banner").font==GameMenu.Instance.headingFont,"Banner typography does not match menu.");
            var edge=hud.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="Weapon panel orange edge");
            Check(edge.rectTransform.anchorMin.x==1&&edge.color==HudStyle.Amber,"Weapon orange line is not on the right.");
            manager.Select(0);await Task.Delay(150);Check(Text(hud.transform,"Ammo").text.Contains("30"),"Rifle ammo missing.");
            manager.Select(1);await Task.Delay(100);Check(Text(hud.transform,"WeaponName").text=="M1911","Weapon switch label did not update.");
            await Capture("HudPistol");manager.Select(0);await Task.Delay(100);
            var banner=Text(director.transform,"Banner");var type=banner.GetComponent<MilitaryText>();
            type.Speak("INCOMING TRANSMISSION");Check(banner.maxVisibleCharacters==0,"Military text did not start hidden.");
            await Task.Delay(90);Check(type.IsTyping&&banner.maxVisibleCharacters>0&&banner.maxVisibleCharacters<20,"Text did not reveal progressively.");
            int visible=banner.maxVisibleCharacters;type.Speak("INCOMING TRANSMISSION");Check(banner.maxVisibleCharacters==visible,"Repeated message restarted typing.");
            GameMenu.Instance.TogglePause();visible=banner.maxVisibleCharacters;await Task.Delay(150);Check(banner.maxVisibleCharacters==visible,"Typing advanced during pause.");
            GameMenu.Instance.Resume();await Task.Delay(350);Check(!type.IsTyping,"Typing did not finish after resume.");
            var radio=(AudioSource)typeof(OutpostAudio).GetField("radio",Hidden).GetValue(OutpostAudio.Instance);
            var clip=(AudioClip)typeof(OutpostAudio).GetField("radioTick",Hidden).GetValue(OutpostAudio.Instance);
            var samples=new float[clip.samples];clip.GetData(samples,0);Check(samples.Any(s=>Mathf.Abs(s)>.05f),"Electronic input sound is silent.");
            await Task.Delay(70);OutpostAudio.RadioTick();Check(radio.isPlaying&&!radio.ignoreListenerVolume&&!radio.ignoreListenerPause,"Radio beep is not playing through normal audio controls.");
            var weapon=manager.Current;typeof(FpsWeapon).GetField("ammo",Hidden).SetValue(weapon,0);await Task.Delay(100);
            var prompt=Text(hud.transform,"Msg");Check(prompt.text=="Press R to reload","Reload prompt changed.");
            await Task.Delay(400);await Capture("HudReload");weapon.StartReload();await Task.Delay(100);
            Check(weapon.IsReloading&&!Text(hud.transform,"Ammo").text.ToUpperInvariant().Contains("RELOAD")&&prompt.text=="","Reload animation should show without a reloading label.");
            await Capture("HudReloadAnimation");manager.RefillAll();await Task.Delay(80);
            hp.TakeDamage(25,hp.transform.position,Vector3.forward);await Task.Delay(80);
            Check(Text(hud.transform,"HP").text.StartsWith("75 "),"Health did not update immediately.");
            HudEvents.ShowToast("+25 HEALTH",HudStyle.Amber);await Task.Delay(180);Check(Text(hud.transform,"Toast").maxVisibleCharacters>0,"Toast typing missing.");
            GeneratorHealth.All[0].Damage(50);await Task.Delay(100);
            var bar=GeneratorHealth.All[0].GetComponent<GeneratorHealthBar>();
            var worldCanvas=(Canvas)typeof(GeneratorHealthBar).GetField("canvas",Hidden).GetValue(bar);
            Check(Text(worldCanvas.transform,"HP").text=="950 / 1000","Generator health display did not update.");
            var bossVariant=director.catalog.variants.First(v=>v.id==director.bossVariantId);
            var boss=EnemyDebugSpawner.SpawnAt(bossVariant.prefab,hp.transform.position+hp.transform.forward*5);
            Check(boss!=null,"Could not spawn boss for health bar preview.");
            boss.GetComponent<EnemyBrain>().enabled=false;
            var agent=boss.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent.isOnNavMesh)agent.isStopped=true;
            await Task.Delay(100);
            var bossName=boss.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Name");Check(bossName.color.r>bossName.color.g*2,"Boss health bar lost warning color.");
            typeof(WaveDirector).GetMethod("Banner",Hidden).Invoke(director,new object[]{"BOSSES INBOUND","Heavy contact approaching. Protect the generator!",3f,true});
            await Task.Delay(100);await Capture("HudBossTyping");await Task.Delay(700);await Capture("HudBossWarning");
            Check(Text(director.transform,"Banner").maxVisibleCharacters>=14,"Boss alert did not finish typing.");
            await Press(kb,Key.M);Check(MinimapController.Instance.IsLarge,"M did not open tactical map.");await Task.Delay(350);await Capture("HudLargeMap");
            await Press(kb,Key.Escape);Check(!MinimapController.Instance.IsLarge&&!GameMenu.Instance.IsPaused,"Escape map priority broke.");
            await LoadGame();await Task.Delay(650);await Capture("HudStyle");
            var result=new{passed=true,sharedMenuFonts=true,rectangularWeaponPanels=true,rightOrangeAccent=true,themedMinimapBorder=true,smallAndLargeMapInViewport=true,progressiveTyping=true,repeatedMessageDoesNotRestart=true,typingPausesWithMenu=true,electronicSound=true,masterVolumeAndPauseRespected=true,instantHealthAndAmmo=true,bossWarningRed=true,generatorHealthUpdated=true,weaponSwitchUpdated=true,noClippedHudText=true,viewport=new[]{Screen.width,Screen.height}};
            Directory.CreateDirectory("Docs/Integration");File.WriteAllText("Docs/Integration/HudStyleChecks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { checks=result, noReloadingText=true },Newtonsoft.Json.Formatting.Indented));
            var load=SceneManager.LoadSceneAsync(GameMenu.MenuScene);await Until(()=>load.isDone&&GameMenu.Instance!=null&&GameMenu.Instance.mainMenu,"Could not return to menu.");return result;
        }
        finally {InputSystem.RemoveDevice(kb);if(original!=null)original.MakeCurrent();InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
    }
}
