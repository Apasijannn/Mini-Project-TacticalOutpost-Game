using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GeneratorRedLights
{
    public static string Add()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity")throw new InvalidOperationException("Open map in Edit Mode.");
        var generator=Resources.FindObjectsOfTypeAll<GameObject>().Single(g=>g.scene.IsValid()&&g.scene.isLoaded&&g.name=="076 • Generator_v1 (1)"&&Mathf.Abs(g.transform.position.x+5.34f)<.05f);
        if(generator.transform.Find("Generator red security floodlights"))throw new InvalidOperationException("Generator already has lights.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;
        var oldScene=generator.scene;
        var p=generator.transform.position;var q=generator.transform.rotation;var scale=generator.transform.lossyScale;
        var b=generator.GetComponent<Renderer>().bounds;
        var backup="C:/Users/Faiz/OneDrive/Documents/TacticalOutpostBackups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_GeneratorRedLights";
        Directory.CreateDirectory(backup);
        if(!EditorSceneManager.SaveScene(scene,backup+"/Before_Map.unity",true)||!EditorSceneManager.SaveScene(oldScene,backup+"/Before_Gallery.unity",true))throw new IOException("Could not back up live scenes.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab");
        if(!prefab)throw new InvalidOperationException("Missing floodlight prefab.");
        const string matPath="Assets/TacticalOutpost/Materials/GeneratorFloodlight_RedLens.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Generator red floodlight lens"};mat.SetColor("_BaseColor",new Color(3,.025f,.01f));AssetDatabase.CreateAsset(mat,matPath);}
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Generator red perimeter lights");
        try{
            if(oldScene!=scene){Undo.SetTransformParent(generator.transform,null,"Keep generator world pose");Undo.MoveGameObjectToScene(generator,scene,"Store generator in map");Undo.SetTransformParent(generator.transform,root,"Map generator");}
            var group=new GameObject("Generator red security floodlights");SceneManager.MoveGameObjectToScene(group,scene);Undo.RegisterCreatedObjectUndo(group,"Generator lights");group.transform.SetParent(generator.transform,true);
            int count=0;
            foreach(var v in new[]{new Vector3(-.27f,0,1),new Vector3(.27f,0,1),new Vector3(-.27f,0,-1),new Vector3(.27f,0,-1),new Vector3(-1,0,0),new Vector3(1,0,0)}){
                var dir=new Vector3(Mathf.Abs(v.x)==1?v.x:0,0,v.z);
                var origin=new Vector3(b.center.x+b.size.x*v.x,b.max.y-1.35f,b.center.z);
                if(v.z!=0)origin.z=b.center.z+v.z*(b.extents.z+3);else origin.x=b.center.x+v.x*(b.extents.x+3);
                if(!generator.GetComponent<Collider>().Raycast(new Ray(origin,-dir),out var hit,8))throw new InvalidOperationException("Could not locate generator wall.");
                var lamp=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(lamp,"Red wall floodlight");
                lamp.transform.SetPositionAndRotation(hit.point+dir*.16f,Quaternion.LookRotation(dir,Vector3.up));lamp.transform.SetParent(group.transform,true);lamp.name="Generator red floodlight "+(++count);
                foreach(var renderer in lamp.GetComponentsInChildren<Renderer>()){renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&m.name=="TowerFloodlight_Bulb"?mat:m).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                var light=lamp.GetComponentInChildren<Light>(true);light.color=new Color(1,.025f,.01f);light.intensity=300;light.range=40;light.spotAngle=120;light.innerSpotAngle=95;light.enabled=OutpostLightingToggle.IsNight();light.transform.parent.localRotation=Quaternion.Euler(65,0,0);PrefabUtility.RecordPrefabInstancePropertyModifications(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform.parent);PrefabUtility.RecordPrefabInstancePropertyModifications(lamp.transform);
                if(Vector3.Dot(light.transform.forward,Vector3.down)<.8f||Vector3.Distance(lamp.transform.lossyScale,Vector3.one)>.001f)throw new InvalidOperationException("Incorrect floodlight direction or size.");
            }
            if(count!=6||Vector3.Distance(generator.transform.position,p)>.001f||Quaternion.Angle(generator.transform.rotation,q)>.001f||Vector3.Distance(generator.transform.lossyScale,scale)>.001f)throw new InvalidOperationException("Generator pose changed.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene)||oldScene!=scene&&!EditorSceneManager.SaveScene(oldScene))throw new IOException("Could not save scenes.");
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText(backup+"/Status.txt","Six red wall floodlights installed on the large generator. Generator moved from gallery into map with identical world pose and scale. Lights follow generator and existing Pagi/Malam toggle. Intensity 300, range 40, outer 120, inner 95, pitch 65. Current time of day preserved. No Play Mode or FPS check.");
            return backup;
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}

