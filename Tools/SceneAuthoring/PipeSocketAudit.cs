using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PipeSocketAudit
{
    public static string Inspect()
    {
        var scene=EditorSceneManager.NewPreviewScene();var report=new List<object>();
        try {foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/RPG_FPS_game_assets_industrial/Other_props/Pipes"}).Select(AssetDatabase.GUIDToAssetPath)){
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var loops=new List<object>();
            foreach(var f in g.GetComponentsInChildren<MeshFilter>()){
                var points=new List<Vector3>();var weld=new Dictionary<Vector3Int,int>();var map=new List<int>();
                foreach(var p in f.sharedMesh.vertices.Select(f.transform.TransformPoint)){var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));if(!weld.ContainsKey(key)){weld[key]=points.Count;points.Add(p);}map.Add(weld[key]);}
                var edges=new Dictionary<ulong,int>();var triangles=f.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){int a=map[triangles[i+j]],b=map[triangles[i+(j+1)%3]];if(a==b)continue;ulong key=((ulong)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(!edges.ContainsKey(key))edges[key]=0;edges[key]++;}
                var neighbours=new Dictionary<int,List<int>>();foreach(var e in edges.Where(e=>e.Value==1)){int a=(int)(e.Key>>32),b=(int)(e.Key&0xffffffff);if(!neighbours.ContainsKey(a))neighbours[a]=new List<int>();if(!neighbours.ContainsKey(b))neighbours[b]=new List<int>();neighbours[a].Add(b);neighbours[b].Add(a);}
                var visited=new HashSet<int>();foreach(int start in neighbours.Keys){if(!visited.Add(start))continue;var queue=new Queue<int>();var component=new List<int>();queue.Enqueue(start);while(queue.Count>0){int current=queue.Dequeue();component.Add(current);foreach(int next in neighbours[current])if(visited.Add(next))queue.Enqueue(next);}
                    var v=component.Select(i=>points[i]).ToArray();var center=v.Aggregate(Vector3.zero,(a,b)=>a+b)/v.Length;var min=v.Aggregate(Vector3.positiveInfinity,Vector3.Min);var max=v.Aggregate(Vector3.negativeInfinity,Vector3.Max);var size=max-min;
                    if(component.Count>=6)loops.Add(new{vertices=v.Length,center=new[]{center.x,center.y,center.z},extent=new[]{size.x,size.y,size.z},radius=v.Average(p=>Vector3.Distance(p,center))});
                }
            }
            var r=g.GetComponentInChildren<Renderer>();report.Add(new{prefab=g.name.Replace("(Clone)",""),min=r.bounds.min.ToString("F6"),max=r.bounds.max.ToString("F6"),loops});UnityEngine.Object.DestroyImmediate(g);
        }} finally {EditorSceneManager.ClosePreviewScene(scene);}
        File.WriteAllText("Temp/PipeSocketAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return "Temp/PipeSocketAudit.json";
    }
}
