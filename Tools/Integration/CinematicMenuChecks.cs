using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using Outpost.UI;
using Outpost.Player;
using Outpost.Enemy;

public static class CinematicMenuChecks
{
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static async Task Until(Func<bool> ready,string message)
    {
        float end=Time.realtimeSinceStartup+25;
        while(!ready()){Check(Application.isPlaying&&Time.realtimeSinceStartup<end,message);await Task.Delay(20);}
    }
    static UnityEngine.UI.Button Button(string name)=>GameMenu.Instance.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name==name);
    static UnityEngine.UI.Slider Slider(string name)=>GameMenu.Instance.GetComponentsInChildren<UnityEngine.UI.Slider>().Single(s=>s.name==name+" slider");
    static void Layout()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in GameMenu.Instance.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            Check(!text.isTextOverflowing,"Text clipped: "+text.name+" / "+text.text);
            Vector3[] corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
            Check(corners.All(c=>c.x>=-1&&c.x<=Screen.width+1&&c.y>=-1&&c.y<=Screen.height+1),"Text outside viewport: "+text.name);
        }
        foreach(var button in GameMenu.Instance.GetComponentsInChildren<UnityEngine.UI.Button>())
        {
            var results=new List<RaycastResult>();var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            EventSystem.current.RaycastAll(pointer,results);
            Check(results.Any(r=>r.gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button),"Button cannot receive pointer: "+button.name);
        }
    }
    static async Task Capture(string name)
    {
        await Task.Delay(180);Layout();
        Directory.CreateDirectory("Assets/TacticalOutpost/Documentation/Debug");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/"+name+".png");await Task.Delay(160);
    }
    static async Task Press(Keyboard keyboard,Key key)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(70);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(130);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(70);
    }
    public static async Task<object> Main()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        string prefix="Outpost.Settings.";
        string[] floats={"Volume","Sensitivity","Fov"},ints={"HeadBob","Quality","Fullscreen"};
        var originalFloats=floats.ToDictionary(k=>k,k=>PlayerPrefs.HasKey(prefix+k)?(float?)PlayerPrefs.GetFloat(prefix+k):null);
        var originalInts=ints.ToDictionary(k=>k,k=>PlayerPrefs.HasKey(prefix+k)?(int?)PlayerPrefs.GetInt(prefix+k):null);
        var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("OutpostCinematicCheck");
        var background=InputSystem.settings.backgroundBehavior;
        var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        InputSystem.EnableDevice(keyboard);
        try
        {
            await Until(()=>GameMenu.Instance!=null&&GameMenu.Instance.mainMenu&&GameMenu.Instance.GetComponent<MenuBackdrop>()!=null&&GameMenu.Instance.GetComponent<MenuBackdrop>().Ready,"Cinematic menu did not initialize.");
            await Task.Delay(1000);
            Check(PlayerHealth.Instance==null&&EnemyHealth.Alive.Count==0,"Gameplay is running behind main menu.");
            var backdrop=GameMenu.Instance.GetComponent<MenuBackdrop>();
            Check(backdrop.PreviewCount==30,"Menu zombie preview population is incomplete.");
            Check(backdrop.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>().Any(a=>a.isOnNavMesh&&a.hasPath&&a.velocity.magnitude>.1f),"Preview zombies are not walking toward the station.");
            Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.enabled)==1,"Duplicate/missing audio listener.");
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Duplicate event systems.");
            var view=GameMenu.Instance.GetComponent<MenuBackdrop>().View;var position=view.transform.position;
            await Task.Delay(600);Check(Vector3.Distance(position,view.transform.position)>.01f,"Idle camera is frozen.");
            await Capture("MainMenu");
            Button("SETTINGS").onClick.Invoke();Check(GameMenu.Instance.CurrentPage==GameMenu.Page.Settings,"Settings did not open.");
            Slider("MASTER VOLUME").value=.22f;await Press(keyboard,Key.Escape);
            Check(GameMenu.Instance.CurrentPage==GameMenu.Page.Home&&Mathf.Abs(AudioListener.volume-GameOptions.Volume)<.001f,"Back did not discard audio preview.");
            Button("SETTINGS").onClick.Invoke();Slider("MASTER VOLUME").value=.63f;Slider("MOUSE SENSITIVITY").value=.16f;Slider("FIELD OF VIEW").value=88;
            GameMenu.Instance.GetComponentsInChildren<UnityEngine.UI.Toggle>().Single(t=>t.name=="HEAD BOB").isOn=false;
            Button("APPLY SETTINGS").onClick.Invoke();
            Check(Mathf.Abs(GameOptions.Volume-.63f)<.001f&&Mathf.Abs(GameOptions.Sensitivity-.16f)<.001f&&GameOptions.FieldOfView==88&&!GameOptions.HeadBob,"Settings were not saved.");
            await Capture("Settings");
            Button("CONTROLS").onClick.Invoke();await Capture("Controls");
            Check(GameMenu.Instance.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="WASD / ARROWS"),"Keyboard guide is missing.");
            Button("FIELD MANUAL").onClick.Invoke();await Capture("FieldManual");
            Button("HOSTILES").onClick.Invoke(); await Task.Delay(180);
            var inspector=GameMenu.Instance.GetComponentInChildren<FieldManualViewer>();
            Check(inspector!=null&&inspector.Model!=null,"3D hostile preview is missing.");
            Check(GameMenu.Instance.manualEntries.Length==9,"Expected seven hostiles and two weapons.");
            var rotation=inspector.Model.localRotation;
            var drag=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,delta=new Vector2(100,0)};
            inspector.OnBeginDrag(drag);inspector.OnDrag(drag);await Task.Delay(100);
            Check(Quaternion.Angle(rotation,inspector.Model.localRotation)>10,"Mouse drag did not rotate the model.");
            inspector.OnEndDrag(drag);rotation=inspector.Model.localRotation;await Task.Delay(3500);
            Check(Quaternion.Angle(rotation,inspector.Model.localRotation)>2,"Idle auto rotation did not resume.");
            await Capture("HostilePreview");
            for(int i=0;i<4;i++)Button("NEXT ENTRY").onClick.Invoke();
            await Capture("BossPreview");
            for(int i=0;i<7;i++)
            {
                Button("NEXT ENTRY").onClick.Invoke();await Task.Delay(80);Layout();
                Check(inspector.Model.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"A hostile variant has no visible model.");
            }
            Check(EnemyHealth.Alive.Count==0,"Inspector spawned a combat entity.");
            Button("ARSENAL").onClick.Invoke();await Capture("RiflePreview");
            Button("NEXT ENTRY").onClick.Invoke();await Capture("PistolPreview");
            await Press(keyboard,Key.Escape);Check(GameMenu.Instance.CurrentPage==GameMenu.Page.Home,"Escape did not return home.");
            Button("START").onClick.Invoke();
            await Until(()=>PlayerHealth.Instance!=null&&GameMenu.Instance!=null&&!GameMenu.Instance.mainMenu&&WaveDirector.Instance!=null,"Start did not initialize gameplay.");
            await Task.Delay(350);
            var player=PlayerHealth.Instance.GetComponent<FpsController>();
            Check(Mathf.Abs(player.mouseSensitivity-.16f)<.001f&&player.baseFov==88&&!player.headBob,"Saved settings did not reach the player.");
            Check(UnityEngine.Object.FindObjectsByType<MenuBackdrop>(FindObjectsSortMode.None).Length==0,"Menu camera remained in gameplay.");
            await Press(keyboard,Key.M);Check(MinimapController.Instance.IsLarge,"M did not open map.");
            await Press(keyboard,Key.Escape);Check(!MinimapController.Instance.IsLarge&&!GameMenu.Instance.IsPaused,"Escape should close minimap first.");
            await Press(keyboard,Key.Escape);Check(GameMenu.Instance.IsPaused&&Time.timeScale==0&&!player.InputEnabled&&AudioListener.pause,"Pause did not stop gameplay.");
            float pausedAt=Time.time;Vector3 pausedPosition=player.transform.position;
            await Press(keyboard,Key.W);Check(Time.time==pausedAt&&Vector3.Distance(pausedPosition,player.transform.position)<.001f,"Player moved while paused.");
            await Capture("PauseMenu");
            Button("SETTINGS").onClick.Invoke();await Press(keyboard,Key.Escape);
            Check(GameMenu.Instance.IsPaused&&GameMenu.Instance.CurrentPage==GameMenu.Page.Home,"Escape resumed from a subpage.");
            Button("FIELD MANUAL").onClick.Invoke();Button("HOSTILES").onClick.Invoke();await Task.Delay(100);
            inspector=GameMenu.Instance.GetComponentInChildren<FieldManualViewer>();inspector.IdleDelay=0;rotation=inspector.Model.localRotation;
            await Task.Delay(350);Check(Quaternion.Angle(rotation,inspector.Model.localRotation)>2&&Time.timeScale==0,"Inspection froze while paused.");
            await Press(keyboard,Key.Escape);
            Button("RESUME").onClick.Invoke();await Task.Delay(200);
            Check(!GameMenu.Instance.IsPaused&&Time.timeScale==1&&player.InputEnabled&&!AudioListener.pause&&Cursor.lockState==CursorLockMode.Locked,"Resume did not restore gameplay.");
            await Press(keyboard,Key.Escape);Button("EXIT").onClick.Invoke();
            await Until(()=>GameMenu.Instance!=null&&GameMenu.Instance.mainMenu&&GameMenu.Instance.GetComponent<MenuBackdrop>()!=null&&GameMenu.Instance.GetComponent<MenuBackdrop>().Ready,"Main menu did not reload.");
            Check(PlayerHealth.Instance==null&&EnemyHealth.Alive.Count==0&&Time.timeScale==1&&!AudioListener.pause,"Gameplay/pause state leaked into menu.");
            var result=new{passed=true,cinematicCameraMoving=true,decorativeZombies=30,previewIsolatedFromCombat=true,manualEntries=9,dragRotatesModel=true,idleAutoRotation=true,menuPages=4,keyboardDiagram=true,settingsSavedAndApplied=true,previewDiscarded=true,mapEscapePriority=true,pauseStopsMovement=true,resumeRestoresInput=true,exitReturnsToMenu=true,viewport=new[]{Screen.width,Screen.height},noClippedText=true,pointerRaycastsPass=true};
            Directory.CreateDirectory("Docs/Integration");File.WriteAllText("Docs/Integration/CinematicMenuChecks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            foreach(var pair in originalFloats)if(pair.Value.HasValue)PlayerPrefs.SetFloat(prefix+pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(prefix+pair.Key);
            foreach(var pair in originalInts)if(pair.Value.HasValue)PlayerPrefs.SetInt(prefix+pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(prefix+pair.Key);
            PlayerPrefs.Save();GameOptions.ApplyGlobal();
            if(PlayerHealth.Instance!=null)GameOptions.ApplyToPlayer(PlayerHealth.Instance.GetComponent<FpsController>());
            InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
        }
    }
}
