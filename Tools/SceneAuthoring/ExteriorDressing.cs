using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExteriorDressing
{
    static Bounds BoundsOf(GameObject g) {
        var rr=g.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
        if(rr.Length==0)throw new InvalidOperationException("No renderers: "+g.name);
        var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    static readonly string[] Folders={"Assets/Sector Warzone/Concrete Barriers/Prefabs","Assets/Sector Warzone/Post-Apocalyptic Barrels/Prefabs","Assets/DBK/Prefabs","Assets/GreenBugGames/Scan Stump Vol.1/Prefabs"};
    public static string RepairRuinReferences() {
        const string path="Assets/DBK/Prefabs/Demo/DBK_Demo_Building.prefab";var g=PrefabUtility.LoadPrefabContents(path);
        try {
            // The demo carries obsolete mesh-combiner components; its colour-theme components remain intact.
            foreach(var t in g.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach(var f in g.GetComponentsInChildren<MeshFilter>(true))if(!f.sharedMesh){f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DBK/CombinedMesh/DBK_Demo_Building/"+f.name+".asset");if(!f.sharedMesh)throw new InvalidOperationException("Missing ruin mesh: "+f.name);EditorUtility.SetDirty(f);PrefabUtility.RecordPrefabInstancePropertyModifications(f);}
            foreach(var r in g.GetComponentsInChildren<MeshRenderer>(true)) {
                string name=r.name.Contains("Rebar")?"DBK_Rebar":r.name.Contains("Rubble")?"DBK_Concrete_Rubble_A":"DBK_Concrete";
                var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/DBK/Materials/"+name+".mat");if(!m)throw new InvalidOperationException("Missing ruin material");r.sharedMaterials=new[]{m};EditorUtility.SetDirty(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            bool saved;PrefabUtility.SaveAsPrefabAsset(g,path,out saved);if(!saved)throw new InvalidOperationException("Ruin prefab save failed");
        }finally {PrefabUtility.UnloadPrefabContents(g);}
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset.GetComponentsInChildren<MeshFilter>(true).Any(f=>!f.sharedMesh)||asset.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m)))throw new InvalidOperationException("Ruin references still missing after save");
        return "Ruin references repaired; native geometry and colour themes preserved.";
    }
    public static string PrepareMaterials() {
        const string folder="Assets/GreenBugGames/Scan Stump Vol.1/Models/Materials";
        foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath)) {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m.shader.name=="Universal Render Pipeline/Lit")continue;
            var texture=m.GetTexture("_MainTex");var color=m.GetColor("_Color");var tiling=m.GetTextureScale("_MainTex");var offset=m.GetTextureOffset("_MainTex");float smooth=m.GetFloat("_GlossMapScale");
            m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",color);m.SetTextureScale("_BaseMap",tiling);m.SetTextureOffset("_BaseMap",offset);m.SetFloat("_Smoothness",smooth);
            m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");m.SetFloat("_AlphaClip",1);m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;EditorUtility.SetDirty(m);
            if(!texture||m.shader.name!="Universal Render Pipeline/Lit")throw new InvalidOperationException("Stump material conversion failed");
        }
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Folders[3]}).Select(AssetDatabase.GUIDToAssetPath)) {
            var g=PrefabUtility.LoadPrefabContents(path);
            try {string stem=Path.GetFileNameWithoutExtension(path).Replace("_prefab","");foreach(var r in g.GetComponentsInChildren<Renderer>(true)) {
                bool billboard=r.name.IndexOf("billboard",StringComparison.OrdinalIgnoreCase)>=0||r.sharedMaterials.Any(m=>m&&m.name.Contains("Billboard"));
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+stem+(billboard?"_Billboard_Color.mat":"_Color.mat"));if(!material)throw new InvalidOperationException("Missing stump material");r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();
            }PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
        }
        const string barrel="Assets/Sector Warzone/Post-Apocalyptic Barrels/";
        var bio=PrefabUtility.LoadPrefabContents(barrel+"Prefabs/PRE_Barrel_Biohazard.prefab");
        try {foreach(var r in bio.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(barrel+"Materials/M_Barrel_BarrelBiohazard.mat");PrefabUtility.SaveAsPrefabAsset(bio,barrel+"Prefabs/PRE_Barrel_Biohazard.prefab");}finally{PrefabUtility.UnloadPrefabContents(bio);}
        AssetDatabase.SaveAssets();return "Stump materials and all LOD assignments prepared for URP; biohazard barrel material corrected.";
    }
    public static string Inspect() {
        var preview=EditorSceneManager.NewPreviewScene();var items=new List<object>();
        try {foreach(var folder in Folders.Where(AssetDatabase.IsValidFolder))foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath)) {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);g.transform.position=Vector3.zero;
            var b=BoundsOf(g);items.Add(new{path,scale=V(g.transform.localScale),min=V(b.min),max=V(b.max),size=V(b.size),colliders=g.GetComponentsInChildren<Collider>(true).Length,parts=g.transform.Cast<Transform>().Select(t=>new{t.name,p=V(t.localPosition),r=V(t.localEulerAngles)}).ToArray(),materials=g.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().Select(m=>new{m.name,shader=m.shader.name,m.shader.isSupported,m.enableInstancing}).ToArray()});
            UnityEngine.Object.DestroyImmediate(g);
        }}finally{EditorSceneManager.ClosePreviewScene(preview);}
        File.WriteAllText("Temp/ExteriorAssetInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(items,Newtonsoft.Json.Formatting.Indented));return "Temp/ExteriorAssetInspection.json";
    }
    static void Render(Scene scene,Vector3 eye,Vector3 target,string path,int width=1280,int height=800) {
        var go=new GameObject("Exterior audit camera");SceneManager.MoveGameObjectToScene(go,scene);go.hideFlags=HideFlags.HideAndDontSave;
        var rt=new RenderTexture(width,height,24);var old=RenderTexture.active;Texture2D tex=null;
        try {
            var camera=go.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=700;camera.fieldOfView=60;camera.clearFlags=CameraClearFlags.Skybox;
            if(!EditorSceneManager.IsPreviewScene(scene))camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=true;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.targetTexture=rt;camera.transform.position=eye;camera.transform.LookAt(target);camera.Render();
            RenderTexture.active=rt;tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        }finally {RenderTexture.active=old;if(tex)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    public static string ContactSheet() {
        var preview=EditorSceneManager.NewPreviewScene();
        try {
            var paths=Folders.Where(AssetDatabase.IsValidFolder).SelectMany(folder=>AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath)).Where(p=>!p.Contains("DBK")).ToArray();int i=0;
            foreach(var path in paths){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);var b=BoundsOf(g);float x=i%4*3,z=i/4*4;g.transform.position+=new Vector3(x-b.center.x,-b.min.y,z-b.center.z);i++;}
            var sun=new GameObject("Preview sun");SceneManager.MoveGameObjectToScene(sun,preview);sun.AddComponent<Light>().type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);
            Render(preview,new Vector3(12,13,-12),new Vector3(4,0,5),"Temp/ExteriorContactSheet.png");return string.Join("\n",paths);
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string RuinPreview() {
        var preview=EditorSceneManager.NewPreviewScene();
        try {var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DBK/Prefabs/Demo/DBK_Demo_Building.prefab"),preview);g.transform.position=Vector3.zero;var b=BoundsOf(g);var sun=new GameObject("Preview sun");SceneManager.MoveGameObjectToScene(sun,preview);sun.AddComponent<Light>().type=LightType.Directional;sun.GetComponent<Light>().intensity=2;sun.transform.rotation=Quaternion.Euler(45,-35,0);Render(preview,b.center+new Vector3(18,14,-18),b.center,"Temp/RuinArtistDemo.png");File.WriteAllText("Temp/RuinArtistParts.json",Newtonsoft.Json.JsonConvert.SerializeObject(g.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()).Select(f=>new{f.name,p=V(f.transform.position),min=V(f.GetComponent<Renderer>().bounds.min),max=V(f.GetComponent<Renderer>().bounds.max),materials=f.GetComponent<Renderer>().sharedMaterials.Select(m=>m?m.name:"MISSING").ToArray()}).ToArray(),Newtonsoft.Json.Formatting.Indented));return "Temp/RuinArtistParts.json";}
        finally {EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Capture(float x,float y,float z,float tx,float ty,float tz,string name) {
        Render(SceneManager.GetActiveScene(),new Vector3(x,y,z),new Vector3(tx,ty,tz),"Temp/"+name+".png");return "Temp/"+name+".png";
    }
    public static string BurningBarrelPreview() {
        var preview=EditorSceneManager.NewPreviewScene();Material floor=null;
        try {
            int i=0;foreach(string variant in new[]{"Custom","Military","Biohazard"}){
                var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_"+variant+".prefab"),preview);g.transform.position=new Vector3(i++*2,0,0);
                foreach(var ps in g.GetComponentsInChildren<ParticleSystem>()){ps.Simulate(3+i*.137f,true,true);ps.Play();}
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);SceneManager.MoveGameObjectToScene(ground,preview);floor=new Material(Shader.Find("Universal Render Pipeline/Lit"));floor.SetColor("_BaseColor",new Color(.16f,.18f,.22f));ground.GetComponent<Renderer>().sharedMaterial=floor;
            var sun=new GameObject("Preview moon");SceneManager.MoveGameObjectToScene(sun,preview);var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.color=new Color(.6f,.72f,1);sun.transform.rotation=Quaternion.Euler(35,-35,0);
            Render(preview,new Vector3(6,2.6f,-7),new Vector3(2,.8f,0),"Temp/BurningBarrel_Masters.png");return "Temp/BurningBarrel_Masters.png";
        }finally{EditorSceneManager.ClosePreviewScene(preview);if(floor)UnityEngine.Object.DestroyImmediate(floor);}
    }
    public static string FloodlightPreview() {
        var preview=EditorSceneManager.NewPreviewScene();
        try{
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab"),preview);
            var sun=new GameObject("Preview sun");SceneManager.MoveGameObjectToScene(sun,preview);var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;sun.transform.rotation=Quaternion.Euler(40,-35,0);
            var b=BoundsOf(g);Render(preview,b.center+new Vector3(.85f,.4f,1),b.center,"Temp/Tower_SingleHead.png");return "Temp/Tower_SingleHead.png";
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string InfrastructurePreview() {
        var preview=EditorSceneManager.NewPreviewScene();
        try {
            var paths=new[]{"Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp1_Tall (Concrete).prefab","Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp1_TallDouble (Concrete).prefab","Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp2_Tall (Concrete).prefab","Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp2_ShortDouble (Black).prefab","Assets/Tower/Models/Tower.FBX"};int i=0;
            foreach(var path in paths){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);g.transform.position=Vector3.zero;var b=BoundsOf(g);g.transform.position+=new Vector3(i==4?4:i*3-b.center.x,-b.min.y,i==4?8-b.center.z:-b.center.z);i++;}
            var sun=new GameObject("Preview sun");SceneManager.MoveGameObjectToScene(sun,preview);sun.AddComponent<Light>().type=LightType.Directional;sun.GetComponent<Light>().intensity=2;sun.transform.rotation=Quaternion.Euler(40,-30,0);
            Render(preview,new Vector3(17,10,-17),new Vector3(4,2,4),"Temp/Infrastructure_Models.png");return "Temp/Infrastructure_Models.png";
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string ReplaceLampsAndTower() {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;var lighting=root.Find("Lighting");var terrain=root.Find("World").GetComponentInChildren<Terrain>();var data=terrain.terrainData;
        const string name="Exterior infrastructure - SpaceZeta lamps and guard tower";var previous=root.Find(name);var oldLamps=lighting.Cast<Transform>().Where(t=>t.name.StartsWith("Exterior street lights")).ToArray();
        bool IsDryTree(GameObject g)=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g).StartsWith("Assets/Dry_Trees/");
        var trees=root.GetComponentsInChildren<MeshCollider>().Where(c=>IsDryTree(c.gameObject)).ToArray();
        var existing=root.GetComponentsInChildren<MeshRenderer>().Where(r=>!IsDryTree(r.gameObject)&&(!previous||!r.transform.IsChildOf(previous))&&!oldLamps.Any(t=>r.transform.IsChildOf(t))).Select(r=>r.bounds).ToList();
        var positions=new List<(float x,float z,float yaw,bool entry,bool alongX)>();
        foreach(float x in new[]{-37f,-20f,25f,43f})positions.Add((x,50.31218f,180,false,true));
        foreach(float x in new[]{-38f,-18f,0f,39f,51f})positions.Add((x,-47.4475f,0,false,true));
        foreach(float z in new[]{-34f,-17f,14f,35f})positions.Add((-51.9475f,z,90,false,false));
        foreach(float z in new[]{-32f,-11f,12f,35f})positions.Add((57.15714f,z,270,false,false));
        foreach(float x in new[]{2.60482f-4.45f,2.60482f+4.45f})positions.Add((x,51.81218f,90,true,false));
        foreach(float z in new[]{-4.24014f-4.45f,-4.24014f+4.45f})positions.Add((-53.4475f,z,0,true,true));
        int station=0;foreach(float x in new[]{-68f,-34f,-16f,11f,35f,46f}){bool north=station++%2==0;positions.Add((x,-4.24014f+(north?4.45f:-4.45f),north?180:0,false,true));}
        station=0;foreach(float z in new[]{64f,32f,12f}){bool east=station++%2==0;positions.Add((2.60482f+(east?4.45f:-4.45f),z,east?270:90,false,false));}
        station=0;foreach(float z in new[]{-16f,-34f}){bool east=station++%2==0;positions.Add((19.62226f+(east?4.45f:-4.45f),z,east?270:90,false,false));}
        bool Overlap(Bounds a,Bounds b)=>a.max.x>b.min.x&&a.min.x<b.max.x&&a.max.z>b.min.z&&a.min.z<b.max.z;
        var paving=root.Find("RoadsAndDecks").GetComponentsInChildren<Collider>();
        float Ground(float x,float z){float y=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;foreach(var c in paving)if(c.Raycast(new Ray(new Vector3(x,30,z),Vector3.down),out var hit,60))y=Mathf.Max(y,hit.point.y);return y;}
        bool HitsTree(GameObject g,Bounds b){var box=g.AddComponent<BoxCollider>();try{box.center=g.transform.InverseTransformPoint(b.center);var size=Quaternion.Inverse(g.transform.rotation)*b.size;box.size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));return trees.Any(c=>c.bounds.Intersects(b)&&Physics.ComputePenetration(box,g.transform.position,g.transform.rotation,c,c.transform.position,c.transform.rotation,out var direction,out var distance));}finally{UnityEngine.Object.DestroyImmediate(box);}}
        var obstacles=root.GetComponentsInChildren<Collider>().Where(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(root.Find("World"))&&!c.transform.IsChildOf(root.Find("RoadsAndDecks"))&&(!previous||!c.transform.IsChildOf(previous))&&!oldLamps.Any(t=>c.transform.IsChildOf(t))).ToList();
        bool LampClashes(GameObject g,Bounds b){var body=g.GetComponent<CapsuleCollider>();if(!body)throw new InvalidOperationException("Expected native lamp post capsule");float height=b.max.y-g.transform.position.y;body.height=height;body.center=new Vector3(body.center.x,height*.5f,body.center.z);var head=g.AddComponent<BoxCollider>();try{var center=g.transform.InverseTransformPoint(b.center);center.y=height-.5f;head.center=center;var size=Quaternion.Inverse(g.transform.rotation)*b.size;head.size=new Vector3(Mathf.Abs(size.x),1,Mathf.Abs(size.z));return obstacles.Any(c=>c.bounds.Intersects(b)&&(Physics.ComputePenetration(body,g.transform.position,g.transform.rotation,c,c.transform.position,c.transform.rotation,out var direction,out var distance)&&distance>.005f||Physics.ComputePenetration(head,g.transform.position,g.transform.rotation,c,c.transform.position,c.transform.rotation,out direction,out distance)&&distance>.005f));}finally{UnityEngine.Object.DestroyImmediate(head);}}
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace street lamps and add guard tower");var preview=EditorSceneManager.NewPreviewScene();GameObject stage=null;var manifest=new List<object>();
        try {
            stage=new GameObject(name);SceneManager.MoveGameObjectToScene(stage,preview);var lamps=new GameObject("Native concrete street lamps");SceneManager.MoveGameObjectToScene(lamps,preview);lamps.transform.SetParent(stage.transform,false);
            foreach(var item in positions) {
                string path="Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp1_"+(item.entry?"TallDouble":"Tall")+" (Concrete).prefab";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException("New lamp prefab missing");
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,preview);g.transform.SetParent(lamps.transform,false);float ground=0;Bounds b=new Bounds();bool seated=false;g.name=(item.entry?"Double entrance lamp ":manifest.Count>=21?"Roadside lamp ":"Concrete perimeter lamp ")+(manifest.Count+1);
                foreach(float offset in new[]{0f,-1,1,-2,2,-3,3}){float x=item.x+(item.alongX?offset:0),z=item.z+(item.alongX?0:offset);ground=Ground(x,z);g.transform.SetPositionAndRotation(new Vector3(x,ground-.015f,z),Quaternion.Euler(0,item.yaw,0));b=BoundsOf(g);if(LampClashes(g,b))continue;seated=true;break;}
                if(!seated)throw new InvalidOperationException("No clear lamp position: "+g.name);
                foreach(var light in g.GetComponentsInChildren<Light>()){light.color=new Color(1,.87f,.7f);light.shadows=item.entry&&light.type==LightType.Spot?LightShadows.Soft:LightShadows.None;if(light.type==LightType.Spot){light.intensity=4;light.range=11;}else {light.intensity=.6f;light.range=1.2f;}}
                if(g.transform.localScale!=Vector3.one||Mathf.Abs(b.min.y-(ground-.015f))>.003f)throw new InvalidOperationException("New lamp native base mismatch");existing.Add(b);obstacles.Add(g.GetComponent<CapsuleCollider>());manifest.Add(new{g.name,path,position=V(g.transform.position),item.yaw,item.entry});
            }
            var tower=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tower/Models/Tower.FBX"),preview);tower.transform.SetParent(stage.transform,false);tower.name="NW perimeter guard tower - native scale";tower.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,270,0)*AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tower/Models/Tower.FBX").transform.rotation);var tb=BoundsOf(tower);float level=Ground(-54,44.5f);tower.transform.position=new Vector3(-54-tb.center.x,level-.015f-tb.min.y,44.5f-tb.center.z);tb=BoundsOf(tower);
            if(existing.Any(b=>Overlap(tb,b))||HitsTree(tower,tb))throw new InvalidOperationException("Guard tower needs a clearer footprint");
            foreach(var f in tower.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)throw new InvalidOperationException("Tower mesh missing");f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;}
            if(stage.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader")))throw new InvalidOperationException("Infrastructure material not ready");
            Undo.RegisterCompleteObjectUndo(data,"Level ground under the native tower plinth");int n=data.heightmapResolution;var heights=data.GetHeights(0,0,n,n);var pad=tb;pad.Expand(new Vector3(1.3f,0,1.3f));
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){float wx=terrain.transform.position.x+x*data.size.x/(n-1),wz=terrain.transform.position.z+z*data.size.z/(n-1);float dx=Mathf.Max(pad.min.x-wx,0,wx-pad.max.x),dz=Mathf.Max(pad.min.z-wz,0,wz-pad.max.z),distance=Mathf.Sqrt(dx*dx+dz*dz);if(distance<1)heights[z,x]=Mathf.Lerp(heights[z,x],(level-terrain.transform.position.y)/data.size.y,1-Mathf.SmoothStep(0,1,distance));}
            data.SetHeights(0,0,heights);terrain.Flush();
            foreach(float x in new[]{tb.min.x,tb.center.x,tb.max.x})foreach(float z in new[]{tb.min.z,tb.center.z,tb.max.z})if(Mathf.Abs(tb.min.y-Ground(x,z))>.025f)throw new InvalidOperationException("Tower plinth is not seated");
            if(manifest.Count!=32)throw new InvalidOperationException("Lamp replacement count mismatch");
            foreach(var old in oldLamps)Undo.DestroyObjectImmediate(old.gameObject);if(previous)Undo.DestroyObjectImmediate(previous.gameObject);SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(root,true);StageUtility.PlaceGameObjectInCurrentStage(stage);Undo.RegisterCreatedObjectUndo(stage,"New exterior infrastructure");
            Physics.SyncTransforms();EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/OutpostInfrastructure.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{lamps=manifest,tower=new{position=V(tower.transform.position),yaw=270,scale=V(tower.transform.localScale),bounds=new{min=V(tb.min),max=V(tb.max)}},limits="Native model sizes retained. The tower stair faces the perimeter walkway with more than 2m clearance to the fence; its plinth is terrain-seated. Lighting has no day/night controller."},Newtonsoft.Json.Formatting.Indented));
            stage=null;Undo.CollapseUndoOperations(undo);return "Replaced old lamps with 32 SpaceZeta concrete poles along approaches, camp roads and perimeter; added one native NW guard tower; scene saved.";
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
        finally{if(stage)UnityEngine.Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Arrange() {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;var terrain=root.Find("World").GetComponentInChildren<Terrain>();var data=terrain.terrainData;
        const string groupName="Exterior dressing - ruins, checkpoints and stumps";var previous=root.Find(groupName);
        var occupied=root.GetComponentsInChildren<MeshRenderer>().Where(r=>!previous||!r.transform.IsChildOf(previous)).Select(r=>r.bounds).ToList();
        var pads=new List<(Bounds bounds,float level,float blend)>();var manifest=new List<object>();var preview=EditorSceneManager.NewPreviewScene();GameObject stage=null;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Exterior ruins and grouped props");
        float Ground(float x,float z)=>terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
        Transform Group(string name) {var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,preview);g.transform.SetParent(stage.transform,false);return g.transform;}
        bool Overlap(Bounds a,Bounds b,float margin=0) => a.max.x+margin>b.min.x&&a.min.x-margin<b.max.x&&a.max.z+margin>b.min.z&&a.min.z-margin<b.max.z;
        GameObject Spawn(string path,Transform parent,float x,float z,float y,float yaw,bool nativePivot=false,float scale=1) {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException("Missing prefab: "+path);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,preview);g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,yaw,0));g.transform.localScale*=scale;
            if(g.GetComponentsInChildren<MeshFilter>(true).Any(f=>!f.sharedMesh)||g.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader")))throw new InvalidOperationException("Unprepared asset: "+path);
            var b=BoundsOf(g);g.transform.position=new Vector3(x-b.center.x,nativePivot?y:y-b.min.y,z-b.center.z);b=BoundsOf(g);
            if(occupied.Any(o=>Overlap(b,o,.02f)))throw new InvalidOperationException("Exterior placement overlaps existing geometry: "+g.name+" at "+x+","+z);
            occupied.Add(b);manifest.Add(new{g.name,prefab=path,position=V(g.transform.position),yaw,scale,bounds=new{min=V(b.min),max=V(b.max)}});return g;
        }
        void Pad(Bounds b,float y,float padding,float blend) {b.Expand(new Vector3(padding*2,0,padding*2));pads.Add((b,y,blend));}
        try {
            stage=new GameObject(groupName);SceneManager.MoveGameObjectToScene(stage,preview);var ruins=Group("Artist assembled ruined buildings");
            foreach(var item in new[]{(x:-78f,z:67f,yaw:18f),(x:82f,z:-68f,yaw:152f)}) {
                float level=Ground(item.x,item.z);var g=Spawn("Assets/DBK/Prefabs/Demo/DBK_Demo_Building.prefab",ruins,item.x,item.z,level-.025f,item.yaw,true);g.name="Abandoned annex "+(ruins.childCount==1?"NW":"SE");
                foreach(var f in g.GetComponentsInChildren<MeshFilter>())if(f.name.StartsWith("Mesh_DBK_Concrete")||f.name=="DBK_Concrete_Rubble_Big"){var collider=f.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;}
                Pad(BoundsOf(g),level,.65f,8);
            }
            var barriers=Group("Native concrete barrier sections");
            var sections=new[]{(x:2.60482f-7,z:61.8f,yaw:90f,count:3),(x:2.60482f+7,z:61.8f,yaw:90f,count:3),(x:-61.7f,z:-4.24014f-7,yaw:0f,count:3),(x:-61.7f,z:-4.24014f+7,yaw:0f,count:3),(x:-8.3f,z:-49.95f,yaw:0f,count:3),(x:60f,z:-25.6f,yaw:90f,count:3),(x:82f,z:-53f,yaw:0f,count:3)};
            int barrierCount=0;
            foreach(var item in sections) {
                float level=Ground(item.x,item.z);Bounds strip=new Bounds();bool first=true;
                for(int i=0;i<item.count;i++){float d=(i-(item.count-1)*.5f)*3.72f;var along=Quaternion.Euler(0,item.yaw,0)*new Vector3(d,0,0);var g=Spawn(Folders[0]+"/PRE_Concrete Barrier_"+(1+barrierCount%3)+".prefab",barriers,item.x+along.x,item.z+along.z,level-.008f,item.yaw);g.name="Barrier "+(++barrierCount);if(first){strip=BoundsOf(g);first=false;}else strip.Encapsulate(BoundsOf(g));}
                Pad(strip,level,.65f,.9f);
            }
            var barrels=Group("Small barrel caches");int barrelCount=0;
            var caches=new[]{(x:16f,z:56.4f,toxic:false),(x:58.6f,z:-38.5f,toxic:true),(x:-56f,z:18f,toxic:false),(x:-15f,z:-53f,toxic:false),(x:-64f,z:66f,toxic:false),(x:81.8f,z:-55.3f,toxic:false)};
            var offsets=new[]{new Vector2(-.55f,-.25f),new Vector2(.42f,-.18f),new Vector2(-.15f,1.05f)};
            foreach(var cache in caches){float level=Ground(cache.x,cache.z);Bounds apron=new Bounds();bool first=true;int i=0;
                foreach(var offset in offsets){string variant=cache.toxic?"Biohazard":i%2==0?"Custom":"Military";var g=Spawn(Folders[1]+"/PRE_Barrel_"+variant+".prefab",barrels,cache.x+offset.x,cache.z+offset.y,level-.008f,37+barrelCount*71);g.name=variant+" barrel "+(++barrelCount);if(first){apron=BoundsOf(g);first=false;}else apron.Encapsulate(BoundsOf(g));i++;}
                Pad(apron,level,.65f,.9f);
            }
            Undo.RegisterCompleteObjectUndo(data,"Seat exterior structures with local terrain grading");
            int n=data.heightmapResolution;var heights=data.GetHeights(0,0,n,n);int changed=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {
                float wx=terrain.transform.position.x+x*data.size.x/(n-1),wz=terrain.transform.position.z+z*data.size.z/(n-1);
                foreach(var pad in pads){float dx=Mathf.Max(pad.bounds.min.x-wx,0,wx-pad.bounds.max.x),dz=Mathf.Max(pad.bounds.min.z-wz,0,wz-pad.bounds.max.z),distance=Mathf.Sqrt(dx*dx+dz*dz);if(distance>=pad.blend)continue;float weight=1-Mathf.SmoothStep(0,1,distance/pad.blend);heights[z,x]=Mathf.Lerp(heights[z,x],(pad.level-terrain.transform.position.y)/data.size.y,weight);changed++;}
            }
            data.SetHeights(0,0,heights);terrain.Flush();
            int an=data.alphamapResolution;var alpha=data.GetAlphamaps(0,0,an,an);
            for(int z=0;z<an;z++)for(int x=0;x<an;x++) {
                float wx=terrain.transform.position.x+x*data.size.x/(an-1),wz=terrain.transform.position.z+z*data.size.z/(an-1);
                foreach(var pad in pads){float dx=Mathf.Max(pad.bounds.min.x-wx,0,wx-pad.bounds.max.x),dz=Mathf.Max(pad.bounds.min.z-wz,0,wz-pad.bounds.max.z),distance=Mathf.Sqrt(dx*dx+dz*dz);if(distance>=pad.blend)continue;float weight=1-Mathf.SmoothStep(0,1,distance/pad.blend);for(int layer=0;layer<data.alphamapLayers;layer++)alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],layer==0?.18f:layer==1?.72f:layer==2?.10f:0,weight);}
            }
            data.SetAlphamaps(0,0,alpha);
            // Keep previously placed natural props seated where a grading falloff reaches their roots.
            foreach(var tree in root.Find("Nature").GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&t.name.StartsWith("Dry"))) {
                var p=tree.position;float y=Ground(p.x,p.z)-.15f;if(Mathf.Abs(y-p.y)>.001f){Undo.RecordObject(tree,"Keep tree roots in graded terrain");tree.position=new Vector3(p.x,y,p.z);}
            }
            var rocks=root.Find("Nature").Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("Geological outcrops"));
            if(rocks)foreach(var rock in rocks.Cast<Transform>()){var b=BoundsOf(rock.gameObject);float embed=rock.name.StartsWith("Small exposed")?.12f:Mathf.Max(b.size.x,b.size.z)*.1f;float dy=Ground(b.center.x,b.center.z)-embed-b.min.y;if(Mathf.Abs(dy)>.001f){Undo.RecordObject(rock,"Seat rock in graded terrain");rock.position+=Vector3.up*dy;}}
            var stumps=Group("Irregular stump patches");var stumpPaths=AssetDatabase.FindAssets("t:Prefab",new[]{Folders[3]}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();var random=new System.Random(20102027);int stumpCount=0;
            foreach(var center in new[]{new Vector2(-35,-69),new Vector2(-62,-87),new Vector2(65,-92),new Vector2(92,52),new Vector2(-90,13),new Vector2(-8,85)}) {
                int accepted=0;
                for(int attempt=0;attempt<400&&accepted<5;attempt++) {
                    float angle=(float)random.NextDouble()*Mathf.PI*2,radius=Mathf.Sqrt((float)random.NextDouble())*12;var p=center+new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                    if(p.x>-57&&p.x<62&&p.y>-53&&p.y<56||Mathf.Abs(p.x-2.60482f)<8&&p.y>46||Mathf.Abs(p.x-19.62226f)<8&&p.y<-43||Mathf.Abs(p.y+4.24014f)<8&&p.x<-48)continue;
                    var box=new Bounds(new Vector3(p.x,0,p.y),new Vector3(2.4f,4,2.4f));if(occupied.Any(b=>Overlap(box,b,1)))continue;
                    float nx=(p.x-terrain.transform.position.x)/data.size.x,nz=(p.y-terrain.transform.position.z)/data.size.z;if(data.GetSteepness(nx,nz)>16)continue;
                    var g=Spawn(stumpPaths[stumpCount%stumpPaths.Length],stumps,p.x,p.y,Ground(p.x,p.y)-.025f,(float)random.NextDouble()*360,true,.85f+(float)random.NextDouble()*.3f);g.name="Seated stump "+(++stumpCount);accepted++;
                }
                if(accepted!=5)throw new InvalidOperationException("Could not fit an exterior stump patch safely");
            }
            if(barrierCount!=21||barrelCount!=18||stumpCount!=30||ruins.childCount!=2||changed==0)throw new InvalidOperationException("Exterior assembly count mismatch");
            foreach(var part in barriers.Cast<Transform>().Concat(barrels.Cast<Transform>())){var b=BoundsOf(part.gameObject);foreach(float x in new[]{b.min.x,b.center.x,b.max.x})foreach(float z in new[]{b.min.z,b.center.z,b.max.z}){float gap=b.min.y-Ground(x,z);if(gap>.025f||gap<-.04f)throw new InvalidOperationException("Exterior prop is not seated: "+part.name+" gap="+gap);}}
            if(previous)Undo.DestroyObjectImmediate(previous.gameObject);SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(root,true);StageUtility.PlaceGameObjectInCurrentStage(stage);Undo.RegisterCreatedObjectUndo(stage,"Exterior dressing");
            Physics.SyncTransforms();EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/ExteriorDressing.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{ruins=2,barriers=barrierCount,barrels=barrelCount,stumps=stumpCount,gradedHeightSamples=changed,items=manifest,limits="Artist building assemblies remain native scale; obsolete combiner references repaired. Rebar and shallow scatter rubble have no added colliders. Three approach corridors remain clear of new props."},Newtonsoft.Json.Formatting.Indented));
            stage=null;Undo.CollapseUndoOperations(undo);return "Placed two native ruined annexes, 21 concrete barriers, 18 grouped barrels and 30 stumps; locally graded terrain; scene saved.";
        }catch {Undo.RevertAllDownToGroup(undo);throw;}
        finally {if(stage)UnityEngine.Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(preview);}
    }
}
