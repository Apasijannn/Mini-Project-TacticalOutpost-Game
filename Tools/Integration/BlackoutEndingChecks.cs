using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Outpost;
using Outpost.UI;
using Outpost.Player;
using Outpost.Enemy;
public static class BlackoutEndingChecks
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static async Task Until(Func<bool> ready,string message)
    {
        float end=Time.realtimeSinceStartup+25;
        while(!ready()){Check(Application.isPlaying&&Time.realtimeSinceStartup<end,message);await Task.Delay(20);}
    }
    static async Task LoadGame()
    {
        Time.timeScale=1;
        var load=SceneManager.LoadSceneAsync(GameMenu.GameScene);
        await Until(()=>load.isDone&&PlayerHealth.Instance!=null&&WaveDirector.Instance!=null&&GeneratorHealth.All.Count==1,"Gameplay did not load.");
        await Task.Delay(200);WaveDirector.Instance.StopAllCoroutines();
        foreach(var health in EnemyHealth.Alive.ToArray())UnityEngine.Object.Destroy(health.gameObject);
    }
    static async Task Capture(string name)
    {
        Directory.CreateDirectory("Assets/TacticalOutpost/Documentation/Debug");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/"+name+".png");await Task.Delay(150);
    }
    static async Task Restart(Keyboard keyboard)
    {
        int old=PlayerHealth.Instance.GetInstanceID();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
        await Until(()=>PlayerHealth.Instance!=null&&PlayerHealth.Instance.GetInstanceID()!=old&&GeneratorHealth.All.Count==1,"Enter did not restart.");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(250);
        Check(PlayerHealth.Instance.Health==100&&GeneratorHealth.All[0].Health==1000,"Health did not reset.");
    }
    public static async Task<object> Main()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        var bg=InputSystem.settings.backgroundBehavior;var ei=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        var original=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("BlackoutEndingCheck");InputSystem.EnableDevice(keyboard);
        try
        {
            await LoadGame();
            var director=WaveDirector.Instance;var destruction=director.GetComponent<GeneratorDestruction>();
            Check(destruction!=null&&destruction.explosion!=null&&destruction.fire!=null&&destruction.debris!=null,"Destruction assets missing.");
            Check(destruction.generatorBlastSound!=null&&destruction.buildingBlastSound!=null&&destruction.tankBlastSound!=null,"Explosion audio is not assigned.");
            foreach(var clip in new[]{destruction.generatorBlastSound,destruction.buildingBlastSound,destruction.tankBlastSound})
            {
                var samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0)&&clip.channels==1&&samples.Any(s=>Mathf.Abs(s)>.1f)&&samples.All(s=>Mathf.Abs(s)<=.701f),"Explosion audio is silent, uncalibrated, or clipping: "+clip.name);
            }
            GeneratorHealth.All[0].Damage(9999);
            await Until(()=>destruction.Playing&&destruction.View!=null,"Drone cinematic did not start.");
            var position=destruction.View.transform.position;
            await Until(()=>destruction.DebrisCount>50,"Primary explosion did not trigger.");
            float primaryAt=Time.unscaledTime;
            var primarySound=destruction.GetComponentsInChildren<AudioSource>().Single(s=>s.clip==destruction.generatorBlastSound);
            Check(primarySound.isPlaying&&primarySound.volume==.55f&&primarySound.spatialBlend==.65f&&!primarySound.ignoreListenerVolume&&!primarySound.ignoreListenerPause&&Vector3.Distance(primarySound.transform.position,destruction.View.transform.position)<primarySound.maxDistance,"Generator audio is not playing through the cinematic listener and master controls.");
            await Until(()=>destruction.BuildingDebrisCount>30,"Connected building did not explode.");
            float buildingDelay=Time.unscaledTime-primaryAt;
            Check(buildingDelay<=1f,"Connected building explosion is delayed too long.");
            Check(destruction.GetComponentsInChildren<AudioSource>().Single(s=>s.clip==destruction.buildingBlastSound).isPlaying,"Building explosion audio is not synchronized.");
            await Capture("GeneratorExplosion");
            Check(Vector3.Distance(position,destruction.View.transform.position)>2,"Drone camera is not orbiting.");
            Check(!PlayerHealth.Instance.GetComponent<FpsController>().InputEnabled&&Time.timeScale>0,"Cinematic controls/clock incorrect.");
            Check(destruction.DebrisCount>50,"Generator did not break into enough small fragments.");
            Check(destruction.pipePaths.Length>0&&destruction.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name=="Ruptured pipe"),"Attached pipes remained intact.");
            await Until(()=>destruction.TankBurstTime>0,"Tank burst did not trigger.");
            Check(destruction.GetComponentsInChildren<AudioSource>().Count(s=>s.clip==destruction.tankBlastSound)==1,"Tank audio is duplicated across simultaneous groups.");
            await Task.Delay(2800);await Capture("GeneratorChainReaction");
            Check(destruction.TanksDestroyed==destruction.tanks.Length&&destruction.WallsDestroyed==destruction.containmentWalls.Length,"Some tanks or containment panels stayed intact.");
            Check(destruction.TankBurstTime>0&&destruction.blastSites.Length==4,"Tank burst groups are incorrect.");
            await Task.Delay(3500);await Capture("PowerStationExplosion");
            await Until(()=>destruction.Finished,"Cinematic did not finish.");
            Check(destruction.BlastsTriggered==destruction.blastSites.Length+2,"Missing secondary blasts.");
            Check(destruction.BuildingDebrisCount>30,"Connected power station did not collapse.");
            Check(WaveDirector.Instance.CurrentPhase==WaveDirector.Phase.Defeat&&Time.timeScale==0,"Defeat screen not shown after cinematic.");
            int blasts=destruction.BlastsTriggered,pieces=destruction.DebrisCount,buildingPieces=destruction.BuildingDebrisCount;
            await Capture("GeneratorBlackout");await Restart(keyboard);
            Check(UnityEngine.Object.FindObjectsByType<GeneratorDestruction>(FindObjectsSortMode.None).All(d=>!d.Playing&&!d.Finished),"Destruction leaked across restart.");
            director=WaveDirector.Instance;director.StopAllCoroutines();
            var player=PlayerHealth.Instance;var controller=player.GetComponent<FpsController>();
            var forward=controller.transform.forward;
            var variant=director.catalog.variants.First(v=>v.id=="walker");
            for(int i=0;i<3;i++)
            {
                var p=player.transform.position+forward*(1.6f+i*.5f)+controller.transform.right*(i-1)*.7f;
                EnemyDebugSpawner.SpawnAt(variant.prefab,p);
            }
            await Task.Delay(300);
            var camera=controller.playerCamera;float cameraY=camera.transform.position.y;
            player.TakeDamage(9999,player.transform.position,Vector3.forward);
            await Task.Delay(400);Check(Time.timeScale<.5f,"Collapse slow motion missing.");await Task.Delay(1900);
            var death=PlayerDeathView.Instance;
            Check(death!=null&&death.Settled&&!controller.enabled,"First person collapse did not complete.");
            Check(camera.transform.position.y<cameraY-.5f,"Camera did not fall.");
            Check(camera.transform.eulerAngles.z>50&&camera.transform.eulerAngles.z<100,"Death camera did not roll.");
            var volume=player.GetComponentInChildren<Volume>();
            Check(volume.sharedProfile.TryGet<ColorAdjustments>(out var color)&&color.saturation.value==-100,"Death view is not grayscale.");
            Check(volume.sharedProfile.TryGet<DepthOfField>(out var focus)&&focus.gaussianMaxRadius.value>1,"Death blur missing.");
            Check(Time.timeScale>0&&EnemyHealth.Alive.Any(h=>h.GetComponent<EnemyBrain>().CurrentState==EnemyBrain.State.AttackPlayer),"Zombies stopped attacking the fallen player.");
            await Capture("PlayerDeath");await Restart(keyboard);
            Check(PlayerDeathView.Instance!=null&&!PlayerDeathView.Instance.IsActive&&PlayerHealth.Instance.GetComponent<FpsController>().enabled,"Death state leaked into restart.");
            var result=new {passed=true,blasts,pieces,buildingPieces,buildingDelaySeconds=buildingDelay,explosionAudioPlaying=true,explosionAudioPeakLimited=true,singleTankGroupSound=true,masterAudioControlsRespected=true,allTanksDestroyed=destruction.tanks.Length,containmentPanelsDestroyed=destruction.containmentWalls.Length,simultaneousTankGroups=4,connectedPipesCollapse=true,noIndependentWallExplosions=true,droneOrbit=true,chainReaction=true,generatorRestart=true,firstPersonCollapse=true,slowMotionCollapse=true,grayscale=true,blur=true,corpseAttack=true,playerRestart=true};
            Directory.CreateDirectory("Docs/Integration");File.WriteAllText("Docs/Integration/BlackoutEndingChecks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            var menuLoad=SceneManager.LoadSceneAsync(GameMenu.MenuScene);
            await Until(()=>GameMenu.Instance!=null&&GameMenu.Instance.mainMenu&&GameMenu.Instance.GetComponent<MenuBackdrop>()!=null&&GameMenu.Instance.GetComponent<MenuBackdrop>().Ready,"Menu did not reload.");
            return result;
        }
        finally {InputSystem.RemoveDevice(keyboard);if(original!=null)original.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=ei;}
    }
}
