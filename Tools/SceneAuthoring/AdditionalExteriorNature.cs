using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AdditionalExteriorNature
{
    static Bounds BoundsOf(GameObject g)
    {
        var renderers=g.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
        if(renderers.Length==0)throw new InvalidOperationException("No geometry: "+g.name);
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
    }
    static bool Overlap(Bounds a,Bounds b)
    {
        return Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x)>.15f&&
            Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z)>.15f&&
            Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y)>.15f;
    }
    static bool Reserved(Bounds b)
    {
        if(b.max.x>-56.45f&&b.min.x<61.66f&&b.max.z>-51.95f&&b.min.z<54.82f)return true;
        if(b.min.x<-48&&b.max.z>-16.3f&&b.min.z<7.8f)return true;
        if(b.max.z>46&&b.max.x>-9.4f&&b.min.x<14.6f)return true;
        if(b.min.z<-44&&b.max.x>7.6f&&b.min.x<31.6f)return true;
        if(b.max.x>-91&&b.min.x<-61&&b.max.z>48&&b.min.z<78)return true;
        if(b.max.x>62&&b.min.x<94&&b.max.z>-80&&b.min.z<-48)return true;
        return b.min.x<-121||b.max.x>121||b.min.z<-121||b.max.z>121;
    }
    public static string Add()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/TacticalOutpost_Solo.unity");
        var root=scene.GetRootGameObjects().Single().transform;var nature=root.Find("Nature");
        const string name="Additional exterior dry trees and stumps";
        if(nature.Find(name))throw new InvalidOperationException("Additional nature already placed.");
        var trees=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dry_Trees/Prefab"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        var stumps=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/GreenBugGames/Scan Stump Vol.1/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        if(trees.Length!=7||stumps.Length!=5)throw new InvalidOperationException("Expected native nature prefabs not found.");
        var terrain=root.GetComponentInChildren<Terrain>();
        var occupied=root.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)&&!r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name.Contains("Dust"))).Select(r=>r.bounds).ToList();
        var centers=new[]{new Vector2(-71,24),new Vector2(-72,-31),new Vector2(-52,73),new Vector2(-27,79),new Vector2(30,85),new Vector2(78,66),new Vector2(85,27),new Vector2(87,-24),new Vector2(73,-89),new Vector2(41,-87),new Vector2(-14,-84),new Vector2(-68,-81)};
        var random=new System.Random(202610202);var positions=new List<Vector2>();var records=new List<object>();
        var preview=EditorSceneManager.NewPreviewScene();GameObject stage=null;int treeCount=0,stumpCount=0;
        try
        {
            stage=new GameObject(name);SceneManager.MoveGameObjectToScene(stage,preview);
            for(int attempt=0;attempt<24000&&(treeCount<48||stumpCount<36);attempt++)
            {
                bool tree=treeCount<48&&(stumpCount>=36||random.NextDouble()<.58);
                var center=centers[random.Next(centers.Length)];float angle=(float)random.NextDouble()*Mathf.PI*2;
                float radius=Mathf.Sqrt((float)random.NextDouble())*21;var p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(positions.Any(o=>Vector2.Distance(o,p)<(tree?5f:2f)))continue;
                float nx=(p.x-terrain.transform.position.x)/terrain.terrainData.size.x,nz=(p.y-terrain.transform.position.z)/terrain.terrainData.size.z;
                if(nx<0||nx>1||nz<0||nz>1||terrain.terrainData.GetSteepness(nx,nz)>16)continue;
                var path=tree?trees[random.Next(trees.Length)]:stumps[random.Next(stumps.Length)];
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);go.transform.SetParent(stage.transform,false);
                float scale=tree?.72f+(float)random.NextDouble()*.38f:.8f+(float)random.NextDouble()*.45f;
                go.transform.localScale=Vector3.one*scale;go.transform.rotation=Quaternion.Euler(tree?0:random.Next(-4,5),(float)random.NextDouble()*360,tree?0:random.Next(-4,5));
                float ground=terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y;
                go.transform.position=new Vector3(p.x,ground,p.y);var b=BoundsOf(go);go.transform.position+=Vector3.up*(ground-b.min.y-(tree?.12f:.035f));b=BoundsOf(go);
                if(Reserved(b)||occupied.Any(o=>Overlap(b,o))){UnityEngine.Object.DestroyImmediate(go);continue;}
                if(tree)treeCount++;else stumpCount++;
                go.name=(tree?"Dry tree "+treeCount.ToString("00"):"Stump "+stumpCount.ToString("00"))+" - "+Path.GetFileNameWithoutExtension(path);
                occupied.Add(b);positions.Add(p);records.Add(new{go.name,prefab=path,position=new[]{go.transform.position.x,go.transform.position.y,go.transform.position.z},scale});
            }
            if(treeCount!=48||stumpCount!=36)throw new InvalidOperationException("Could not place all nature without overlaps: "+treeCount+" trees, "+stumpCount+" stumps.");
            Undo.RegisterCreatedObjectUndo(stage,"Additional exterior nature");SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(nature,false);
            File.WriteAllText("Docs/Integration/AdditionalExteriorNature.json",JsonConvert.SerializeObject(new{treeCount,stumpCount,seed=202610202,objects=records},Formatting.Indented));
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Environment save failed.");
            stage=null;return "Added 48 dry trees and 36 stumps in irregular exterior patches; original objects retained, approaches and ruin footprints clear.";
        }
        finally{if(stage)UnityEngine.Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(preview);}
    }
}
