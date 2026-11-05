using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReplaceBarrelFire
{
    const string Source="Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small.prefab";
    const string Fire="Assets/TacticalOutpost/Prefabs/BarrelFire_Vefects.prefab";
    const string Name="Free Fire URP - barrel flame";
    const string Backup="C:/Users/Faiz/OneDrive/Documents/TacticalOutpostBackups/20261020_194859_DustBeamsFire";
    static void Replace(Transform anchor,bool live)
    {
        foreach(var name in new[]{"Flames","Smoke","Embers"}){var old=anchor.Find(name);if(old){if(live)Undo.DestroyObjectImmediate(old.gameObject);else UnityEngine.Object.DestroyImmediate(old.gameObject);}}
        var fx=anchor.Find(Name);
        if(!fx){var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fire),anchor.gameObject.scene);obj.name=Name;obj.transform.SetParent(anchor,false);if(live)Undo.RegisterCreatedObjectUndo(obj,"Replace barrel fire");fx=obj.transform;}
        fx.localPosition=Vector3.down*.18f;PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
        if(fx.GetComponentsInChildren<ParticleSystem>(true).Length<3||Vector3.Distance(fx.localPosition,Vector3.down*.18f)>.001f)throw new InvalidOperationException("Fire not anchored to barrel rim.");
    }
    public static string Masters()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode required.");
        Directory.CreateDirectory(Backup);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);if(!source)throw new InvalidOperationException("Import Free Fire URP first.");
        var preview=EditorSceneManager.NewPreviewScene();
        try{
            var fx=(GameObject)PrefabUtility.InstantiatePrefab(source,preview);fx.name=Name;fx.transform.localScale=new Vector3(1,.72f,1);
            foreach(var audio in fx.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
            foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.playOnAwake=true;var lights=ps.lights;lights.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(ps);}
            foreach(var t in fx.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Distortion"||t.name=="Heat Distortion"))t.gameObject.SetActive(false);
            foreach(var renderer in fx.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterials.All(m=>!m))renderer.enabled=false;
            if(fx.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.sharedMaterials.Any(m=>m&&!m.shader.isSupported)))throw new InvalidOperationException("Unsupported fire material.");
            bool saved;PrefabUtility.SaveAsPrefabAsset(fx,Fire,out saved);if(!saved)throw new IOException("Fire prefab save failed.");
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
        var scene=SceneManager.GetActiveScene();if(!EditorSceneManager.SaveScene(scene,Backup+"/Before_FireReplacement.unity",true))throw new IOException("Scene backup failed.");
        foreach(string variant in new[]{"Custom","Military","Biohazard"}){
            string path="Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_"+variant+".prefab";File.Copy(path,Backup+"/Before_BurningBarrel_"+variant+".prefab",true);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Replace(root.transform.Find("Burning barrel - visual only"),false);bool saved;PrefabUtility.SaveAsPrefabAsset(root,path,out saved);if(!saved)throw new IOException("Barrel master save failed.");}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Three master barrels now use native Small fire, scale 1/.72/1, emitter seated at rim; original glow retained.";
    }
    public static string Scene()
    {
        var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity")throw new InvalidOperationException("Open map in Edit Mode.");
        var anchors=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Burning barrel - visual only").ToArray();
        if(anchors.Length==0)throw new InvalidOperationException("No barrels found.");
        var poses=anchors.Select(t=>(t,t.position,t.rotation,t.lossyScale)).ToArray();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace all barrel flames with Free Fire URP");
        try{
            foreach(var anchor in anchors)Replace(anchor,true);
            foreach(var entry in poses)if(Vector3.Distance(entry.t.position,entry.position)>.001f||Quaternion.Angle(entry.t.rotation,entry.rotation)>.001f||Vector3.Distance(entry.t.lossyScale,entry.lossyScale)>.001f)throw new InvalidOperationException("Barrel pose changed.");
            if(anchors.Any(t=>!t.Find(Name)||t.Find("Flames")||t.Find("Smoke")||t.Find("Embers")))throw new InvalidOperationException("Old flames remain.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new IOException("Map save failed.");Undo.CollapseUndoOperations(undo);
            File.AppendAllText(Backup+"/Status.md","\n\nFree Fire URP acquired with EULA approval, imported and installed on "+anchors.Length+" live barrels and all three master prefabs. Native Small effect scale 1/.72/1, emitter -.18 m relative to original fire anchor; existing warm glow retained; package audio and distortion disabled. Barrel transforms unchanged.");
            return anchors.Length+" barrels replaced and saved; three master prefabs updated.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
