using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OutpostBarrelDressing
{
    const string GroupName="Burning barrels - visible road scatter";
    const string PrefabFolder="Assets/TacticalOutpost/Prefabs/BurningBarrels";
    static readonly string[] Variants={"Custom","Military","Biohazard"};
    static Bounds BoundsOf(GameObject g){var rr=g.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Material ParticleMaterial(string name,bool additive)
    {
        string path="Assets/TacticalOutpost/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name=name+" soft sprite",wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){float px=(x+0.5f)/32-1,py=(y+0.5f)/32-1;float radius=additive?px*px/(.45f-.15f*py)+py*py:px*px+py*py;float alpha=Mathf.Pow(Mathf.Clamp01(1-radius),2);texture.SetPixel(x,y,new Color(1,1,1,alpha));}texture.Apply();AssetDatabase.CreateAsset(texture,"Assets/TacticalOutpost/Materials/"+name+"_Sprite.asset");
        material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name=name};material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",additive?2:0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;AssetDatabase.CreateAsset(material,path);return material;
    }
    static ParticleSystem Particles(Transform parent,string name,Material material,int rate,float lifetime,float speed,float size,Color start,Color end)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localRotation=Quaternion.Euler(-90,0,0);var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(71+parent.parent.GetSiblingIndex()*19+rate);
        var main=ps.main;main.loop=true;main.playOnAwake=true;main.prewarm=true;main.duration=4;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.7f,lifetime);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.7f,speed);main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.maxParticles=64;main.cullingMode=ParticleSystemCullingMode.Automatic;main.startColor=Color.white;
        var emission=ps.emission;emission.rateOverTime=rate;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.18f;shape.radiusThickness=1;
        var colour=ps.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();float alpha=name=="Smoke"?.16f:.85f;gradient.SetKeys(new[]{new GradientColorKey(start,0),new GradientColorKey(end,.7f)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(alpha,.08f),new GradientAlphaKey(alpha*.45f,.65f),new GradientAlphaKey(0,1)});colour.color=gradient;
        var noise=ps.noise;noise.enabled=true;noise.strength=.12f;noise.frequency=.8f;noise.scrollSpeed=.4f;noise.quality=ParticleSystemNoiseQuality.Low;
        var renderer=g.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=2;
        return ps;
    }
    static void Fire(Transform parent,Vector3 position,Material flame,Material smoke)
    {
        var g=new GameObject("Burning barrel - visual only");g.transform.SetParent(parent,false);g.transform.position=position;
        var ps=Particles(g.transform,"Flames",flame,26,.65f,.8f,.5f,new Color(2.2f,1.15f,.2f),new Color(.9f,.12f,.005f));
        var main=ps.main;main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(.2f,.38f);main.startSizeY=new ParticleSystem.MinMaxCurve(.35f,.65f);main.startSizeZ=.3f;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.25f,1),new Keyframe(1,.05f)));
        ps=Particles(g.transform,"Smoke",smoke,6,2.8f,.6f,.5f,new Color(.12f,.13f,.15f),new Color(.045f,.05f,.06f));ps.transform.localPosition=Vector3.up*.4f;size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(1,2.5f)));
        ps=Particles(g.transform,"Embers",flame,4,1.4f,1.2f,.035f,new Color(2,1,.1f),new Color(1,.1f,0));
        var glow=new GameObject("Warm fire glow");glow.transform.SetParent(g.transform,false);glow.transform.localPosition=Vector3.down*.12f;var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.4f,.10f);light.intensity=2.8f+parent.GetSiblingIndex()%4*.2f;light.range=4.5f;light.shadows=LightShadows.None;
        foreach(var system in g.GetComponentsInChildren<ParticleSystem>()){system.Simulate(3+parent.GetSiblingIndex()*.137f,true,true);system.Play();}
    }
    public static string BuildMasters()
    {
        if(!AssetDatabase.IsValidFolder("Assets/TacticalOutpost/Prefabs"))AssetDatabase.CreateFolder("Assets/TacticalOutpost","Prefabs");
        if(!AssetDatabase.IsValidFolder(PrefabFolder))AssetDatabase.CreateFolder("Assets/TacticalOutpost/Prefabs","BurningBarrels");
        var flame=ParticleMaterial("Outpost_Fire",true);var smoke=ParticleMaterial("Outpost_Smoke",false);var preview=EditorSceneManager.NewPreviewScene();
        try{foreach(string variant in Variants){
            var g=new GameObject("BurningBarrel_"+variant);SceneManager.MoveGameObjectToScene(g,preview);
            var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sector Warzone/Post-Apocalyptic Barrels/Prefabs/PRE_Barrel_"+variant+".prefab"),preview);body.transform.SetParent(g.transform,false);body.transform.position-=Vector3.up*BoundsOf(body).min.y;var b=BoundsOf(body);
            Fire(g.transform,new Vector3(b.center.x,b.max.y-.015f,b.center.z),flame,smoke);
            if(g.GetComponentsInChildren<MeshCollider>().Length!=1||g.GetComponentsInChildren<ParticleSystem>().Length!=3||Mathf.Abs(BoundsOf(body).min.y)>.002f)throw new InvalidOperationException("Master barrel geometry incomplete");
            var fire=g.transform.Find("Burning barrel - visual only");if(Mathf.Abs(fire.position.y-b.max.y)>.02f||Vector2.Distance(new Vector2(fire.position.x,fire.position.z),new Vector2(b.center.x,b.center.z))>.001f)throw new InvalidOperationException("Fire must be centered at the barrel top");
            bool saved;PrefabUtility.SaveAsPrefabAsset(g,PrefabFolder+"/"+g.name+".prefab",out saved);if(!saved)throw new InvalidOperationException("Could not save master barrel");UnityEngine.Object.DestroyImmediate(g);
        }AssetDatabase.SaveAssets();return "Three master prefabs saved; each has one native barrel, top-centered fire, smoke, embers and glow.";
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Arrange()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;var previous=root.Find(GroupName);if(!previous)previous=root.Find("Scattered barrel reserves and small fires");
        var terrain=root.Find("World").GetComponentInChildren<Terrain>();var paving=root.Find("RoadsAndDecks").GetComponentsInChildren<Collider>();
        float Ground(float x,float z){float y=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;foreach(var c in paving)if(c.Raycast(new Ray(new Vector3(x,30,z),Vector3.down),out var hit,60))y=Mathf.Max(y,hit.point.y);return y;}
        bool OnRoad(float x,float z)=>x> -77&&x<53&&Mathf.Abs(z+4.24014f)<2.5f||z> -2&&z<77&&Mathf.Abs(x-2.60482f)<2.5f||z< -7&&z> -42&&Mathf.Abs(x-19.62226f)<2.5f;
        var occupied=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)&&(!previous||!r.transform.IsChildOf(previous))&&!r.transform.IsChildOf(root.Find("World"))&&!r.transform.IsChildOf(root.Find("RoadsAndDecks"))).Select(r=>r.bounds).ToList();
        var markers=root.Find("GameplayMarkers").Cast<Transform>().Where(t=>t.name.StartsWith("Spawn_")||t.name=="PlayerStart"||t.name=="Objective_Main").Select(t=>t.position).ToArray();
        var rng=new System.Random(398635);var sites=new List<Vector2>();
        foreach(float x in new[]{-70f,-61,-50,-42,-33,-24,-16,-8,1,11,21,31,41,49})sites.Add(new Vector2(x+(float)rng.NextDouble()*4-2,-4.24014f+(float)rng.NextDouble()*2.8f-1.4f));
        foreach(float z in new[]{8f,18,28,39,51,63,73})sites.Add(new Vector2(2.60482f+(float)rng.NextDouble()*2.8f-1.4f,z+(float)rng.NextDouble()*4-2));
        foreach(float z in new[]{-12f,-18,-26,-34,-40})sites.Add(new Vector2(19.62226f+(float)rng.NextDouble()*2.8f-1.4f,z+(float)rng.NextDouble()*3-1.5f));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Scatter single burning barrels along roads");var manifest=new List<object>();
        try{
            var stage=new GameObject(GroupName);SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(root,false);Undo.RegisterCreatedObjectUndo(stage,"Road barrel scatter");
            for(int i=0;i<sites.Count;i++){
                string variant=Variants[rng.Next(Variants.Length)];var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/BurningBarrel_"+variant+".prefab");if(!asset)throw new InvalidOperationException("Build and inspect all three master barrels first");
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);g.name="Burning "+variant+" barrel "+(i+1);g.transform.SetParent(stage.transform,false);g.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);bool placed=false;Bounds b=new Bounds();
                for(int attempt=0;attempt<161;attempt++){
                    float radius=attempt==0?0:.4f*Mathf.Sqrt(attempt),angle=attempt*2.399963f,x=sites[i].x+Mathf.Cos(angle)*radius,z=sites[i].y+Mathf.Sin(angle)*radius;
                    if(!OnRoad(x,z)||markers.Any(p=>Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.z))<3))continue;
                    float ground=Ground(x,z);if(new[]{new Vector2(-.27f,-.27f),new Vector2(.27f,-.27f),new Vector2(.27f,.27f),new Vector2(-.27f,.27f)}.Any(o=>Mathf.Abs(Ground(x+o.x,z+o.y)-ground)>.025f))continue;
                    g.transform.position=new Vector3(x,ground-.004f,z);b=BoundsOf(g);var clear=b;clear.Expand(new Vector3(3.2f,.02f,3.2f));if(occupied.Any(o=>o.Intersects(clear)))continue;placed=true;break;
                }
                if(!placed)throw new InvalidOperationException("No clear road position for barrel "+(i+1));
                var fire=g.transform.Find("Burning barrel - visual only");if(Mathf.Abs(fire.position.y-b.max.y)>.02f)throw new InvalidOperationException("Fire detached from barrel top");
                foreach(var ps in g.GetComponentsInChildren<ParticleSystem>()){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.randomSeed+=(uint)(i*97+3);ps.Simulate(3+i*.137f,true,true);ps.Play();PrefabUtility.RecordPrefabInstancePropertyModifications(ps);}
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);occupied.Add(b);manifest.Add(new{g.name,variant,x=g.transform.position.x,y=g.transform.position.y,z=g.transform.position.z});
            }
            if(stage.transform.childCount!=26||stage.GetComponentsInChildren<MeshCollider>().Length!=26||stage.GetComponentsInChildren<ParticleSystem>().Length!=78)throw new InvalidOperationException("Each road barrel must contain exactly one body and three fire layers");
            if(previous)Undo.DestroyObjectImmediate(previous.gameObject);Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
            File.WriteAllText("Temp/ScatteredBarrelDressing.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{barrels=manifest,total=26,limits="Single barrels from three master prefabs; all fires centered at barrel tops. Fire is visual only, with no damage or audio."},Newtonsoft.Json.Formatting.Indented));return "Replaced hidden stacks with 26 single burning barrels scattered across visible roads; all three master prefab variants used.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
