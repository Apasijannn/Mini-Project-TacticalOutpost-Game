using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Outpost;
using Outpost.Player;
using Outpost.Enemy;
using Outpost.Env;

public static class ImproveCombatNavigation
{
    const string Root = "Assets/TacticalOutpost/Gameplay";
    const string ScenePath = "Assets/Scenes/Gameplay/TacticalOutpost_Game.unity";
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var session=scene.GetRootGameObjects().Single(g=>g.name=="Industrial Game Session").GetComponent<IndustrialGameSession>();
        if(session.additionalNavigation!=null&&session.additionalNavigation.Length>0)throw new InvalidOperationException("Boss navigation already authored.");
        var player=session.systems.GetComponentInChildren<FpsController>(true);
        foreach(var w in player.GetComponentsInChildren<FpsWeapon>(true))
        {
            w.recoilResponse=w.kind==WeaponKind.Pistol?48:38;
            w.recoilRecovery=w.kind==WeaponKind.Pistol?9:7;
            w.recoilRecoveryDelay=w.kind==WeaponKind.Pistol?.055f:.08f;
            w.burstResetDelay=w.kind==WeaponKind.Pistol?.22f:.28f;
            w.maxRecoilPitch=w.kind==WeaponKind.Pistol?7.5f:10;
            w.maxRecoilYaw=w.kind==WeaponKind.Pistol?2f:3;
            w.recoilRandomness=.12f;
            w.recoilPattern=w.kind==WeaponKind.Pistol?
                new[]{new Vector2(.15f,1),new Vector2(-.3f,1.05f),new Vector2(.3f,1.1f),new Vector2(-.1f,1.05f)}:
                new[]{new Vector2(0,1),new Vector2(-.25f,1.08f),new Vector2(.35f,1.15f),new Vector2(.7f,1.2f),new Vector2(.5f,1.25f),new Vector2(-.6f,1.25f),new Vector2(-.8f,1.3f),new Vector2(-.3f,1.3f)};
            PrefabUtility.RecordPrefabInstancePropertyModifications(w);
        }
        bool saved;
        PrefabUtility.SaveAsPrefabAssetAndConnect(player.gameObject,Root+"/Prefabs/Adapted/Player_Industrial.prefab",InteractionMode.AutomatedAction,out saved);
        if(!saved)throw new IOException("Player variant save failed.");

        var settingsAsset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
        int bossType=-1;
        for(int i=0;i<NavMesh.GetSettingsCount();i++){var setting=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(setting.agentTypeID)=="Industrial Boss")bossType=setting.agentTypeID;}
        if(bossType==-1)bossType=NavMesh.CreateSettings().agentTypeID;
        var so=new SerializedObject(settingsAsset);so.Update();var settings=so.FindProperty("m_Settings");var names=so.FindProperty("m_SettingNames");
        for(int i=0;i<settings.arraySize;i++)
        {
            var item=settings.GetArrayElementAtIndex(i);
            if(item.FindPropertyRelative("agentTypeID").intValue!=bossType)continue;
            item.FindPropertyRelative("agentRadius").floatValue=.8f;item.FindPropertyRelative("agentHeight").floatValue=3.8f;
            item.FindPropertyRelative("agentClimb").floatValue=.5f;item.FindPropertyRelative("agentSlope").floatValue=45;
            while(names.arraySize<=i)names.InsertArrayElementAtIndex(names.arraySize);
            names.GetArrayElementAtIndex(i).stringValue="Industrial Boss";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var bossSettings=NavMesh.GetSettingsByID(bossType);
        if(Mathf.Abs(bossSettings.agentHeight-3.8f)>.01f)throw new InvalidOperationException("Boss agent settings not applied.");

        Folder(Root+"/Enemies/Adapted");Folder(Root+"/Enemies/Adapted/Configs");
        string catalogPath=Root+"/Enemies/Adapted/EnemyCatalog_Industrial.asset";
        if(!AssetDatabase.CopyAsset(Root+"/Enemies/EnemyCatalog.asset",catalogPath))throw new IOException("Catalog copy failed.");
        var catalog=AssetDatabase.LoadAssetAtPath<EnemyCatalog>(catalogPath);
        foreach(var v in catalog.variants)
        {
            var original=v.prefab.GetComponent<EnemyHealth>().config;
            if(original.kind!=EnemyKind.Boss)continue;
            string configPath=Root+"/Enemies/Adapted/Configs/"+v.id+".asset";
            var config=UnityEngine.Object.Instantiate(original);config.name=original.name+" Industrial";config.visualScale=2;
            AssetDatabase.CreateAsset(config,configPath);
            var contents=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(v.prefab));
            try
            {
                float ratio=2f/original.visualScale;
                contents.transform.localScale=Vector3.one;
                var model=contents.transform.Find("Model");if(!model)throw new InvalidOperationException("Boss Model child missing.");
                model.localScale*=ratio;model.localPosition*=ratio;
                foreach(var collider in contents.GetComponents<CapsuleCollider>()){collider.radius*=ratio;collider.height*=ratio;collider.center*=ratio;}
                var agent=contents.GetComponent<NavMeshAgent>();agent.agentTypeID=bossType;agent.radius=.75f;agent.height=3.7f;agent.baseOffset=0;
                contents.GetComponent<EnemyHealth>().config=config;contents.GetComponent<EnemyBrain>().config=config;
                v.prefab=PrefabUtility.SaveAsPrefabAsset(contents,Root+"/Enemies/Adapted/"+v.id+".prefab",out saved);
                if(!saved)throw new IOException("Boss prefab save failed: "+v.id);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        EditorUtility.SetDirty(catalog);session.systems.GetComponentInChildren<WaveDirector>(true).catalog=catalog;

        var navObject=new GameObject("Boss Navigation - 3.8m clearance");SceneManager.MoveGameObjectToScene(navObject,scene);navObject.transform.SetParent(session.systems.transform,false);
        var nav=navObject.AddComponent<NavMeshSurface>();nav.agentTypeID=bossType;nav.collectObjects=CollectObjects.Volume;
        nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.layerMask=session.navigation.layerMask;
        nav.center=session.navigation.center;nav.size=session.navigation.size;nav.overrideVoxelSize=true;nav.voxelSize=.16f;
        nav.BuildNavMesh();if(!nav.navMeshData)throw new InvalidOperationException("Boss navigation bake failed.");
        AssetDatabase.CreateAsset(nav.navMeshData,Root+"/Navigation/IndustrialBoss.asset");nav.RemoveData();nav.enabled=false;
        session.additionalNavigation=new[]{nav};
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Gameplay scene save failed.");
        AssetDatabase.SaveAssets();return ValidateNavigation();
    }

    public static string ValidateNavigation()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);var session=scene.GetRootGameObjects().Single(g=>g.name=="Industrial Game Session").GetComponent<IndustrialGameSession>();
        var env=SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene).GetRootGameObjects().Single().GetComponent<NavMeshSurface>();
        env.RemoveData();session.navigation.AddData();foreach(var nav in session.additionalNavigation)nav.AddData();
        var records=new List<object>();int failures=0;
        try
        {
            var catalog=session.systems.GetComponentInChildren<WaveDirector>(true).catalog;
            var gen=session.systems.GetComponentInChildren<EnvGeneratorSlot>(true);
            var spawns=session.systems.GetComponentsInChildren<EnvSpawnPoint>(true);
            foreach(int type in catalog.variants.Select(v=>v.prefab.GetComponent<NavMeshAgent>().agentTypeID).Distinct())
            {
                var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
                foreach(var spawn in spawns)
                {
                    int reachable=0;var path=new NavMeshPath();
                    if(NavMesh.SamplePosition(spawn.transform.position,out NavMeshHit start,1.5f,filter))
                        foreach(var attack in gen.attackPoints)
                            if(NavMesh.SamplePosition(attack.position,out NavMeshHit end,1.5f,filter)&&Mathf.Abs(end.position.y-attack.position.y)<1.2f&&NavMesh.CalculatePath(start.position,end.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete)reachable++;
                    if(reachable!=gen.attackPoints.Length)failures++;
                    records.Add(new{agent=NavMesh.GetSettingsNameFromID(type),spawn=spawn.name,reachableAttackPoints=reachable});
                }
            }
            File.WriteAllText("Docs/Integration/NavigationByAgent.json",JsonConvert.SerializeObject(new{failures,checks=records},Formatting.Indented));
            if(failures>0)throw new InvalidOperationException("Navigation requires refinement: "+failures+" spawn/agent combinations.");
            return "Recoil configured; three bosses normalized; "+records.Count+" spawn/agent checks reach every attack point.";
        }
        finally{session.navigation.RemoveData();foreach(var nav in session.additionalNavigation)nav.RemoveData();env.AddData();}
    }

    public static string EnlargeBoss()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath(ScenePath);
        var session=scene.GetRootGameObjects().Single(g=>g.name=="Industrial Game Session").GetComponent<IndustrialGameSession>();
        var director=session.systems.GetComponentInChildren<WaveDirector>(true);
        director.bossScale=2.3f;
        foreach(var v in director.catalog.variants)
        {
            var config=v.prefab.GetComponent<EnemyHealth>().config;
            if(config.kind!=EnemyKind.Boss)continue;
            float ratio=2.3f/config.visualScale;
            var contents=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(v.prefab));
            try
            {
                var model=contents.transform.Find("Model");model.localScale*=ratio;model.localPosition*=ratio;
                foreach(var collider in contents.GetComponents<CapsuleCollider>()){collider.radius*=ratio;collider.height*=ratio;collider.center*=ratio;}
                var agent=contents.GetComponent<NavMeshAgent>();agent.radius=.87f;agent.height=4.3f;
                bool saved;PrefabUtility.SaveAsPrefabAsset(contents,AssetDatabase.GetAssetPath(v.prefab),out saved);
                if(!saved)throw new IOException("Boss resize save failed.");
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            config.visualScale=2.3f;EditorUtility.SetDirty(config);
        }
        var surface=session.additionalNavigation.Single();
        var asset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
        var settings=new SerializedObject(asset);var array=settings.FindProperty("m_Settings");
        for(int i=0;i<array.arraySize;i++)
        {
            var item=array.GetArrayElementAtIndex(i);
            if(item.FindPropertyRelative("agentTypeID").intValue!=surface.agentTypeID)continue;
            item.FindPropertyRelative("agentRadius").floatValue=.9f;item.FindPropertyRelative("agentHeight").floatValue=4.4f;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        var savedData=surface.navMeshData;surface.BuildNavMesh();var newData=surface.navMeshData;
        if(!newData)throw new InvalidOperationException("Boss navigation bake failed.");
        surface.RemoveData();EditorUtility.CopySerialized(newData,savedData);surface.navMeshData=savedData;
        UnityEngine.Object.DestroyImmediate(newData);EditorUtility.SetDirty(savedData);
        surface.name="Boss Navigation - 4.4m clearance";
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return ValidateNavigation();
    }

    static void Rebuild(NavMeshSurface surface)
    {
        var saved=surface.navMeshData;surface.BuildNavMesh();var built=surface.navMeshData;
        if(!built)throw new InvalidOperationException("Navigation bake failed: "+surface.name);
        surface.RemoveData();EditorUtility.CopySerialized(built,saved);surface.navMeshData=saved;
        UnityEngine.Object.DestroyImmediate(built);EditorUtility.SetDirty(saved);
    }

    public static string SpawnAndRebake()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath(ScenePath);
        var session=scene.GetRootGameObjects().Single(g=>g.name=="Industrial Game Session").GetComponent<IndustrialGameSession>();
        var environment=SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
        var env=environment.GetRootGameObjects().Single().transform;
        var original=env.GetComponent<NavMeshSurface>();original.RemoveData();
        try
        {
            Rebuild(session.navigation);foreach(var surface in session.additionalNavigation)Rebuild(surface);
            session.navigation.AddData();
            var towers=env.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="NW perimeter guard tower - native scale (4)"||t.name=="NW perimeter guard tower - native scale (5)").ToArray();
            if(towers.Length!=2)throw new InvalidOperationException("Generator tower pair missing.");
            float x=towers.Average(t=>t.position.x);var desired=new Vector3(x,.4f,-8.0f);
            var filter=new NavMeshQueryFilter{agentTypeID=session.navigation.agentTypeID,areaMask=NavMesh.AllAreas};
            if(!NavMesh.SamplePosition(desired,out NavMeshHit hit,2f,filter))throw new InvalidOperationException("Generator road spawn unavailable.");
            var position=hit.position+Vector3.up*.08f;
            int mask=~((1<<8)|(1<<9)|(1<<12)|(1<<14)|(1<<2)|(1<<5));
            if(Physics.CheckCapsule(position+Vector3.up*.4f,position+Vector3.up*1.45f,.34f,mask,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Player spawn overlaps an obstacle.");
            var player=session.systems.GetComponentInChildren<FpsController>(true);
            var generator=session.systems.GetComponentInChildren<GeneratorHealth>(true);
            var direction=generator.transform.position-position;direction.y=0;
            Undo.RecordObject(player.transform,"Player spawn facing generator");player.transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction));
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            var marker=env.Find("GameplayMarkers/PlayerStart");Undo.RecordObject(marker,"Player spawn marker");marker.SetPositionAndRotation(position,player.transform.rotation);
            session.systems.GetComponentInChildren<WaveDirector>(true).waveInterval=60;
            File.WriteAllText("Docs/Integration/PlayerSpawn.json",JsonConvert.SerializeObject(new{position=new[]{position.x,position.y,position.z},yaw=player.transform.eulerAngles.y,location="Road in front of generator, between the two front towers"},Formatting.Indented));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            EditorSceneManager.MarkSceneDirty(environment);EditorSceneManager.SaveScene(environment);AssetDatabase.SaveAssets();
        }
        finally{session.navigation.RemoveData();foreach(var surface in session.additionalNavigation)surface.RemoveData();original.AddData();}
        return ValidateNavigation();
    }
}
