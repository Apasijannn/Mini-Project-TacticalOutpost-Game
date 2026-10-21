using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Outpost;
using Outpost.Env;
using Outpost.Player;
using Outpost.Enemy;
using Outpost.Pickups;
using Outpost.Audio;
using Outpost.UI;
using MiniMapModular;

public static class AdaptIndustrialGameplay
{
    const string Gameplay = "Assets/TacticalOutpost/Gameplay";
    const string ScenePath = "Assets/Scenes/Gameplay/TacticalOutpost_Game.unity";
    const string Radar = "Assets/ThirdParty/Gameplay/Mini Mapa(Radar)";
    static Scene GameScene => SceneManager.GetSceneByPath(ScenePath);
    static GameObject Systems => GameScene.GetRootGameObjects().Single(g => g.name == "Industrial Game Session").transform.Find("Gameplay Systems").gameObject;
    static Transform Environment => SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene).GetRootGameObjects().Single().transform;

    static T Asset<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static GameObject Child(string name, Transform parent)
    {
        if (parent.Find(name)) throw new InvalidOperationException("Already authored: " + name);
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false); return go;
    }

    static void Save()
    {
        EditorSceneManager.MarkSceneDirty(GameScene);
        if (!EditorSceneManager.SaveScene(GameScene)) throw new IOException("Gameplay scene save failed.");
        AssetDatabase.SaveAssets();
    }

    public static string OrganizeAndPlayer()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Edit Mode required.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new InvalidOperationException("Gameplay scene already exists.");
        Folder("Assets/Scenes/Archive"); Folder("Assets/Scenes/References"); Folder("Assets/Scenes/Gameplay");
        foreach (var pair in new[]{
            new[]{"Assets/Scenes/Outpost_Draft.unity","Assets/Scenes/Archive/Outpost_Draft.unity"},
            new[]{"Assets/Scenes/ArtistDemo_Reference.unity","Assets/Scenes/References/ArtistDemo_Reference.unity"}})
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(pair[0])) continue;
            var error = AssetDatabase.MoveAsset(pair[0], pair[1]);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        string[] names = {"Player", "Enemy", "Obstacle", "Cover", "Generator", "Ground", "Marker", "Fence"};
        for (int i = 0; i < names.Length; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i + 8);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != names[i]) throw new InvalidOperationException("Layer conflict: " + (i + 8));
        }
        for (int i = 0; i < names.Length; i++) layers.GetArrayElementAtIndex(i + 8).stringValue = names[i];
        tags.ApplyModifiedPropertiesWithoutUndo();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("Industrial Game Session");
        var session = root.AddComponent<IndustrialGameSession>();
        var systems = Child("Gameplay Systems", root.transform); systems.SetActive(false); session.systems = systems;
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(Gameplay + "/Prefabs/Player_FPS.prefab"), scene);
        player.transform.SetParent(systems.transform, false);
        var overrides = JArray.Parse(File.ReadAllText("Docs/Integration/PlayerSceneOverrides.json"));
        int applied = 0;
        var sourceObjects = player.GetComponentsInChildren<Component>(true).Where(c => c).Cast<UnityEngine.Object>()
            .Concat(player.GetComponentsInChildren<Transform>(true).Select(t => (UnityEngine.Object)t.gameObject));
        var byId = new Dictionary<long,UnityEngine.Object>();
        foreach (var obj in sourceObjects)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            if (source && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id)) byId[id] = obj;
        }
        foreach (JObject entry in overrides)
        {
            long id = long.Parse((string)entry["target"], CultureInfo.InvariantCulture);
            if (!byId.TryGetValue(id, out var obj)) throw new InvalidOperationException("Pose target not found: " + id);
            var so = new SerializedObject(obj); var prop = so.FindProperty((string)entry["property"]);
            if (prop == null) throw new InvalidOperationException("Pose property missing: " + entry);
            string value = (string)entry["value"];
            if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = float.Parse(value, CultureInfo.InvariantCulture);
            else if (prop.propertyType == SerializedPropertyType.String) prop.stringValue = value;
            else throw new InvalidOperationException("Unhandled pose property: " + prop.propertyType);
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); applied++;
        }
        Folder(Gameplay + "/Prefabs/Adapted");
        bool saved;
        PrefabUtility.SaveAsPrefabAsset(player, Gameplay + "/Prefabs/Adapted/Player_Industrial.prefab", out saved);
        if (!saved || applied != overrides.Count) throw new InvalidOperationException("Player pose preservation failed.");
        player.transform.position = Environment.Find("GameplayMarkers/PlayerStart").position + Vector3.up * .08f;
        player.transform.rotation = Quaternion.LookRotation(new Vector3(-5.34f, player.transform.position.y, -29.11f) - player.transform.position);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save gameplay scene.");
        EditorBuildSettings.scenes = new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene(IndustrialGameSession.EnvironmentScene,true)}
            .Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath && s.path!=IndustrialGameSession.EnvironmentScene)).ToArray();
        EditorSceneManager.playModeStartScene = Asset<SceneAsset>(ScenePath);
        AssetDatabase.SaveAssets();
        return "Player authored; all " + applied + " scene overrides preserved. Industrial environment not saved or changed.";
    }

    static Vector3 NavPoint(Vector3 desired, float radius = 2f)
    {
        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, radius, NavMesh.AllAreas) || Mathf.Abs(hit.position.y-desired.y)>1.2f)
            throw new InvalidOperationException("No matching ground navigation at " + desired);
        return hit.position;
    }

    public static string NavigationAndObjective()
    {
        SceneManager.SetActiveScene(GameScene);
        var navigation = Child("Industrial Navigation", Systems.transform).AddComponent<NavMeshSurface>();
        navigation.agentTypeID = 0; navigation.collectObjects = CollectObjects.Volume;
        navigation.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navigation.layerMask = ~((1<<8)|(1<<9)|(1<<12)|(1<<14)|(1<<2)|(1<<5));
        navigation.center = new Vector3(2,5,0); navigation.size = new Vector3(160,30,145);
        navigation.overrideVoxelSize = true; navigation.voxelSize = .16f;
        var oldSurface = Environment.GetComponent<NavMeshSurface>();
        oldSurface.RemoveData();
        try
        {
            // Build from the existing colliders; no environment transform or mesh is edited.
            navigation.gameObject.SetActive(true);
            navigation.BuildNavMesh();
            if (!navigation.navMeshData) throw new InvalidOperationException("Navigation bake returned no data.");
            Folder(Gameplay + "/Navigation");
            AssetDatabase.CreateAsset(navigation.navMeshData, Gameplay + "/Navigation/IndustrialGameplay.asset");
            navigation.AddData();
            var area = Child("Industrial Play Area", Systems.transform).AddComponent<PlayArea>();
            area.min = new Vector2(-72,-64); area.max = new Vector2(72,64);

            var genTransform = Environment.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("076 • Generator_v1"));
            var bounds = genTransform.GetComponent<Renderer>().bounds;
            var objective = Child("Generator Objective - industrial power station", Systems.transform);
            objective.layer = 12; objective.transform.position = bounds.center;
            var body = objective.AddComponent<BoxCollider>(); body.size = bounds.size; body.isTrigger = true;
            var slot = objective.AddComponent<EnvGeneratorSlot>(); slot.id = "Industrial Generator"; slot.maxHp = 400;
            var hp = objective.AddComponent<GeneratorHealth>(); hp.maxHealth = 1000;
            var bar = objective.AddComponent<GeneratorHealthBar>(); bar.generator = hp; bar.width = 12; bar.label = "POWER STATION";
            bar.barBackground = Asset<Sprite>("Assets/ThirdParty/Gameplay/2D Gui Shooter + Icons/Ui/Icon_34.png");
            bar.barFill = Asset<Sprite>("Assets/ThirdParty/Gameplay/2D Gui Shooter + Icons/Ui/Icon_35.png");
            var points = Child("Attack Points", objective.transform).transform;
            var attacks = ConnectedAttackPoints(points, bounds, Systems.GetComponentInChildren<FpsController>(true).transform.position);
            slot.attackPoints=attacks.ToArray();

            var spawns = Child("Enemy Spawn Lanes",Systems.transform).transform;
            var records = new List<object>();
            foreach (var lane in new[]{new{marker="Spawn_W_Main",id=LaneId.West},new{marker="Spawn_N_Flank",id=LaneId.North},new{marker="Spawn_S_Utility",id=LaneId.South}})
            {
                var origin = Environment.Find("GameplayMarkers/"+lane.marker).position;
                var group = Child(lane.id.ToString(),spawns).transform;
                int placed=0;
                foreach (float along in new[]{0f,2.5f,-2.5f,5f,-5f,7.5f,-7.5f})
                {
                    var desired=origin+(lane.id==LaneId.West?Vector3.forward:Vector3.right)*along;
                    if(!NavMesh.SamplePosition(desired,out NavMeshHit hit,2.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-origin.y)>1.2f)continue;
                    if(group.Cast<Transform>().Any(t=>Vector3.Distance(t.position,hit.position)<1.5f))continue;
                    var path=new NavMeshPath();
                    if(!attacks.Any(t=>NavMesh.CalculatePath(hit.position,t.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete))continue;
                    var point=Child(lane.id+" "+(++placed).ToString("00"),group).AddComponent<EnvSpawnPoint>();
                    point.group="Enemy";point.lane=lane.id;point.gameObject.layer=14;point.transform.position=hit.position;
                    var direction=bounds.center-hit.position;direction.y=0;point.transform.rotation=Quaternion.LookRotation(direction);
                    records.Add(new{lane=lane.id.ToString(),point=point.name,position=new[]{hit.position.x,hit.position.y,hit.position.z}});
                }
                if(placed<3)throw new InvalidOperationException("Lane has fewer than 3 reachable spawns: "+lane.id+" ("+placed+")");
            }
            var player=Systems.GetComponentInChildren<FpsController>(true);
            player.transform.position=NavPoint(player.transform.position,3f)+Vector3.up*.08f;
            File.WriteAllText("Docs/Integration/SpawnLayout.json",JsonConvert.SerializeObject(records,Formatting.Indented));
            File.WriteAllText("Docs/Integration/GeneratorAttackPoints.json",JsonConvert.SerializeObject(attacks.Select(t=>new{t.name,position=new[]{t.position.x,t.position.y,t.position.z}}),Formatting.Indented));
            navigation.enabled=false;
            GameScene.GetRootGameObjects().Single(g=>g.name=="Industrial Game Session").GetComponent<IndustrialGameSession>().navigation=navigation;
            Save();
            return "Separate navigation baked; "+records.Count+" connected spawns across West/North/South; "+attacks.Count+" generator attack points.";
        }
        finally { navigation.RemoveData();navigation.enabled=false;oldSurface.AddData(); }
    }

    static List<Transform> ConnectedAttackPoints(Transform parent, Bounds bounds, Vector3 anchor)
    {
        var accepted = new List<Vector3>();
        var candidates = new List<Vector3>();
        float y=bounds.min.y+.1f;
        for(float x=bounds.min.x+1.5f;x<=bounds.max.x-1.5f;x+=1.8f)
        {
            candidates.Add(new Vector3(x,y,bounds.max.z+1.05f));
            candidates.Add(new Vector3(x,y,bounds.min.z-1.05f));
        }
        for(float z=bounds.min.z+1.5f;z<=bounds.max.z-1.5f;z+=1.8f)
        {
            candidates.Add(new Vector3(bounds.min.x-1.05f,y,z));
            candidates.Add(new Vector3(bounds.max.x+1.05f,y,z));
        }
        foreach(var desired in candidates)
        {
            if(!NavMesh.SamplePosition(desired,out NavMeshHit hit,1.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-y)>1.2f)continue;
            var horizontal=new Vector2(hit.position.x-Mathf.Clamp(hit.position.x,bounds.min.x,bounds.max.x),hit.position.z-Mathf.Clamp(hit.position.z,bounds.min.z,bounds.max.z));
            if(horizontal.magnitude<.6f||horizontal.magnitude>1.65f||accepted.Any(p=>Vector3.Distance(p,hit.position)<1.65f))continue;
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(anchor,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            accepted.Add(hit.position);
            if(accepted.Count==6)break;
        }
        if(accepted.Count<4)throw new InvalidOperationException("Fewer than four connected attack positions: "+accepted.Count);
        foreach(var child in parent.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        var attacks=new List<Transform>();
        foreach(var p in accepted)
        {
            var point=Child("Attack "+(attacks.Count+1).ToString("00"),parent).transform;
            point.position=p;point.LookAt(new Vector3(bounds.center.x,p.y,bounds.center.z));attacks.Add(point);
        }
        return attacks;
    }

    public static string RefineObjective()
    {
        var navigation=Systems.GetComponentInChildren<NavMeshSurface>(true);
        var old=Environment.GetComponent<NavMeshSurface>();
        old.RemoveData();navigation.AddData();
        try
        {
            var slot=Systems.GetComponentInChildren<EnvGeneratorSlot>(true);
            var box=slot.GetComponent<BoxCollider>();
            var bounds=new Bounds(box.transform.TransformPoint(box.center),box.size);
            slot.attackPoints=ConnectedAttackPoints(slot.transform.Find("Attack Points"),bounds,Systems.GetComponentInChildren<FpsController>(true).transform.position).ToArray();
            File.WriteAllText("Docs/Integration/GeneratorAttackPoints.json",JsonConvert.SerializeObject(slot.attackPoints.Select(t=>new{t.name,position=new[]{t.position.x,t.position.y,t.position.z}}),Formatting.Indented));
            Save();return slot.attackPoints.Length+" attack points on the connected main navigation network.";
        }
        finally {navigation.RemoveData();old.AddData();}
    }

    public static string WavesDropsAudio()
    {
        SceneManager.SetActiveScene(GameScene);
        var director=Child("Wave Director",Systems.transform).AddComponent<WaveDirector>();
        director.catalog=Asset<EnemyCatalog>(Gameplay+"/Enemies/EnemyCatalog.asset");
        var drops=Child("Drop Manager",Systems.transform).AddComponent<DropManager>();
        drops.ammoPrefab=Asset<GameObject>(Gameplay+"/Pickups/Pickup_Ammo.prefab");
        drops.healthPrefab=Asset<GameObject>(Gameplay+"/Pickups/Pickup_Health.prefab");
        drops.groundMask=~((1<<8)|(1<<9)|(1<<12)|(1<<14)|(1<<2)|(1<<5));
        var audio=Child("Audio Manager",Systems.transform).AddComponent<OutpostAudio>();
        const string a="Assets/ThirdParty/Gameplay/Audio/";
        audio.switchWeapon=Asset<AudioClip>(a+"switch_weapon.wav");audio.playerDead=Asset<AudioClip>(a+"player_dead.wav");
        audio.pickAmmo=Asset<AudioClip>(a+"pick_ammo.wav");audio.pickHealth=Asset<AudioClip>(a+"pick_health.wav");
        audio.zombieGroan=Asset<AudioClip>(a+"zombie.wav");audio.zombieBite=Asset<AudioClip>(a+"zombie_bite.wav");audio.bossRoar=Asset<AudioClip>(a+"zombie_bos.mp3");
        Save();return "Enemy catalog, three waves, boss, pickups, audio and HUD connected. No debug keyboard spawner attached.";
    }

    public static string Minimap()
    {
        SceneManager.SetActiveScene(GameScene);
        var player=Systems.GetComponentInChildren<FpsController>(true);
        var cv=Child("Minimap Canvas",Systems.transform);var canvas=cv.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
        var sc=cv.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1920,1080);sc.matchWidthOrHeight=.5f;
        var holder=new GameObject("Map Holder",typeof(RectTransform));holder.transform.SetParent(cv.transform,false);
        var hr=(RectTransform)holder.transform;hr.anchorMin=Vector2.zero;hr.anchorMax=Vector2.one;hr.offsetMin=hr.offsetMax=Vector2.zero;
        var dimGo=new GameObject("Dim",typeof(RectTransform),typeof(Image));dimGo.transform.SetParent(holder.transform,false);
        var dr=(RectTransform)dimGo.transform;dr.anchorMin=Vector2.zero;dr.anchorMax=Vector2.one;dr.offsetMin=dr.offsetMax=Vector2.zero;
        var dim=dimGo.GetComponent<Image>();dim.color=Color.clear;dim.raycastTarget=false;
        var mapInst=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(Radar+"/Prefab/Map_Status.prefab"),holder.transform);
        var camInst=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(Radar+"/Prefab/Cam_Map.prefab"),GameScene);
        camInst.name="Cam_Map";camInst.transform.SetParent(Systems.transform,false);
        var ms=mapInst.GetComponentInChildren<Custom_MP>(true);var mp=camInst.GetComponent<MP>();var cam=camInst.GetComponent<Camera>();
        ms.Canvas_Control=canvas;ms.Cam_Player=player.playerCamera;ms.WFT=null;
        mp.MS=ms;mp.Player_RY=player.transform;mp.Cam_Map=cam;
        mp.LayerSelect=~((1<<8)|(1<<9)|(1<<12)|(1<<14)|(1<<2)|(1<<4)|(1<<5));
        mp.CamFixed=1;mp.CamSize=27;mp.Aux_ClipPlane_Far=100;mp.Min_Dis_Icon=24;mp.MMSize=.9f;mp.PosyMeLocal=new Vector3(0,40,0);mp.Ray_On=0;
        var rt=Asset<RenderTexture>(Radar+"/Textures/MiniMap.renderTexture");
        rt.Release();rt.width=1024;rt.height=1024;rt.depth=24;rt.antiAliasing=1;EditorUtility.SetDirty(rt);
        cam.targetTexture=rt;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.allowHDR=false;cam.allowMSAA=false;cam.useOcclusionCulling=false;cam.depth=-10;cam.orthographic=true;
        var cd=camInst.GetComponent<UniversalAdditionalCameraData>()??camInst.AddComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing=false;cd.renderShadows=false;cd.antialiasing=AntialiasingMode.None;cd.volumeLayerMask=0;cd.requiresColorOption=CameraOverrideOption.Off;cd.requiresDepthOption=CameraOverrideOption.Off;
        typeof(MP).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.Invoke(mp,null);
        var ctl=cv.AddComponent<MinimapController>();ctl.mp=mp;ctl.ms=ms;ctl.holder=hr;ctl.map=(RectTransform)mapInst.transform;ctl.mapCam=cam;ctl.player=player.transform;ctl.dim=dim;
        ctl.mapCenter=new Vector3(2,0,0);ctl.largeCamSize=100;ctl.smallCamSize=27;ctl.sunBoost=2.4f;
        var gen=Systems.GetComponentInChildren<GeneratorHealth>(true);
        var blip=gen.gameObject.AddComponent<MinimapBlip>();blip.icon=Asset<Sprite>(Gameplay+"/UI/mm_gen.png");blip.color=new Color(.35f,.9f,1);blip.sizeAdd=.5f;
        blip.arrowSprite=Asset<Sprite>(Radar+"/Icons/Arrows/Seta4.png");blip.iconPrefab=Asset<GameObject>(Radar+"/Prefab/Icon.prefab");blip.arrowPrefab=Asset<GameObject>(Radar+"/Prefab/Arrow_Icon.prefab");blip.arrowIconPrefab=Asset<GameObject>(Radar+"/Prefab/Icon_Arrow.prefab");
        Save();return "Minimap connected to player and objective; large map covers industrial grounds, M to toggle.";
    }
}
