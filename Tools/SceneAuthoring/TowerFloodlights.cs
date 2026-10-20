using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class TowerFloodlights
{
    const string HeadPath="Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab";
    static Bounds BoundsOf(GameObject g){var rr=g.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Material Material(string name,string shader,Color color){string path="Assets/TacticalOutpost/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find(shader)){name=name};m.SetColor("_BaseColor",color);if(m.HasProperty("_Metallic")){m.SetFloat("_Metallic",.65f);m.SetFloat("_Smoothness",.35f);}AssetDatabase.CreateAsset(m,path);}return m;}
    public static string BuildHead()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        try{
            var source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Industrial Props/Prefabs/WorkLight.prefab"),preview);PrefabUtility.UnpackPrefabInstance(source,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var head=source.transform.Find("WorkLight/BezierCurve.014");if(!head||head.GetComponentsInChildren<MeshRenderer>().Length!=10)throw new InvalidOperationException("Inspect the artist's single head hierarchy before extraction.");
            var black=Material("TowerFloodlight_Black","Universal Render Pipeline/Lit",new Color(.162f,.162f,.162f));var bulb=Material("TowerFloodlight_Bulb","Universal Render Pipeline/Unlit",new Color(2,1.9f,1.7f));
            var root=new GameObject("Tower single floodlight - bracket mount");SceneManager.MoveGameObjectToScene(root,preview);var tilt=new GameObject("Lamp tilt - 55 degrees");SceneManager.MoveGameObjectToScene(tilt,preview);tilt.transform.SetParent(root.transform,false);
            var visual=new GameObject("One artist lamp head and U bracket");SceneManager.MoveGameObjectToScene(visual,preview);visual.transform.SetParent(tilt.transform,false);var b=BoundsOf(head.gameObject);head.SetParent(visual.transform,true);head.position-=new Vector3(b.center.x,b.min.y,b.center.z);visual.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var r in head.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m.name=="UnlightWhite"?bulb:black).ToArray();
            b=BoundsOf(visual);var lens=head.GetComponentsInChildren<Renderer>().Single(r=>r.sharedMaterials.Contains(bulb)).bounds;var lightObject=new GameObject("Outward flood beam");SceneManager.MoveGameObjectToScene(lightObject,preview);lightObject.transform.SetParent(tilt.transform,false);lightObject.transform.localPosition=new Vector3(lens.center.x,lens.center.y,b.max.z+.025f);var light=lightObject.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(1,.95f,.85f);light.intensity=100;light.range=35;light.spotAngle=95;light.innerSpotAngle=70;light.shadows=LightShadows.None;light.enabled=false;
            foreach(var part in new[]{("Short mounting arm",new Vector3(0,-.025f,-.055f),new Vector3(.065f,.055f,.20f)),("Mounting plate",new Vector3(0,-.055f,-.14f),new Vector3(.17f,.12f,.035f))}){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(g,preview);g.name=part.Item1;g.transform.SetParent(root.transform,false);g.transform.localPosition=part.Item2;g.transform.localScale=part.Item3;g.GetComponent<Renderer>().sharedMaterial=black;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
            tilt.transform.localRotation=Quaternion.Euler(55,0,0);UnityEngine.Object.DestroyImmediate(source);
            if(root.GetComponentsInChildren<Renderer>().Length!=12||BoundsOf(root).size.magnitude>1||Vector3.Dot(light.transform.forward,Vector3.down)<.8f)throw new InvalidOperationException("Single lamp geometry or downward beam is incorrect.");
            bool saved;PrefabUtility.SaveAsPrefabAsset(root,HeadPath,out saved);if(!saved)throw new InvalidOperationException("Could not save one-head floodlight prefab");AssetDatabase.SaveAssets();return "One native-sized lamp head plus U bracket extracted; tripod and second head absent; short mount and 55-degree beam ready.";
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Attach()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;var towers=root.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith("Assets/Tower/")).ToArray();
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(HeadPath);if(!asset||towers.Length==0)throw new InvalidOperationException("Build the single head first and inspect tower placement");
        var before=towers.Select(t=>(t,t.position,t.rotation,t.localScale)).ToArray();var manifest=new List<object>();Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Attach corner floodlights without moving towers");
        try{foreach(var tower in towers){
            if(tower.Find("Corner floodlights - four single heads"))throw new InvalidOperationException("Tower already fitted: "+tower.name);
            var right=Vector3.ProjectOnPlane(tower.TransformDirection(Vector3.right),Vector3.up).normalized;var rotation=Quaternion.LookRotation(Vector3.Cross(right,Vector3.up),Vector3.up);var inverse=Matrix4x4.TRS(tower.position,rotation,Vector3.one).inverse;var vertices=new List<Vector3>();
            foreach(var filter in tower.GetComponentsInChildren<MeshFilter>()){
                if(!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/Tower/"))continue;
                using(var data=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh)){var positions=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);try{data[0].GetVertices(positions);foreach(var v in positions)vertices.Add(inverse.MultiplyPoint3x4(filter.transform.TransformPoint(v)));}finally{positions.Dispose();}}
            }
            float top=vertices.Max(v=>v.y);var roof=vertices.Where(v=>v.y>top-.12f).ToArray();float x0=roof.Min(v=>v.x),x1=roof.Max(v=>v.x),z0=roof.Min(v=>v.z),z1=roof.Max(v=>v.z);
            if(x1-x0<1||x1-x0>6||z1-z0<1||z1-z0>6)throw new InvalidOperationException("Unexpected roof geometry: "+tower.name);
            var group=new GameObject("Corner floodlights - four single heads");SceneManager.MoveGameObjectToScene(group,scene);group.transform.SetParent(tower,false);Undo.RegisterCreatedObjectUndo(group,"Tower corner lights");var frame=Matrix4x4.TRS(tower.position,rotation,Vector3.one);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1}){
                var outward=rotation*new Vector3(sx,0,sz).normalized;var p=frame.MultiplyPoint3x4(new Vector3(sx<0?x0:x1,top-.035f,sz<0?z0:z1))+outward*.045f;
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);g.transform.SetParent(group.transform,true);g.transform.SetPositionAndRotation(p,Quaternion.LookRotation(outward,Vector3.up));g.name="Corner "+sx+","+sz+" - 55 degree downlight";PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
                var light=g.GetComponentInChildren<Light>();if(Vector3.Dot(Vector3.ProjectOnPlane(light.transform.forward,Vector3.up).normalized,outward)<.99f||Mathf.Abs(Mathf.Asin(-light.transform.forward.y)*Mathf.Rad2Deg-55)>.1f)throw new InvalidOperationException("Corner light faces incorrectly");
                manifest.Add(new{tower=tower.name,lamp=g.name,position=new[]{p.x,p.y,p.z},pitch=55});
            }
        }
        foreach(var entry in before)if(entry.t.position!=entry.position||Quaternion.Angle(entry.t.rotation,entry.rotation)>.0001f||entry.t.localScale!=entry.localScale)throw new InvalidOperationException("Tower transform changed");
        if(manifest.Count!=towers.Length*4)throw new InvalidOperationException("Missing corner lamp");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);File.WriteAllText("Temp/TowerCornerFloodlights.json",Newtonsoft.Json.JsonConvert.SerializeObject(manifest,Newtonsoft.Json.Formatting.Indented));return "Attached "+manifest.Count+" single-head floodlights to "+towers.Length+" towers. Every tower transform is unchanged; floodlights off for Noon.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
