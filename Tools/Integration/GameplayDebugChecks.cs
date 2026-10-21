using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Player;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.UI;
using MiniMapModular;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;

public static class GameplayDebugChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Save(string name,object data){File.WriteAllText("Docs/Integration/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));}
    static async Task Ready()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");float deadline=Time.realtimeSinceStartup+20;
        while(PlayerHealth.Instance==null||WaveDirector.Instance==null||MinimapController.Instance==null)
        {
            Check(Application.isPlaying&&Time.realtimeSinceStartup<deadline,"Additive gameplay initialization did not complete.");
            await Task.Delay(16);
        }
    }
    static async Task Advance(float seconds,Action sample=null)
    {
        float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+45;
        while(Time.time<end)
        {
            Check(Application.isPlaying&&Time.timeScale>0,"Play session stopped or paused during check.");
            Check(Time.realtimeSinceStartup<deadline,"Player clock did not advance in time.");
            sample?.Invoke();await Task.Delay(16);
        }
    }

    public static async Task<object> MenuControls()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        var load=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(GameMenu.MenuScene);
        float deadline=Time.realtimeSinceStartup+25;
        while(!load.isDone||GameMenu.Instance==null||!GameMenu.Instance.mainMenu)
        {Check(Time.realtimeSinceStartup<deadline,"Menu did not load.");await Task.Delay(16);}
        while(GameMenu.Instance.GetComponent<MenuBackdrop>()==null||!GameMenu.Instance.GetComponent<MenuBackdrop>().Ready)
        {Check(Time.realtimeSinceStartup<deadline,"Menu backdrop did not load.");await Task.Delay(16);}
        await Advance(.9f);
        Func<UnityEngine.UI.Button[]> buttons=()=>GameMenu.Instance.GetComponentsInChildren<UnityEngine.UI.Button>();
        Check(buttons().Select(b=>b.name).SequenceEqual(new[]{"START","SETTINGS","CONTROLS","FIELD MANUAL","EXIT"}),"Main menu options differ.");
        Check(GameMenu.Instance.headingFont!=null&&GameMenu.Instance.bodyFont!=null&&GameMenu.Instance.monoFont!=null,"Menu typography missing.");
        Check(PlayerHealth.Instance==null&&EnemyHealth.Alive.Count==0,"Gameplay running behind main menu.");
        Check(UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().actionsAsset!=null,"Menu has no UI input actions.");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/MainMenu.png");await Advance(.1f);
        buttons().Single(b=>b.name=="START").onClick.Invoke();await Ready();await Advance(.3f);
        var player=PlayerHealth.Instance.GetComponent<FpsController>();
        Check(!GameMenu.Instance.mainMenu&&player.InputEnabled,"Start did not enable gameplay.");
        var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("OutpostMenuCheck");
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));await Advance(.1f);
            Check(MinimapController.Instance.IsLarge,"M did not open map.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Advance(.1f);
            Check(!MinimapController.Instance.IsLarge&&!GameMenu.Instance.IsPaused,"Esc should close large map first.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Advance(.05f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(250);
            Check(GameMenu.Instance.IsPaused&&Time.timeScale==0&&!player.InputEnabled&&AudioListener.pause,"Esc did not freeze gameplay/audio/input.");
            Check(Cursor.lockState==CursorLockMode.None&&buttons().Select(b=>b.name).SequenceEqual(new[]{"RESUME","SETTINGS","CONTROLS","FIELD MANUAL","EXIT"}),"Pause controls/cursor incorrect.");
            float pausedAt=Time.time;Vector3 position=player.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(250);
            Check(Time.time==pausedAt&&Vector3.Distance(position,player.transform.position)<.001f,"Pause clock/player moved.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/PauseMenu.png");await Task.Delay(150);
            buttons().Single(b=>b.name=="RESUME").onClick.Invoke();await Advance(.2f);
            Check(!GameMenu.Instance.IsPaused&&player.InputEnabled&&Time.timeScale==1&&!AudioListener.pause&&Cursor.lockState==CursorLockMode.Locked,"Resume did not restore gameplay.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(150);
            Check(GameMenu.Instance.IsPaused,"Second pause failed.");
            buttons().Single(b=>b.name=="EXIT").onClick.Invoke();deadline=Time.realtimeSinceStartup+25;
            while(GameMenu.Instance==null||!GameMenu.Instance.mainMenu)
            {Check(Time.realtimeSinceStartup<deadline,"Exit did not return to menu.");await Task.Delay(16);}
            await Advance(.2f);
            Check(PlayerHealth.Instance==null&&EnemyHealth.Alive.Count==0&&Time.timeScale==1&&!AudioListener.pause,"Exit left gameplay or pause state behind.");
            buttons().Single(b=>b.name=="START").onClick.Invoke();await Ready();await Advance(.2f);
            Check(PlayerHealth.Instance.Health==100&&!GameMenu.Instance.IsPaused,"Starting again did not reset session.");
            var result=new{passed=true,mainMenu="Start / Settings / Controls / Field Manual / Exit",pause="Resume / Settings / Controls / Field Manual / Main Menu",exitToMenu=true,mapEscapePriority=true,clockAndMovementFrozen=true,audioPaused=true,resumeRestoresInput=true,freshRestart=true};Save("MenuRuntimeChecks",result);return result;
        }
        finally{InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();}
    }

    public static async Task<object> ApproachWalls()
    {
        await ReloadGame();var director=WaveDirector.Instance;director.StopAllCoroutines();
        var walls=director.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BoxCollider>(true)).Where(c=>c.transform.parent.name=="Approach lane invisible walls").ToArray();
        Check(walls.Length==6,"Expected six approach walls.");int physical=0,navBlocked=0,routes=0;
        foreach(var c in walls)
        {
            Check(!c.isTrigger&&c.GetComponents<Renderer>().Length==0,"Wall is visible or not solid.");
            var center=c.bounds.center;center.y=.4f;
            var axis=c.size.x<c.size.z?Vector3.right:Vector3.forward;
            var lane=c.name.StartsWith("West")?new Vector3(-58,.4f,-4.24f):new Vector3(2.6f,.4f,55);
            axis*=Mathf.Sign(Vector3.Dot(lane-center,axis));var from=center+axis*2;var to=center-axis*2;
            foreach(float height in new[]{1f,4f})
            {
                var a=from;a.y=height;Check(c.Raycast(new Ray(a,(to-from).normalized),out var hit,4),"Physics wall gap: "+c.name);physical++;
            }
            foreach(int type in new[]{0,-1372625422})
            {
                var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
                Check(NavMesh.SamplePosition(from,out var inside,.75f,filter),"No inside-lane navigation: "+c.name);
                bool outside=NavMesh.SamplePosition(to,out var end,.75f,filter);
                Check(!outside||NavMesh.Raycast(inside.position,end.position,out var hit,filter),"Navigation crosses invisible wall: "+c.name);navBlocked++;
            }
        }
        var target=GeneratorHealth.All.First().AttackPoints[0].position;
        foreach(var p in UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None).Where(p=>p.group=="Enemy"))
            foreach(int type in new[]{0,-1372625422})for(int i=0;i<9;i++)
            {
                var offset=i==8?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*.6f;
                var f=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};var path=new NavMeshPath();
                Check(NavMesh.SamplePosition(p.transform.position+offset,out var start,1.5f,f)&&PlayArea.Instance.Contains(start.position)&&NavMesh.SamplePosition(target,out var end,2,f)&&NavMesh.CalculatePath(start.position,end.position,f,path)&&path.status==NavMeshPathStatus.PathComplete,"Blocked spawn route: "+p.name);routes++;
            }
        var result=new{passed=true,walls=walls.Length,physicalRays=physical,navigationRays=navBlocked,spawnJitterRoutes=routes};Save("ApproachWallRuntimeChecks",result);return result;
    }

    public static async Task<object> EnglishUiAndBossWarning()
    {
        await ReloadGame();var d=WaveDirector.Instance;d.StopAllCoroutines();
        var hud=PlayerHealth.Instance.GetComponent<FpsHud>();
        var death=(TMPro.TMP_Text)typeof(FpsHud).GetField("deathText",Private).GetValue(hud);
        Check(death.text.Contains("DEAD")&&death.text.Contains("Press ENTER to try again"),"Death UI is not English.");
        var manager=PlayerHealth.Instance.GetComponentInChildren<WeaponManager>(true);var weapon=manager.Current;
        typeof(FpsWeapon).GetField("ammo",Private).SetValue(weapon,0);weapon.reserveAmmo=60;await Advance(.05f);
        var message=(TMPro.TMP_Text)typeof(FpsHud).GetField("msgText",Private).GetValue(hud);
        Check(message.text=="Press R to reload","Reload prompt is not English.");
        weapon.reserveAmmo=0;await Advance(.05f);Check(message.text=="Out of ammo. Find a resupply.","Empty-ammo prompt is not English.");
        manager.RefillAll();
        var player=PlayerHealth.Instance;player.TakeDamage(15,player.transform.position,Vector3.forward);
        var pickup=new GameObject("Temporary health UI check").AddComponent<Outpost.Pickups.PickupItem>();pickup.kind=Outpost.Pickups.PickupKind.Health;
        typeof(Outpost.Pickups.PickupItem).GetMethod("TryCollect",Private).Invoke(pickup,new object[]{player});await Advance(.05f);
        var toast=(TMPro.TMP_Text)typeof(FpsHud).GetField("toastText",Private).GetValue(hud);Check(toast.text.Contains(" HEALTH"),"Health pickup is not English.");
        typeof(WaveDirector).GetProperty("CurrentWave").GetSetMethod(true).Invoke(d,new object[]{3});
        d.bossDelay=1.2f;
        var sequence=(System.Collections.IEnumerator)typeof(WaveDirector).GetMethod("SpawnWave",Private).Invoke(d,new object[]{new WaveDirector.Wave{perSide=0,boss=true}});
        d.StartCoroutine(sequence);await Advance(.35f);
        var title=(TMPro.TMP_Text)typeof(WaveDirector).GetField("bannerText",Private).GetValue(d);
        var glow=(UnityEngine.UI.Image)typeof(WaveDirector).GetField("bossGlow",Private).GetValue(d);
        Check(title.text=="BOSSES INBOUND"&&title.color.r>title.color.g*2,"Boss warning is not highlighted red.");
        Check(glow.color.a>.1f&&!glow.raycastTarget,"Boss glow is absent or blocks input.");
        Check(glow.sprite.texture.GetPixel(32,8).a>.9f&&glow.sprite.texture.GetPixel(0,0).a<.01f,"Boss glow has no smooth falloff.");
        Check(!EnemyHealth.Alive.Any(h=>h.config.kind==EnemyKind.Boss),"Boss warning did not precede arrival.");
        float firstAlpha=glow.color.a;await Advance(.3f);Check(Mathf.Abs(firstAlpha-glow.color.a)>.01f,"Boss glow does not pulse.");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/BossWarningEnglish.png");
        await Advance(.7f);Check(EnemyHealth.Alive.Any(h=>h.config.kind==EnemyKind.Boss),"Boss did not arrive after warning.");
        var texts=d.GetComponentsInChildren<UnityEngine.UI.Text>(true).Concat(hud.GetComponentsInChildren<UnityEngine.UI.Text>(true)).Select(t=>t.text).ToArray();
        Check(!texts.Any(t=>new[]{"tekan ","dimulai","mendekati","waspada","darah","amunisi","hancur","berhasil"}.Any(word=>t.ToLowerInvariant().Contains(word))),"Indonesian content remains on gameplay UI.");
        var result=new{passed=true,death=death.text,reload="Press R to reload",emptyAmmo="Out of ammo. Find a resupply.",bossTitle=title.text,redTitle=true,redGlow=true,warningBeforeArrival=true};Save("EnglishUiRuntimeChecks",result);
        await ReloadGame();return result;
    }

    public static async Task<object> WarFx()
    {
        await ReloadGame();
        var director=WaveDirector.Instance;director.StopAllCoroutines();
        foreach(var h in EnemyHealth.Alive.ToArray())UnityEngine.Object.Destroy(h.gameObject);
        var player=PlayerHealth.Instance.GetComponent<FpsController>();
        var manager=player.GetComponentInChildren<WeaponManager>(true);
        var oldInput=player.InputEnabled;player.InputEnabled=false;
        var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Temporary VFX check target";
        target.transform.position=player.playerCamera.transform.position+player.playerCamera.transform.forward*4;
        target.transform.localScale=Vector3.one*1.4f;
        Physics.SyncTransforms();var rows=new List<object>();
        try
        {
            for(int i=0;i<2;i++)
            {
                manager.Select(i);manager.RefillAll();await Advance(.45f);var weapon=manager.Current;
                Check(weapon.muzzleFlash&&weapon.impactPrefab&&weapon.enemyImpactPrefab,"Missing weapon VFX references.");
                Check(!weapon.muzzleFlash.main.loop&&!weapon.muzzleFlash.main.playOnAwake,"Muzzle is not shot-driven.");
                var fire=typeof(FpsWeapon).GetMethod("TryFire",Private);int ammo=weapon.Ammo;
                var oldImpacts=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(s=>s.name=="Surface impact URP(Clone)");
                fire.Invoke(weapon,null);await Advance(.01f);
                Check(weapon.Ammo==ammo-1,"Shot did not consume ammo.");
                Check(weapon.muzzleFlash.GetComponentsInChildren<ParticleSystem>().Sum(s=>s.particleCount)>0,"Muzzle flash emitted no visible particles.");
                var impacts=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(s=>s.name=="Surface impact URP(Clone)").ToArray();
                Check(impacts.Length>oldImpacts,"Ray hit did not spawn surface impact.");
                Check(Vector3.Distance(weapon.muzzle.position,weapon.muzzleFlash.transform.position)<.001f,"Muzzle effect detached.");
                foreach(var r in weapon.muzzleFlash.GetComponentsInChildren<ParticleSystemRenderer>())
                    foreach(var m in r.sharedMaterials.Where(m=>m))Check(m.shader.name=="Universal Render Pipeline/Particles/Unlit"&&m.shader.isSupported,"Muzzle material unsupported.");
                if(i==0)ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/WarFxShot.png");
                rows.Add(new{weapon=weapon.name,particleCount=weapon.muzzleFlash.particleCount,impactCount=impacts.Length});
                await Advance(.3f);Check(!weapon.muzzleFlash.IsAlive(true),"Muzzle continues emitting after trigger release.");
            }
            await Advance(4.2f);
            Check(!UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(s=>s.name=="Surface impact URP(Clone)"),"Impact effects did not clean up.");
            var result=new{passed=true,weapons=rows,cleanup=true};Save("WarFxRuntimeChecks",result);return result;
        }
        finally{UnityEngine.Object.Destroy(target);manager.Select(0);player.InputEnabled=oldInput;}
    }

    public static async Task<object> Movement()
    {
        await Ready();
        var player=PlayerHealth.Instance.GetComponent<FpsController>();
        var cc=player.GetComponent<CharacterController>();
        Check(cc.enabled&&player.enabled&&player.InputEnabled,"Player movement disabled at startup.");
        var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("MovementCheck");
        var start=player.transform.position;float yaw=player.transform.eulerAngles.y;
        var distances=new Dictionary<string,float>();
        try
        {
            foreach(var key in new[]{Key.W,Key.UpArrow,Key.A,Key.LeftArrow,Key.S,Key.DownArrow,Key.D,Key.RightArrow})
            {
                player.Teleport(start,yaw);FpsController.LockCursor(true);
                await Advance(.05f);var before=player.transform.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Advance(.25f);
                var delta=player.transform.position-before;delta.y=0;
                distances[key.ToString()]=delta.magnitude;
                Check(delta.magnitude>.15f,"No movement for "+key+": "+delta);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Advance(.05f);
            }
            var result=new{passed=true,controllerEnabled=cc.enabled,distances};Save("MovementRuntimeChecks",result);return result;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
            if(player)player.Teleport(start,yaw);
        }
    }

    public static async Task<object> DisabledController()
    {
        await Ready();
        var cc=PlayerHealth.Instance.GetComponent<CharacterController>();
        var player=cc.GetComponent<FpsController>();
        var errors=new List<string>();
        Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception)errors.Add(message);};
        Application.logMessageReceived+=collect;
        var start=player.transform.position;
        cc.enabled=false;
        try
        {
            await Advance(.3f);
            Check(Vector3.Distance(start,player.transform.position)<.001f,"Disabled controller moved.");
            Check(errors.Count==0,"Disabled controller logged errors: "+string.Join("; ",errors));
            var result=new{passed=true,errors=errors.Count};Save("DisabledControllerChecks",result);return result;
        }
        finally{cc.enabled=true;Application.logMessageReceived-=collect;}
    }

    public static async Task<object> Recoil()
    {
        await Ready();
        var director=WaveDirector.Instance;director.StopAllCoroutines();
        foreach(var h in EnemyHealth.Alive.ToArray())UnityEngine.Object.Destroy(h.gameObject);
        var player=PlayerHealth.Instance.GetComponent<FpsController>();player.InputEnabled=false;
        var manager=player.GetComponentInChildren<WeaponManager>(true);manager.Select(0);manager.RefillAll();
        var rifle=manager.Current;var fire=typeof(FpsWeapon).GetMethod("TryFire",Private);
        await Advance(.45f);
        int before=rifle.Ammo,shots=0;float peak=0;
        Action fired=()=>shots++;rifle.OnFired+=fired;
        await Advance(1.15f,()=>{fire.Invoke(rifle,null);peak=Mathf.Max(peak,player.recoilPitch);});
        rifle.OnFired-=fired;
        Check(shots>=7&&shots<=13,"Unexpected automatic fire count: "+shots);
        Check(before-rifle.Ammo==shots,"Ammo count disagrees with fired events.");
        Check(peak>.6f&&peak<=rifle.maxRecoilPitch+.01f&&!float.IsNaN(peak),"Recoil did not build within its limits.");
        await Advance(1.25f);
        float recovered=player.recoilPitch;Check(Mathf.Abs(recovered)<.08f,"Rifle recoil did not recover: "+recovered);

        var directFire=typeof(FpsWeapon).GetMethod("Fire",Private);var ads=typeof(FpsWeapon).GetField("adsT",Private);
        ads.SetValue(rifle,0f);directFire.Invoke(rifle,null);float hipPeak=0;
        await Advance(.22f,()=>hipPeak=Mathf.Max(hipPeak,player.recoilPitch));
        await Advance(.9f);
        ads.SetValue(rifle,1f);directFire.Invoke(rifle,null);float adsPeak=0;
        await Advance(.22f,()=>adsPeak=Mathf.Max(adsPeak,player.recoilPitch));
        Check(adsPeak>0&&adsPeak<hipPeak*.8f,"ADS did not reduce shot impulse.");
        manager.Select(1);Check(Mathf.Abs(player.recoilPitch)<.001f,"Weapon switch retained rifle recoil.");
        await Advance(.45f);var pistol=manager.Current;int pistolBefore=pistol.Ammo;
        fire.Invoke(pistol,null);await Advance(.18f);fire.Invoke(pistol,null);await Advance(.1f);
        Check(pistolBefore-pistol.Ammo==2,"Pistol did not fire two paced shots.");
        Check(player.recoilPitch>0&&player.recoilPitch<=pistol.maxRecoilPitch+.01f,"Pistol recoil invalid.");
        var result=new{passed=true,automaticShots=shots,peakPitch=peak,recoveredPitch=recovered,singleShotHipPeak=hipPeak,singleShotAdsPeak=adsPeak,pistolShots=2,pistolPitch=player.recoilPitch};
        Save("RecoilRuntimeChecks",result);return result;
    }

    public static async Task<object> BossAndRadar()
    {
        await ReloadGame();
        var director=WaveDirector.Instance;director.StopAllCoroutines();
        foreach(var h in EnemyHealth.Alive.ToArray())UnityEngine.Object.Destroy(h.gameObject);
        var player=PlayerHealth.Instance.GetComponent<FpsController>();player.InputEnabled=false;
        var starts=UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None).Where(s=>s.group=="Enemy").GroupBy(s=>s.lane).Select(g=>g.OrderBy(p=>p.name).First()).ToArray();
        var variants=director.catalog.variants.Where(v=>v.prefab.GetComponent<EnemyHealth>().config.kind==EnemyKind.Boss).ToArray();
        var actors=new List<GameObject>();var positions=new List<Vector3>();
        for(int i=0;i<variants.Length;i++)
        {
            var actor=EnemyDebugSpawner.SpawnAt(variants[i].prefab,starts[i].transform.position);
            Check(actor!=null,"Boss failed to spawn: "+variants[i].id);actors.Add(actor);positions.Add(actor.transform.position);
            Check(actor.transform.localScale==Vector3.one,"Boss root is scaled.");
        }
        await Advance(5.5f);
        var records=new List<object>();
        for(int i=0;i<actors.Count;i++)
        {
            var actor=actors[i];var agent=actor.GetComponent<NavMeshAgent>();var blip=actor.GetComponent<MinimapBlip>();var aux=actor.GetComponent<Aux_MP>();
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};var settings=NavMesh.GetSettingsByID(agent.agentTypeID);
            float moved=Vector3.Distance(positions[i],actor.transform.position);
            Check(agent.isOnNavMesh&&agent.hasPath&&agent.pathStatus==NavMeshPathStatus.PathComplete,"Boss has no complete runtime path: "+actor.name);
            Check(moved>2f,"Boss did not move after spawning: "+actor.name);
            Check(settings.agentHeight>=agent.height&&settings.agentRadius>=agent.radius,"Bake clearance smaller than boss agent.");
            Check(blip!=null&&blip.enabled&&aux!=null&&aux.enabled&&aux.Arrow==1,"Boss radar/edge arrow missing.");
            Check(Mathf.Approximately(aux.IconSizeMultiplier,.29f),"Boss marker size is incorrect.");
            var arrow=(GameObject)typeof(Aux_MP).GetField("Aux_Icon_A",Private).GetValue(aux);
            Check(arrow!=null&&Mathf.Approximately(arrow.GetComponent<Aux_Icon>().Local_Icon.transform.localScale.x,.42f),"Small-map arrow size is incorrect.");
            records.Add(new{actor.name,moved,agentType=agent.agentTypeID,agent.height,agent.radius,bakeHeight=settings.agentHeight,bakeRadius=settings.agentRadius,rootScale=actor.transform.localScale.ToString("F2"),color=blip.color.ToString()});
        }
        var normal=director.catalog.variants.First(v=>v.prefab.GetComponent<EnemyHealth>().config.kind==EnemyKind.Normal);
        var zombie=EnemyDebugSpawner.SpawnAt(normal.prefab,starts[0].transform.position+Vector3.forward*2);
        await Advance(.25f);
        Check(zombie.GetComponent<Aux_MP>().Arrow==1,"Normal zombie has no off-screen indicator.");
        Check(Mathf.Approximately(zombie.GetComponent<Aux_MP>().IconSizeMultiplier,.24f),"Zombie marker size is incorrect.");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/SmallMapMarkersLatest.png");await Advance(.1f);
        var ctl=UnityEngine.Object.FindFirstObjectByType<MinimapController>();
        typeof(MinimapController).GetProperty("IsLarge").GetSetMethod(true).Invoke(ctl,new object[]{true});
        await Advance(.4f);
        Check(EnemyHealth.Alive.All(h=>Mathf.Approximately(h.GetComponent<Aux_MP>().IconSizeMultiplier,h.config.kind==EnemyKind.Boss?.245f:.20f)&&Mathf.Approximately(h.GetComponent<Aux_MP>().ArrowSizeMultiplier,.32f)),"Large-map marker sizes did not update.");
        ScreenCapture.CaptureScreenshot("Assets/TacticalOutpost/Documentation/Debug/LargeMapMarkersLatest.png");await Advance(.1f);
        var tracked=EnemyHealth.Alive.Count(h=>h.GetComponent<MinimapBlip>().enabled&&h.GetComponent<Aux_MP>().enabled);
        Check(tracked==EnemyHealth.Alive.Count,"Not all living zombies have live radar markers.");
        var result=new{passed=true,bosses=records,livingZombies=EnemyHealth.Alive.Count,radarTracked=tracked,largeMap=ctl.IsLarge};
        Save("BossRadarRuntimeChecks",result);return result;
    }

    public static async Task<object> MapControls()
    {
        await Ready();
        var ctl=MinimapController.Instance;Check(ctl!=null,"Minimap controller missing.");
        var player=PlayerHealth.Instance.GetComponent<FpsController>();player.InputEnabled=true;
        var oldKeyboard=Keyboard.current;var testKeyboard=InputSystem.AddDevice<Keyboard>("OutpostMapCheck");
        try
        {
            FpsController.LockCursor(true);
            typeof(MinimapController).GetProperty("IsLarge").GetSetMethod(true).Invoke(ctl,new object[]{false});
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.M));await Advance(.08f);
            Check(ctl.IsLarge,"M did not open large map.");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());await Advance(.05f);
            var locked=Cursor.lockState;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Escape));await Advance(.08f);
            Check(!ctl.IsLarge,"Escape did not close large map.");
            Check(Cursor.lockState==locked,"Escape closing map also changed cursor state.");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());await Advance(.05f);
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.M));await Advance(.08f);
            Check(ctl.IsLarge,"M could not reopen map after Escape.");
            var result=new{passed=true,mOpens=true,escapeCloses=true,cursorRetained=true,mReopens=true};Save("MinimapControlChecks",result);return result;
        }
        finally{InputSystem.RemoveDevice(testKeyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();}
    }

    public static async Task<object> NavigationTravel()
    {
        await Ready();
        var director=WaveDirector.Instance;director.StopAllCoroutines();
        foreach(var h in EnemyHealth.Alive.ToArray())UnityEngine.Object.Destroy(h.gameObject);
        var player=PlayerHealth.Instance.GetComponent<FpsController>();var oldPosition=player.transform.position;float oldYaw=player.transform.eulerAngles.y;
        bool oldEnabled=player.enabled;float oldTimeScale=Time.timeScale;
        var generator=GeneratorHealth.All.Single();float initialHealth=generator.Health,originalMaxHealth=generator.maxHealth;
        var starts=UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None).Where(s=>s.group=="Enemy").GroupBy(s=>s.lane).Select(g=>g.OrderBy(p=>p.name).First()).ToArray();
        var bosses=director.catalog.variants.Where(v=>v.prefab.GetComponent<EnemyHealth>().config.kind==EnemyKind.Boss).ToArray();
        var normal=director.catalog.variants.First(v=>v.id=="walker");
        var actors=new List<GameObject>();var firstPositions=new List<Vector3>();var lanes=new List<string>();
        try
        {
            player.enabled=false;player.Teleport(new Vector3(250,50,250),0);
            generator.maxHealth=100000;generator.ResetHealth();initialHealth=generator.Health;
            await Advance(.1f);
            float longest=0;
            for(int i=0;i<starts.Length;i++)
                foreach(var prefab in new[]{bosses[i].prefab,normal.prefab})
                {
                    var actor=EnemyDebugSpawner.SpawnAt(prefab,starts[i].transform.position+(prefab==normal.prefab?Vector3.forward*2:Vector3.zero));
                    Check(actor!=null,"Travel-check spawn failed.");actors.Add(actor);firstPositions.Add(actor.transform.position);lanes.Add(starts[i].lane.ToString());
                    var a=actor.GetComponent<NavMeshAgent>();var filter=new NavMeshQueryFilter{agentTypeID=a.agentTypeID,areaMask=a.areaMask};
                    var path=new NavMeshPath();var target=generator.AttackPoints[0].position;
                    Check(NavMesh.SamplePosition(target,out NavMeshHit hit,1.5f,filter)&&NavMesh.CalculatePath(actor.transform.position,hit.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete,"Travel route unavailable.");
                    float length=0;for(int c=1;c<path.corners.Length;c++)length+=Vector3.Distance(path.corners[c-1],path.corners[c]);
                    longest=Mathf.Max(longest,length/actor.GetComponent<EnemyHealth>().config.walkSpeed);
                }
            Time.timeScale=10;
            float began=Time.time,end=began+longest+18,deadline=Time.realtimeSinceStartup+55;
            while(Time.time<end)
            {
                Check(Application.isPlaying&&Time.timeScale>0,"Travel check interrupted.");
                Check(Time.realtimeSinceStartup<deadline,"Travel check exceeded wall-clock budget.");
                if(actors.All(g=>{var d=generator.Body.ClosestPoint(g.transform.position)-g.transform.position;d.y=0;return d.magnitude<=g.GetComponent<EnemyHealth>().config.attackRange+.6f;})&&generator.Health<initialHealth)break;
                await Task.Delay(32);
            }
            var rows=actors.Select((g,i)=>{var a=g.GetComponent<NavMeshAgent>();var d=generator.Body.ClosestPoint(g.transform.position)-g.transform.position;d.y=0;return new{name=g.name,lane=lanes[i],moved=Vector3.Distance(firstPositions[i],g.transform.position),distanceToGenerator=d.magnitude,arrived=d.magnitude<=g.GetComponent<EnemyHealth>().config.attackRange+.6f,a.isOnNavMesh,path=a.pathStatus.ToString(),state=g.GetComponent<EnemyBrain>().CurrentState.ToString()};}).ToArray();
            var result=new{passed=rows.All(r=>r.arrived&&r.isOnNavMesh),simulationSeconds=Time.time-began,generatorDamage=initialHealth-generator.Health,actors=rows};Save("NavigationTravelChecks",result);
            Check(result.passed,"Some agents did not reach generator; see NavigationTravelChecks.json.");return result;
        }
        finally
        {
            Time.timeScale=oldTimeScale;
            if(player){player.Teleport(oldPosition,oldYaw);player.enabled=oldEnabled;}
            foreach(var actor in actors)if(actor)UnityEngine.Object.Destroy(actor);
            if(generator){generator.maxHealth=originalMaxHealth;generator.ResetHealth();}
        }
    }

    static async Task ReloadGame()
    {
        var load=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Scenes/Gameplay/TacticalOutpost_Game.unity",UnityEngine.SceneManagement.LoadSceneMode.Single);
        float deadline=Time.realtimeSinceStartup+25;
        while(!load.isDone){Check(Time.realtimeSinceStartup<deadline,"Gameplay reload timed out.");await Task.Delay(16);}
        await Ready();await Advance(.1f);
    }

    public static async Task<object> ClearAdvancesWave()
    {
        await ReloadGame();var d=WaveDirector.Instance;d.StopAllCoroutines();
        d.firstWaveDelay=.05f;d.bannerTime=.05f;d.spawnInterval=.2f;d.waveInterval=60f;d.bossDelay=.8f;
        d.waves=new[]{new WaveDirector.Wave{perSide=1},new WaveDirector.Wave{perSide=1,boss=true}};
        var cleared=new List<int>();d.OnWaveCleared+=cleared.Add;
        d.StartCoroutine((System.Collections.IEnumerator)typeof(WaveDirector).GetMethod("Run",Private).Invoke(d,null));
        float deadline=Time.realtimeSinceStartup+15;
        while(d.CurrentPhase!=WaveDirector.Phase.Spawning||d.EnemiesAlive!=1)
        {Check(Time.realtimeSinceStartup<deadline,"First enemy did not arrive.");await Task.Delay(16);}
        EnemyDebugSpawner.KillAll();await Advance(.1f);
        Check(d.CurrentWave==1,"An empty gap between scheduled enemies advanced the wave.");
        while(d.CurrentPhase!=WaveDirector.Phase.Fighting)
        {Check(Time.realtimeSinceStartup<deadline,"First wave did not finish entering.");await Task.Delay(16);}
        Check(d.EnemiesAlive==1,"Pending enemy was skipped.");float clearAt=Time.time;
        EnemyDebugSpawner.KillAll();
        while(d.CurrentWave!=2)
        {Check(Time.realtimeSinceStartup<deadline,"Clearing wave did not advance.");await Task.Delay(16);}
        float advanceDelay=Time.time-clearAt;Check(advanceDelay<1f,"Clear still waits for countdown.");
        Check(cleared.SequenceEqual(new[]{1}),"Wave clear event missing or duplicated.");
        while(d.CurrentPhase!=WaveDirector.Phase.Spawning)
        {Check(Time.realtimeSinceStartup<deadline,"Final wave did not begin.");await Task.Delay(16);}
        await Advance(.5f);EnemyDebugSpawner.KillAll();await Advance(.15f);
        Check(d.CurrentPhase!=WaveDirector.Phase.Victory,"Victory before pending boss arrived.");
        while(d.CurrentPhase!=WaveDirector.Phase.Fighting)
        {Check(Time.realtimeSinceStartup<deadline,"Boss wave did not finish entering.");await Task.Delay(16);}
        Check(d.EnemiesAlive==3&&EnemyHealth.Alive.Count(h=>!h.IsDead&&h.config.kind==EnemyKind.Boss)==3,"Pending bosses were skipped.");
        EnemyDebugSpawner.KillAll();await Advance(.4f);
        Check(d.CurrentPhase==WaveDirector.Phase.Victory&&cleared.SequenceEqual(new[]{1,2}),"Final clear did not end match exactly once.");
        var result=new{passed=true,clearToNextWaveSeconds=advanceDelay,pendingEnemiesPreserved=true,pendingBossPreserved=true,clearEvents=cleared.ToArray(),finalVictory=true};
        Save("ClearWaveChecks",result);await ReloadGame();return result;
    }

    public static async Task<object> TimedWaves()
    {
        await ReloadGame();var d=WaveDirector.Instance;d.StopAllCoroutines();
        d.firstWaveDelay=.1f;d.bannerTime=.1f;d.spawnInterval=.03f;d.waveInterval=1.6f;d.bossDelay=.1f;
        Check(d.waves.Select(w=>w.perSide*2).SequenceEqual(new[]{24,36,48}),"Saved wave counts are incorrect.");
        var counts=new Dictionary<int,int>();var carried=new Dictionary<int,int>();int bossCount=0;
        var bossLanes=new List<LaneId>();var points=UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None).Where(p=>p.group=="Enemy").ToArray();
        Action<EnemyHealth> spawned=h=>{int w=d.CurrentWave;if(!counts.ContainsKey(w))counts[w]=0;counts[w]++;if(h.config.kind==EnemyKind.Boss){bossCount++;bossLanes.Add(points.OrderBy(p=>Vector3.Distance(p.transform.position,h.transform.position)).First().lane);Check(h.transform.localScale==Vector3.one,"Boss root scaled during wave spawn.");}};
        Action<int> started=w=>carried[w]=d.EnemiesAlive;
        EnemyHealth.AnySpawned+=spawned;d.OnWaveStarted+=started;float oldScale=Time.timeScale;
        try
        {
            Time.timeScale=4;
            d.StartCoroutine((System.Collections.IEnumerator)typeof(WaveDirector).GetMethod("Run",Private).Invoke(d,null));
            float deadline=Time.realtimeSinceStartup+20;
            while(d.CurrentWave<3||d.CurrentPhase!=WaveDirector.Phase.Fighting)
            {Check(Application.isPlaying&&Time.timeScale>0&&Time.realtimeSinceStartup<deadline,"Timed waves did not advance.");await Task.Delay(16);}
            Check(counts[1]==24&&counts[2]==36&&counts[3]==51,"Wave spawn totals changed.");
            Check(carried[2]>0&&carried[3]>0,"Next wave waited for previous enemies to die.");
            Check(d.EnemiesAlive==111&&bossCount==3,"Living totals/boss count invalid.");
            Check(bossLanes.Distinct().Count()==3&&bossLanes.OrderBy(x=>x).SequenceEqual(points.Select(p=>p.lane).Distinct().OrderBy(x=>x)),"Expected exactly one boss at every gate.");
            EnemyDebugSpawner.KillAll();await Advance(.4f);
            Check(d.CurrentPhase==WaveDirector.Phase.Victory,"Final clear did not produce victory.");
            var result=new{passed=true,spawned=counts,previousEnemiesAtWaveStart=carried,totalSpawned=111,bosses=bossCount,bossLanes=bossLanes.ToArray(),victory=true};Save("TimedWaveChecks",result);return result;
        }
        finally{EnemyHealth.AnySpawned-=spawned;d.OnWaveStarted-=started;Time.timeScale=oldScale;}
    }

    static async Task EnterAndWait(Keyboard keyboard,int oldPlayerId)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));float deadline=Time.realtimeSinceStartup+25;
        while(PlayerHealth.Instance==null||PlayerHealth.Instance.GetInstanceID()==oldPlayerId||MinimapController.Instance==null)
        {
            Check(Application.isPlaying&&Time.realtimeSinceStartup<deadline,"Enter did not restart gameplay.");await Task.Delay(16);
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Advance(.15f);
    }

    public static async Task<object> DefeatAndRestart()
    {
        await ReloadGame();var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("OutpostRestartCheck");
        try
        {
            var hp=PlayerHealth.Instance;int id=hp.GetInstanceID();hp.TakeDamage(1000,hp.transform.position,Vector3.forward);
            Check(hp.IsDead&&WaveDirector.Instance.CurrentPhase==WaveDirector.Phase.Defeat&&PlayerDeathView.Instance.IsActive,"Player death did not stop wave.");
            var text=hp.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t=>t.name=="DeathText");
            Check(text!=null&&text.text.StartsWith("DEAD"),"Death label is not DEAD.");
            await Task.Delay(2200);
            await EnterAndWait(keyboard,id);
            Check(!PlayerHealth.Instance.IsDead&&PlayerHealth.Instance.Health==100,"Player did not reset after restart.");
            id=PlayerHealth.Instance.GetInstanceID();GeneratorHealth.All.Single().Damage(100000);
            Check(WaveDirector.Instance.CurrentPhase==WaveDirector.Phase.Defeat,"Generator defeat did not stop game.");
            float deadline=Time.realtimeSinceStartup+15;
            while(!WaveDirector.Instance.GetComponent<Outpost.GeneratorDestruction>().Finished){Check(Time.realtimeSinceStartup<deadline,"Destruction cinematic did not finish.");await Task.Delay(30);}
            await EnterAndWait(keyboard,id);
            Check(GeneratorHealth.All.Count==1&&GeneratorHealth.All[0].Health==1000,"Generator did not reset.");
            var result=new{passed=true,deadLabel=true,playerDeathStopsWave=true,playerRestart=true,generatorDefeat=true,generatorRestart=true};Save("RestartRuntimeChecks",result);return result;
        }
        finally{InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();}
    }
}
