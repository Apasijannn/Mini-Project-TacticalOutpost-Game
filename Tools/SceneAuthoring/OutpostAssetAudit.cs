using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OutpostAssetAudit
{
    public static string Crossroads()
    {
        var root=GameObject.Find("TacticalOutpost").transform;var roads=root.Find("RoadsAndDecks");var crossing=roads.Find("Command four-way crossing");if(!crossing)throw new InvalidOperationException("Crossing not found");
        var center=new Vector3(19.62226f,.18364f,-4.24014f);var failures=new List<string>();int samples=0;
        for(float x=-4.975f;x<5;x+=.25f)for(float z=-4.975f;z<5;z+=.25f){samples++;if(!Physics.RaycastAll(center+new Vector3(x,1,z),Vector3.down,2).Any(h=>h.transform.IsChildOf(roads)&&h.point.y>=center.y-.002f))failures.Add("Missing paving "+x+","+z);}
        var directions=new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left};var arms=new List<object>();
        for(int i=0;i<4;i++){
            bool surface=true;for(float d=0;d<=9;d+=.25f){var p=center+directions[i]*d;if(!Physics.RaycastAll(p+Vector3.up,Vector3.down,2).Any(h=>h.transform.IsChildOf(roads)&&Mathf.Abs(h.point.y-center.y)<.002f))surface=false;}
            bool blocked=Physics.CapsuleCast(center+Vector3.up*.4f,center+Vector3.up*1.45f,.35f,directions[i],9,~0,QueryTriggerInteraction.Ignore);
            UnityEngine.AI.NavMeshHit a,b;bool sampled=UnityEngine.AI.NavMesh.SamplePosition(center,out a,.3f,-1)&UnityEngine.AI.NavMesh.SamplePosition(center+directions[i]*9,out b,.3f,-1);
            UnityEngine.AI.NavMeshHit wall;bool nav=sampled&&!UnityEngine.AI.NavMesh.Raycast(a.position,b.position,out wall,-1);
            arms.Add(new{direction=new[]{"North","East","South","West"}[i],levelAsphalt=surface,capsuleClear=!blocked,directNavMesh=nav});if(!surface||blocked||!nav)failures.Add("Arm "+i);
        }
        string path="Temp/OutpostCrossroadsAudit.json";File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{junctionSamples=samples,arms,failures,limits="0.25m ground sampling, 0.35m radius standing capsule and straight NavMesh rays within 9m of the crossing center; gameplay balance is not tested."},Newtonsoft.Json.Formatting.Indented));
        if(failures.Count>0)throw new InvalidOperationException("Crossing audit findings: "+path);return samples+" center samples and four level, capsule-clear, navigable arms. "+path;
    }
    public static string Layout()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity")throw new InvalidOperationException("Open the outpost scene first");
        var root=GameObject.Find("TacticalOutpost").transform;var roads=root.Find("RoadsAndDecks");Physics.SyncTransforms();
        var holes=new List<string>();int samples=0;
        for(float x=-47.9f;x<53.2f;x+=.5f)for(float z=-43.4f;z<46.3f;z+=.5f){samples++;if(!Physics.RaycastAll(new Vector3(x,1,z),Vector3.down,2).Any(h=>h.transform.IsChildOf(roads)&&h.point.y>.17f))holes.Add(x+","+z);}
        var badMeshes=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.sharedMesh).Select(f=>f.name).ToArray();
        var badMaterials=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader")).Select(r=>r.name).ToArray();
        int missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        var roots=root.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&new[]{"Buildings","Cargo","Utilities","Props","Perimeter"}.Any(n=>t.IsChildOf(root.Find(n)))).Select(t=>{var rr=t.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return new{t,b};}).ToArray();
        var overlaps=new List<object>();
        for(int i=0;i<roots.Length;i++)for(int j=i+1;j<roots.Length;j++){
            var a=roots[i];var b=roots[j];if(a.t.parent==b.t.parent&&(a.t.parent.name.StartsWith("Stored pallet")||a.t.IsChildOf(root.Find("Perimeter"))||a.t.parent.name.StartsWith("Fuel farm")))continue;
            if(a.t.parent==b.t.parent&&a.t.parent.name=="Fuel to power station pipeline")continue;
            if(a.t.name=="Power station inlet"&&b.t.name.StartsWith("Power station")||b.t.name=="Power station inlet"&&a.t.name.StartsWith("Power station"))continue;
            var depth=Vector3.Min(a.b.max,b.b.max)-Vector3.Max(a.b.min,b.b.min);
            if(depth.x>.09f&&depth.y>.09f&&depth.z>.09f)overlaps.Add(new{a=a.t.parent.name+"/"+a.t.name,b=b.t.parent.name+"/"+b.t.name,depth=new[]{depth.x,depth.y,depth.z}});
        }
        var outer=root.Find("Perimeter").Cast<Transform>().SingleOrDefault(t=>t.name.StartsWith("Continuous perimeter"));
        bool fenceValid=outer&&outer.childCount==65&&!outer.Cast<Transform>().Any(t=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("Gate"));
        string path="Temp/OutpostLayoutAudit.json";File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{scene=scene.path,pavingSamples=samples,pavingHoles=holes,missingScripts,missingMeshes=badMeshes,invalidMaterials=badMaterials,unintendedBoundsOverlaps=overlaps,outerFenceValid=fenceValid,limits="Three open road entrances, no gates; 0.5m ground grid; bounds candidates exclude artist fuel assemblies, shared wall posts, pallet contents, pipe/trestle joints and the power station wall inlet. Visual screenshots complement these checks."},Newtonsoft.Json.Formatting.Indented));
        if(holes.Count+missingScripts+badMeshes.Length+badMaterials.Length+overlaps.Count>0||!fenceValid)throw new InvalidOperationException("Layout audit has findings: "+path);
        return samples+" paving samples; no missing references or unintended bounds overlaps; 65 exterior fence modules without gates. Report: "+path;
    }
    public static string RoadSurfaces()
    {
        var preview=EditorSceneManager.NewPreviewScene(); var result=new List<object>();
        try { foreach(var n in new[]{"Road_set_v1_s_tile_1","Road_set_v1_s_tile_2","Road_set_v1_s_bend_1","Road_set_v1_s_bend_2","Road_set_v1_s_floor"}) {
            var path=AssetDatabase.FindAssets(n+" t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p=>Path.GetFileNameWithoutExtension(p)==n);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview); g.transform.position=Vector3.zero;
            var surfaces=new Dictionary<string,float>();
            foreach(var f in g.GetComponentsInChildren<MeshFilter>()) { var m=f.sharedMesh; var v=m.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray(); var mats=f.GetComponent<Renderer>().sharedMaterials;
                for(int sub=0;sub<m.subMeshCount;sub++){var ts=m.GetTriangles(sub); for(int i=0;i<ts.Length;i+=3){var a=v[ts[i]];var b=v[ts[i+1]];var c=v[ts[i+2]];var cross=Vector3.Cross(b-a,c-a);if(cross.y<=0 || Mathf.Abs(a.y-b.y)>.0001f || Mathf.Abs(a.y-c.y)>.0001f)continue;string key=mats[sub].name+" @Y="+a.y.ToString("F4"); if(!surfaces.ContainsKey(key))surfaces[key]=0;surfaces[key]+=cross.magnitude*.5f;}}
            }
            result.Add(new{name=n,surfaces});UnityEngine.Object.DestroyImmediate(g);
        }} finally{EditorSceneManager.ClosePreviewScene(preview);} return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }
    public static string Inspect()
    {
        var selected = new[] { "Road_set_v1_s_floor", "Road_set_v1_b_floor", "Road_set_v1_s_tile_1", "Road_set_v1_s_tile_2", "Road_set_v1_s_bend_1", "Road_set_v1_s_bend_2", "Road_set_v1_b_bend_1", "Road_set_v1_road_up_2m_HD", "Concrete_fence_v2_S", "Concrete_fence_v2_L", "Concrete_fence_v2_Gate", "UNIConcrete_wall_v1_W_tile_1", "UNIConcrete_wall_v1_W_tile_2", "Hangar_v1_full", "Hangar_v2", "Hangar_v3", "Hangar_v4", "Oil_tank_v1", "Oil_tank_v2", "Cargo_container_v1_LD1close", "Cargo_container_v1_LD1open", "Cargo_container_v1_LD1through", "Road_block_v1", "Generator_v1", "Industrial_pipe_v1", "Pipes_set_v1_H_set_v1", "Pipes_set_v1_H_set_v2", "Pipes_set_v1_H_set_v3", "Pipes_set_v1_H_set_v4", "Pipes_set_v1_support", "Pipes_set_v1_tile_d1", "Pipes_set_v1_tile_s1", "Pipes_set_v1_bend_s", "Pipes_set_v1_U_set_v1", "Electric_box_v1", "Electric_box_v2", "Electric_box_v3", "Palet_v1_single", "Palet_v1_set", "Wooden_box_v1_LD1", "Wooden_box_v1_LD1square", "Bags_on_pallet_v1_1", "Barrel_v1_LD1", "Barrel_v2_quadro", "Dumpsters_v1_garbadge" };
        var preview = EditorSceneManager.NewPreviewScene();
        var prefabs = new List<object>();
        try
        {
            foreach (string n in selected)
            {
                string path = AssetDatabase.FindAssets(n + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == n);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var g = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                g.transform.position = Vector3.zero; g.transform.rotation = Quaternion.identity;
                var rs = g.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                Bounds b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
                prefabs.Add(new { name = n, scale = V(g.transform.localScale), min = V(b.min), max = V(b.max), size = V(b.size), colliders = g.GetComponentsInChildren<Collider>(true).Length, parts = rs.Where(r=>n.StartsWith("Road_") || n.StartsWith("Pipes_")).Select(r=>new {name=r.name,min=V(r.bounds.min),max=V(r.bounds.max)}).ToArray() });
                UnityEngine.Object.DestroyImmediate(g);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var demoPaths = AssetDatabase.FindAssets("t:SceneAsset", new[] {"Assets/RPG_FPS_game_assets_industrial"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var examples = new List<object>();
        foreach (string path in demoPaths.Where(p => Path.GetFileNameWithoutExtension(p)=="Map_v1"))
        {
            var demo = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = demo.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToArray();
                foreach (var group in roots.GroupBy(t=>Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))))
                    examples.Add(new { prefab=group.Key, count=group.Count(), samples=group.Take(3).Select(t=>new {pos=V(t.position),rot=V(t.eulerAngles),scale=V(t.localScale)}).ToArray() });
            }
            finally { EditorSceneManager.ClosePreviewScene(demo); }
        }
        string report = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/OutpostAssetAudit.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(report));
        File.WriteAllText(report, Newtonsoft.Json.JsonConvert.SerializeObject(new { prefabs, scenes = demoPaths, examples }, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
    static float[] V(Vector3 v) => new[] {v.x,v.y,v.z};
}
