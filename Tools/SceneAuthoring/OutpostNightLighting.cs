using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OutpostNightLighting
{
    public static string ReplaceWithModernLamps()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;
        var old=root.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/StreetLamp1_")).ToArray();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace poles with modern flat-head lamps");
        try{foreach(var t in old){
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Replace("/StreetLamp1_","/StreetLamp2_");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException("Missing modern counterpart: "+path);
            var oldRenderer=t.GetComponentInChildren<Renderer>();float baseY=oldRenderer.bounds.min.y;
            var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);Undo.RegisterCreatedObjectUndo(g,"Modern street lamp");g.name=t.name;g.transform.SetParent(t.parent,false);g.transform.SetPositionAndRotation(t.position,t.rotation);g.transform.localScale=Vector3.one;g.transform.SetSiblingIndex(t.GetSiblingIndex());
            var renderer=g.GetComponentInChildren<Renderer>();g.transform.position+=Vector3.up*(baseY-renderer.bounds.min.y);
            var capsule=g.GetComponent<CapsuleCollider>();capsule.height=renderer.bounds.max.y-g.transform.position.y;capsule.center=new Vector3(capsule.center.x,capsule.height*.5f,capsule.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
            foreach(var light in g.GetComponentsInChildren<Light>()){light.color=new Color(1,.87f,.7f);if(light.type==LightType.Spot){light.intensity=45;light.range=20;light.spotAngle=115;light.innerSpotAngle=65;light.shadows=t.name.StartsWith("Double entrance")?LightShadows.Soft:LightShadows.None;}else{light.intensity=.6f;light.range=1.2f;}PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
            if(Mathf.Abs(renderer.bounds.min.y-baseY)>.002f||renderer.sharedMaterials.Any(m=>!m||!m.shader.isSupported))throw new InvalidOperationException("Modern lamp base or material mismatch");
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);Undo.DestroyObjectImmediate(t.gameObject);
        }
        if(root.GetComponentsInChildren<Transform>(true).Any(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/StreetLamp1_")))throw new InvalidOperationException("Old lamp model remains");
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);return "Replaced "+old.Length+" poles with StreetLamp2, native scale; road positions and intensity preserved.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
    public static string PairRoadLamps()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;
        var group=root.Find("Exterior infrastructure - SpaceZeta lamps and guard tower/Native concrete street lamps");
        var terrain=root.Find("World").GetComponentInChildren<Terrain>();var paving=root.Find("RoadsAndDecks").GetComponentsInChildren<Collider>();
        var obstacles=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!(c is TerrainCollider)&&!c.transform.IsChildOf(group)&&!c.transform.IsChildOf(root.Find("World"))&&!c.transform.IsChildOf(root.Find("RoadsAndDecks"))).ToArray();
        float Ground(Vector3 p){float y=terrain.SampleHeight(p)+terrain.transform.position.y;foreach(var c in paving)if(c.Raycast(new Ray(new Vector3(p.x,30,p.z),Vector3.down),out var hit,60))y=Mathf.Max(y,hit.point.y);return y;}
        Vector3 Opposite(Transform t,out float yaw){var p=t.position;if(Mathf.Abs(p.z-.20986f)<.05f||Mathf.Abs(p.z+8.69014f)<.05f){p.z=-8.48028f-p.z;yaw=p.z> -4.24014f?180:0;}else if(Mathf.Abs(p.x+1.84518f)<.05f||Mathf.Abs(p.x-7.05482f)<.05f){p.x=5.20964f-p.x;yaw=p.x>2.60482f?270:90;}else if(Mathf.Abs(p.x-15.17226f)<.05f||Mathf.Abs(p.x-24.07226f)<.05f){p.x=39.24452f-p.x;yaw=p.x>19.62226f?270:90;}else throw new InvalidOperationException("Inspect off-lane pole: "+t.name);p.y=Ground(p)-.015f;return p;}
        bool Near(Transform t,Vector3 p)=>Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(p.x,p.z))<.05f;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Pair lamps across road");int added=0;
        try {
            foreach(var original in group.Cast<Transform>().Where(t=>t.name.StartsWith("Roadside lamp ")).ToArray()){
                var position=Opposite(original,out var yaw);if(group.Cast<Transform>().Any(t=>Near(t,position)))continue;
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject));
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);Undo.RegisterCreatedObjectUndo(g,"Opposite road lamp");g.name="Opposite - "+original.name;g.transform.SetParent(group,false);g.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
                var renderer=g.GetComponentInChildren<Renderer>();renderer.sharedMaterials=original.GetComponentInChildren<Renderer>().sharedMaterials;
                var bounds=renderer.bounds;var capsule=g.GetComponent<CapsuleCollider>();capsule.height=bounds.max.y-position.y;capsule.center=new Vector3(capsule.center.x,capsule.height*.5f,capsule.center.z);
                var head=g.AddComponent<BoxCollider>();var center=g.transform.InverseTransformPoint(bounds.center);center.y=capsule.height-.5f;head.center=center;var size=Quaternion.Inverse(g.transform.rotation)*bounds.size;head.size=new Vector3(Mathf.Abs(size.x),1,Mathf.Abs(size.z));
                var clashes=obstacles.Where(c=>c.bounds.Intersects(bounds)&&(Physics.ComputePenetration(capsule,position,g.transform.rotation,c,c.transform.position,c.transform.rotation,out var d,out var depth)&&depth>.005f||Physics.ComputePenetration(head,position,g.transform.rotation,c,c.transform.position,c.transform.rotation,out d,out depth)&&depth>.005f)).Select(c=>c.name).ToArray();UnityEngine.Object.DestroyImmediate(head);
                if(clashes.Length>0)throw new InvalidOperationException(g.name+" overlaps: "+string.Join(", ",clashes));
                foreach(var light in g.GetComponentsInChildren<Light>()){light.color=new Color(1,.87f,.7f);light.shadows=LightShadows.None;if(light.type==LightType.Spot){light.intensity=45;light.range=20;light.spotAngle=115;light.innerSpotAngle=65;}else{light.intensity=.6f;light.range=1.2f;}PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
                PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                Undo.RecordObject(original,"Face paired lamp toward road");original.rotation=Quaternion.Euler(0,(yaw+180)%360,0);PrefabUtility.RecordPrefabInstancePropertyModifications(original);added++;
            }
            foreach(var original in group.Cast<Transform>().Where(t=>t.name.StartsWith("Roadside lamp "))){var p=Opposite(original,out var yaw);if(!group.Cast<Transform>().Any(t=>Near(t,p)))throw new InvalidOperationException("Missing opposite pole: "+original.name);}
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);return "Added "+added+" opposing poles; total "+group.childCount+". Every roadside station now has a pole across the road.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
    public static string Apply()=>OutpostNightPreset.Apply();
}
