using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WidenBarrelFire
{
    static void Tune(Transform fire,bool live)
    {
        foreach(var ps in fire.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name=="Flames"||p.name=="Flames Secondary")){
            if(live)Undo.RecordObject(ps,"Fuller barrel flame base");
            float radius=ps.name=="Flames"?.22f:.20f;
            var shape=ps.shape;shape.radius=radius;shape.scale=new Vector3(1,1,.1f/radius);
            var emission=ps.emission;emission.rateOverTime=ps.name=="Flames"?20:18;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            if(shape.radius>.25f||Mathf.Abs(shape.radius*shape.scale.z-.1f)>.001f)throw new InvalidOperationException("Flame emitter exceeds barrel mouth or changes vertical spread.");
        }
    }
    public static string Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity")throw new InvalidOperationException("Open map in Edit Mode.");
        var live=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Free Fire URP - barrel flame").ToArray();
        if(live.Length==0)throw new InvalidOperationException("No barrel flames found.");
        var backup="C:/Users/Faiz/OneDrive/Documents/TacticalOutpostBackups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_BarrelDiameterFire";
        Directory.CreateDirectory(backup);
        if(!EditorSceneManager.SaveScene(scene,backup+"/Before_Map.unity",true))throw new IOException("Backup failed.");
        var poses=live.Select(t=>(t,t.localPosition,height:t.localScale.y)).ToArray();
        var paths=new[]{"Assets/TacticalOutpost/Prefabs/BarrelFire_Vefects.prefab","Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_Custom.prefab","Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_Military.prefab","Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_Biohazard.prefab"};
        foreach(var path in paths){
            File.Copy(path,backup+"/"+Path.GetFileName(path),true);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{var t=path==paths[0]?root.transform:root.GetComponentsInChildren<Transform>(true).Single(o=>o.name=="Free Fire URP - barrel flame");t.localScale=new Vector3(1,t.localScale.y,1);Tune(t,false);PrefabUtility.RecordPrefabInstancePropertyModifications(t);bool saved;PrefabUtility.SaveAsPrefabAsset(root,path,out saved);if(!saved)throw new IOException("Prefab save failed.");}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Widen fire to barrel mouth");
        foreach(var entry in poses){Undo.RecordObject(entry.t,"Flame width");entry.t.localScale=new Vector3(1,entry.height,1);Tune(entry.t,true);PrefabUtility.RecordPrefabInstancePropertyModifications(entry.t);if(entry.t.localPosition!=entry.localPosition||entry.t.localScale.y!=entry.height)throw new InvalidOperationException("Flame base or height changed.");}
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        return live.Length+" barrel flames filled across .40-.44 m emitter diameter, emission 20/18, height and rim anchors preserved; saved.";
    }
}
