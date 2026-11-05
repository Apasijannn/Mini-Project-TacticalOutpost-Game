using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

public static class OutpostBuilder
{
    const string ScenePath = "Assets/Scenes/TacticalOutpost_Solo.unity";
    const float Pad = .18364f;
    const float RoadX = 2.60482f;
    const float RoadZ = -4.24014f;
    const float SouthX = 19.62226f;
    const float FenceW=-48.4475f, FenceE=53.65714f, FenceS=-43.9475f, FenceN=46.81218f;
    static Scene scene;
    static Transform root;
    static readonly Dictionary<string, string> paths = new Dictionary<string, string>();

    static void Init()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open TacticalOutpost_Solo first.");
        root = scene.GetRootGameObjects().Single(g => g.name == "TacticalOutpost").transform;
    }
    static Transform Group(string name) => root.Find(name) ?? Child(root, name);
    static Transform Child(Transform parent, string name)
    {
        var g = new GameObject(name); SceneManager.MoveGameObjectToScene(g, scene);
        g.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(g, "Outpost layout"); return g.transform;
    }
    static void Clear(Transform t)
    {
        foreach (var c in t.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(c.gameObject);
    }
    static string PrefabPath(string name)
    {
        if (!paths.ContainsKey(name)) paths[name] = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == name);
        return paths[name];
    }
    static Bounds BoundsOf(GameObject g)
    {
        var renderers = g.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("No mesh bounds: " + g.name);
        var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static void Seat(GameObject g, float x, float z, float bottom, float yaw)
    {
        Undo.RecordObject(g.transform, "Place on measured surface");
        g.transform.position = Vector3.zero; g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var b = BoundsOf(g); g.transform.position += new Vector3(x - b.center.x, bottom - b.min.y, z - b.center.z);
    }
    static GameObject Place(string prefab, Transform parent, float x, float z, float bottom = Pad, float yaw = 0, string name = null)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefab)), scene);
        Undo.RegisterCreatedObjectUndo(g, "Place linked prefab"); g.transform.SetParent(parent, true); g.name = name ?? prefab;
        Seat(g, x, z, bottom, yaw); return g;
    }
    static string Source(GameObject g) => Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g));
    static float DistanceBox(float x, float z, float x0, float x1, float z0, float z1)
    {
        float dx = Mathf.Max(x0 - x, 0, x - x1), dz = Mathf.Max(z0 - z, 0, z - z1); return Mathf.Sqrt(dx * dx + dz * dz);
    }
    static float Gaussian(float x, float z, float cx, float cz, float amp, float sx, float sz) => amp * Mathf.Exp(-.5f * (Mathf.Pow((x-cx)/sx,2) + Mathf.Pow((z-cz)/sz,2)));
    static float Ground(float x, float z)
    {
        float edge = Mathf.SmoothStep(0, 1, DistanceBox(x,z,-55,58,-50,50) / 27f);
        float drives = Mathf.Min(DistanceBox(x,z,-80,-40,RoadZ-5,RoadZ+5), DistanceBox(x,z,RoadX-5,RoadX+5,35,70), DistanceBox(x,z,SouthX-5,SouthX+5,-70,-35));
        edge *= Mathf.SmoothStep(0,1,drives/7f);
        return .15f + edge * (Gaussian(x,z,-95,64,9,39,33) + Gaussian(x,z,95,77,11,38,40) + Gaussian(x,z,95,-84,7,33,38) + Gaussian(x,z,-88,-90,5,40,37) + (Mathf.PerlinNoise((x+171)*.024f,(z+93)*.024f)-.5f)*.35f);
    }
    static void GradeTerrain()
    {
        var terrain = Group("World").GetComponentInChildren<Terrain>(); var d = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(d, "Grade industrial yard"); int n=d.heightmapResolution; var h=new float[n,n];
        for(int z=0;z<n;z++) for(int x=0;x<n;x++) {float wx=terrain.transform.position.x+x*d.size.x/(n-1),wz=terrain.transform.position.z+z*d.size.z/(n-1);h[z,x]=(Ground(wx,wz)-terrain.transform.position.y)/d.size.y;}
        d.SetHeights(0,0,h);
        int an=d.alphamapResolution; var a=new float[an,an,d.terrainLayers.Length];
        for(int z=0;z<an;z++)for(int x=0;x<an;x++) {float wx=terrain.transform.position.x+x*d.size.x/(an-1),wz=terrain.transform.position.z+z*d.size.z/(an-1);float skirt=1-Mathf.SmoothStep(0,1,DistanceBox(wx,wz,-50,52.1f,-45.5f,45.26f)/12);float gravel=.20f+.62f*skirt;float rock=Mathf.Clamp01((d.GetSteepness(x/(float)(an-1),z/(float)(an-1))-23)/30)*.6f;a[z,x,0]=(1-gravel)*(1-rock);a[z,x,1]=gravel*(1-rock);a[z,x,2]=rock;}
        d.SetAlphamaps(0,0,a); EditorUtility.SetDirty(d); terrain.Flush();
    }
    public static string Foundation()
    {
        Init(); Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Outpost: continuous ground and roads");
        var roads=Group("RoadsAndDecks");Clear(roads);var yard=Child(roads,"Continuous asphalt yard • ground level");var lanes=Child(roads,"6.94m traffic lanes • native curbs");
        for(int i=-4;i<=4;i++)for(int j=-3;j<=4;j++) {
            bool eastWest=j==0&&i!=0&&i!=2, northSouth=(i==0&&j>0)||(i==2&&j<0);
            Place(eastWest||northSouth?"Road_set_v1_s_tile_1":"Road_set_v1_s_floor",eastWest||northSouth?lanes:yard,RoadX+i*10,RoadZ+j*10,Pad,northSouth?90:0);
        }
        for(int i=-7;i<=-5;i++)Place("Road_set_v1_s_tile_1",lanes,RoadX+i*10,RoadZ);
        for(int j=5;j<=6;j++)Place("Road_set_v1_s_tile_1",lanes,RoadX,RoadZ+j*10,Pad,90);
        Place("Road_set_v1_s_floor",yard,RoadX+10,RoadZ-40);Place("Road_set_v1_s_floor",yard,RoadX+20,RoadZ-40);
        for(int j=5;j<=6;j++)Place("Road_set_v1_s_floor",yard,SouthX,RoadZ-j*10);
        GradeTerrain();
        foreach(var t in Group("Buildings").Cast<Transform>().ToArray()) {
            switch(Source(t.gameObject)) {
                case "Hangar_v2": Seat(t.gameObject,-29,25,Pad,180); t.name="Logistics warehouse • complete artist prefab";break;
                case "Hangar_v3": Seat(t.gameObject,30,25,Pad,180);t.name="Command and barracks • complete artist prefab";break;
                case "Hangar_v1_full": Seat(t.gameObject,-35,-25,Pad,90);t.name="Maintenance shed • complete artist prefab";break;
                case "Hangar_v4": Seat(t.gameObject,42,-27,Pad,0);t.name="Power station • complete artist prefab";break;
            }
        }
        RemoveOuterFence(); FillPerimeter();
        foreach(var t in Group("GameplayMarkers").GetComponentsInChildren<Transform>().Where(t=>t!=Group("GameplayMarkers"))) {Undo.RecordObject(t,"Ground markers");var p=t.position;p.y=Pad;t.position=p;}
        var player=Group("Player").GetComponentInChildren<CharacterController>();Undo.RecordObject(player.transform,"Ground player");player.transform.position=new Vector3(31,Pad,2.5f);player.transform.rotation=Quaternion.Euler(0,270,0);
        var sun=Group("Lighting").GetComponentInChildren<Light>();Undo.RecordObject(sun,"Neutral afternoon light");Undo.RecordObject(sun.transform,"Afternoon sun");sun.transform.rotation=Quaternion.Euler(38,-42,0);sun.color=new Color(1,.94f,.84f);sun.intensity=1.45f;
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.46f,.54f,.65f);RenderSettings.ambientEquatorColor=new Color(.43f,.43f,.39f);RenderSettings.ambientGroundColor=new Color(.23f,.22f,.20f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.63f,.66f,.67f);RenderSettings.fogDensity=.0008f;
        EditorSceneManager.MarkSceneDirty(scene);
        var bad=roads.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).Count(t=>Ground(t.position.x,t.position.z)>=Pad-.01f);
        if(bad!=0)throw new InvalidOperationException("Terrain occludes "+bad+" paving modules");
        var view=SceneView.lastActiveSceneView;view.LookAt(new Vector3(1,2,0),Quaternion.Euler(52,45,0),67,true);view.showGrid=false;view.drawGizmos=false;view.Repaint();
        return "Foundation complete: 81 native paving modules, datum "+Pad+"m, terrain clearance >1cm, scene not yet saved.";
    }
    static void WallLine(Transform parent, Vector2 start, Vector2 direction, int spans, HashSet<string> posts)
    {
        const float pitch=4.6565f;
        for(int i=0;i<=spans;i++) {
            var p=start+direction*(pitch*i);string key=Mathf.RoundToInt(p.x*1000)+":"+Mathf.RoundToInt(p.y*1000);
            if(posts.Add(key))Place("Concrete_fence_v1_column",parent,p.x,p.y);
            if(i<spans){var c=p+direction*(pitch*.5f);Place(i%2==0?"Concrete_fence_v1_s1":"Concrete_fence_v1_s2",parent,c.x,c.y,Pad,direction.x==0?0:90);}
        }
    }
    public static string RemoveOuterFence()
    {
        Init();var p=Group("Perimeter");int removed=0;
        foreach(var t in p.Cast<Transform>().Where(t=>t.name.StartsWith("Continuous perimeter")).ToArray()){removed+=t.GetComponentsInChildren<Transform>(true).Count(o=>PrefabUtility.IsOutermostPrefabInstanceRoot(o.gameObject));Undo.DestroyObjectImmediate(t.gameObject);}
        EditorSceneManager.MarkSceneDirty(scene);return removed+" outer fence modules removed; building compound walls retained.";
    }
    public static string OuterFence()
    {
        Init();Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Outer fence with open road entrances");
        try {
            RemoveOuterFence();var f=Child(Group("Perimeter"),"Continuous perimeter • open entrances, no gates");
            const float pitch=5.67248f,socket=1.283608f,baseY=.15f;
            // Concrete feet embed in the yard edge and meet the surrounding terrain.
            Place("Concrete_fence_v2_L",f,FenceW+socket,FenceN-socket,baseY,0,"NW connected corner");
            Place("Concrete_fence_v2_L",f,FenceE-socket,FenceN-socket,baseY,90,"NE connected corner");
            Place("Concrete_fence_v2_L",f,FenceE-socket,FenceS+socket,baseY,180,"SE connected corner");
            Place("Concrete_fence_v2_L",f,FenceW+socket,FenceS+socket,baseY,270,"SW connected corner");
            for(int i=1;i<=17;i++){
                if(i!=9)Place("Concrete_fence_v2_S",f,FenceW+i*pitch,FenceN,baseY,90,"North span "+i);
                if(i!=12)Place("Concrete_fence_v2_S",f,FenceW+i*pitch,FenceS,baseY,90,"South span "+i);
            }
            for(int i=1;i<=15;i++){
                if(i!=7)Place("Concrete_fence_v2_S",f,FenceW,FenceS+i*pitch,baseY,0,"West span "+i);
                Place("Concrete_fence_v2_S",f,FenceE,FenceS+i*pitch,baseY,0,"East span "+i);
            }
            if(f.childCount!=65||f.Cast<Transform>().Any(t=>Source(t.gameObject).Contains("Gate")))throw new InvalidOperationException("Fence count/gate validation failed");
            if(f.Cast<Transform>().Any(t=>Mathf.Abs(BoundsOf(t.gameObject).min.y-baseY)>.001f||Vector3.Distance(t.lossyScale,Vector3.one)>.001f))throw new InvalidOperationException("Fence base or scale mismatch");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);
            return "65 native fence modules, four connected corners, three open entrances, zero gates.";
        }catch {Undo.RevertAllDownToGroup(undo);throw;}
    }
    public static string AssetGallery()
    {
        Init();const string path="Assets/Scenes/Outpost_AssetGallery.unity";
        if(File.Exists(path))throw new InvalidOperationException("Gallery already exists; edit its existing objects instead of duplicating them");
        var gallery=EditorSceneManager.NewPreviewScene();
        try {
        var galleryRoot=new GameObject("ASSET GALLERY • select prefab and duplicate into the map");SceneManager.MoveGameObjectToScene(galleryRoot,gallery);galleryRoot.tag="EditorOnly";
        var exclude=galleryRoot.AddComponent<NavMeshModifier>();exclude.ignoreFromBuild=true;exclude.applyToChildren=true;
        var terrain=Group("World").GetComponentInChildren<Terrain>();var terrainPos=terrain.transform.position;var td=terrain.terrainData;
        var padMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Outpost_Concrete_Foundation.mat");var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var items=new List<(string category,GameObject asset)>();
        Bounds DisplayBounds(GameObject obj){var renderers=obj.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new InvalidOperationException("No renderer: "+obj.name);var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;}
        foreach(var assetPath in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/RPG_FPS_game_assets_industrial"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p)){
            string category=assetPath.Contains("/Pipes/")?"Pipes":assetPath.Split('/')[2];var asset=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),gallery);foreach(var effect in asset.GetComponentsInChildren<ParticleSystem>()){effect.Simulate(2,true,true,true);effect.Pause(true);category="Effects";}items.Add((category,asset));
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Models/namaqualand_boulders_01_2k.fbx");
        foreach(var f in model.GetComponentsInChildren<MeshFilter>()){
            var g=new GameObject(f.name);SceneManager.MoveGameObjectToScene(g,gallery);var part=new GameObject("Scanned mesh");SceneManager.MoveGameObjectToScene(part,gallery);part.transform.SetParent(g.transform,false);part.transform.localRotation=f.transform.rotation;part.transform.localScale=f.transform.lossyScale;part.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;part.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Namaqualand_Boulders.mat");var bounds=BoundsOf(g);g.transform.localScale*=3/Mathf.Max(bounds.size.x,bounds.size.z);items.Add(("Nature",g));
        }
        float x=88,z=-70,rowDepth=0;int count=0;string previous=null;Transform categoryRoot=null;var placed=new List<Bounds>();var manifest=new List<object>();
        foreach(var item in items.OrderBy(i=>i.category).ThenBy(i=>i.asset.name)){
            if(previous!=item.category){if(previous!=null)z+=rowDepth+5;x=88;rowDepth=0;var group=new GameObject(item.category);SceneManager.MoveGameObjectToScene(group,gallery);group.transform.SetParent(galleryRoot.transform,false);categoryRoot=group.transform;previous=item.category;}
            var g=item.asset;g.transform.SetParent(categoryRoot,true);g.transform.rotation=Quaternion.identity;var b=DisplayBounds(g);float width=Mathf.Max(8,b.size.x+2.5f),depth=Mathf.Max(8,b.size.z+4);
            if(x+width>193&&x>88){x=88;z+=rowDepth+3;rowDepth=0;}
            float minHeight=Pad,maxHeight=Pad;
            for(int ix=0;ix<=8;ix++)for(int iz=0;iz<=8;iz++){
                float wx=x+width*ix/8,wz=z+depth*iz/8;float h=Pad;
                if(wx>=terrainPos.x&&wx<=terrainPos.x+td.size.x&&wz>=terrainPos.z&&wz<=terrainPos.z+td.size.z)h=terrainPos.y+td.GetInterpolatedHeight((wx-terrainPos.x)/td.size.x,(wz-terrainPos.z)/td.size.z);
                minHeight=Mathf.Min(minHeight,h);maxHeight=Mathf.Max(maxHeight,h);
            }
            float top=maxHeight+.08f;g.transform.position+=new Vector3(x+width*.5f-b.center.x,top-b.min.y,z+2.8f+b.size.z*.5f-b.center.z);g.name=(++count).ToString("D3")+" • "+g.name;
            var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(pad,gallery);pad.name="Display plinth "+count;pad.transform.SetParent(categoryRoot,false);pad.transform.position=new Vector3(x+width*.5f,(top+minHeight-.2f)*.5f,z+depth*.5f);pad.transform.localScale=new Vector3(width-.5f,top-minHeight+.2f,depth-.5f);pad.GetComponent<Renderer>().sharedMaterial=padMaterial;
            var label=new GameObject("Label "+g.name);SceneManager.MoveGameObjectToScene(label,gallery);label.transform.SetParent(categoryRoot,false);label.transform.SetPositionAndRotation(new Vector3(x+.65f,top+.02f,z+.55f),Quaternion.Euler(90,0,0));var text=label.AddComponent<TextMesh>();text.font=font;text.fontSize=48;text.characterSize=.22f;text.anchor=TextAnchor.LowerLeft;text.text=g.name;text.color=new Color(.95f,.93f,.8f);label.GetComponent<Renderer>().sharedMaterial=font.material;
            float labelWidth=label.GetComponent<Renderer>().bounds.size.x;if(labelWidth>width-1)text.characterSize*=(width-1)/labelWidth;
            b=DisplayBounds(g);if(placed.Any(p=>p.Intersects(b)))throw new InvalidOperationException("Gallery bounds overlap: "+g.name);placed.Add(b);manifest.Add(new{index=count,category=item.category,name=g.name,position=g.transform.position.ToString(),min=b.min.ToString(),max=b.max.ToString(),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)});
            x+=width+1.5f;rowDepth=Mathf.Max(rowDepth,depth);
        }
        var mainBounds=root.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();if(placed.Any(p=>mainBounds.Any(b=>b.Intersects(p))))throw new InvalidOperationException("Gallery intersects an existing map prop");
        var savedScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);foreach(var obj in gallery.GetRootGameObjects())SceneManager.MoveGameObjectToScene(obj,savedScene);
        if(!EditorSceneManager.SaveScene(savedScene,path))throw new InvalidOperationException("Gallery save failed");SceneManager.SetActiveScene(scene);File.WriteAllText("Temp/OutpostAssetGallery.json",Newtonsoft.Json.JsonConvert.SerializeObject(manifest,Newtonsoft.Json.Formatting.Indented));
        var view=SceneView.lastActiveSceneView;view.LookAt(new Vector3(135,5,z*.5f),Quaternion.Euler(55,35,0),Mathf.Max(60,(z+80)*.55f),true,true);view.Repaint();
        return CompactGallery();
        } finally {EditorSceneManager.ClosePreviewScene(gallery);}
    }
    public static string CompactGallery()
    {
        Init();var gallery=SceneManager.GetSceneByPath("Assets/Scenes/Outpost_AssetGallery.unity");var galleryRoot=gallery.GetRootGameObjects().Single().transform;
        Bounds DisplayBounds(GameObject obj){var rr=obj.GetComponentsInChildren<Renderer>(true);var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
        var items=galleryRoot.Cast<Transform>().SelectMany(category=>category.Cast<Transform>().Where(t=>char.IsDigit(t.name[0]))).Select(t=>new{t,b=DisplayBounds(t.gameObject)}).OrderByDescending(o=>o.b.size.z).ThenByDescending(o=>o.b.size.x).ToArray();
        float x=134,z=-75,rowDepth=0;var occupied=new List<Bounds>();var manifest=new List<object>();var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Outpost_Concrete_Foundation.mat");
        foreach(var item in items){var t=item.t;var b=item.b;int id=int.Parse(t.name.Substring(0,3));float width=Mathf.Max(4,b.size.x+2.5f),depth=Mathf.Max(4,b.size.z+3.5f);if(x+width>304&&x>134){x=134;z+=rowDepth+2;rowDepth=0;}
            var delta=new Vector3(x+width*.5f-b.center.x,Pad+.08f-b.min.y,z+2.3f+b.size.z*.5f-b.center.z);Undo.RecordObject(t,"Compact asset gallery");t.position+=delta;
            var pad=t.parent.Find("Display plinth "+id);pad.position=new Vector3(x+width*.5f,Pad-.02f,z+depth*.5f);pad.localScale=new Vector3(width-.5f,.2f,depth-.5f);
            var label=t.parent.Find("Label "+t.name);if(label){label.position=new Vector3(x+.65f,Pad+.10f,z+.45f);var text=label.GetComponent<TextMesh>();float labelWidth=label.GetComponent<Renderer>().bounds.size.x;if(labelWidth>width-1)text.characterSize*=(width-1)/labelWidth;}
            b.center+=delta;if(occupied.Any(o=>o.Intersects(b)))throw new InvalidOperationException("Catalog overlap: "+t.name);occupied.Add(b);manifest.Add(new{index=id,category=t.parent.name,name=t.name,position=t.position.ToString(),min=b.min.ToString(),max=b.max.ToString(),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)});
            foreach(var effect in t.GetComponentsInChildren<ParticleSystem>())effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            x+=width+1.5f;rowDepth=Mathf.Max(rowDepth,depth);
        }
        var floor=galleryRoot.Find("Gallery ground");if(!floor){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(g,gallery);g.name="Gallery ground";g.transform.SetParent(galleryRoot,false);floor=g.transform;}
        floor.position=new Vector3(219,Pad-.06f,(-75+z+rowDepth)*.5f);floor.localScale=new Vector3(176,.2f,z+rowDepth+79);floor.GetComponent<Renderer>().sharedMaterial=material;
        GalleryLabels();EditorSceneManager.MarkSceneDirty(gallery);if(!EditorSceneManager.SaveScene(gallery))throw new InvalidOperationException("Gallery save failed");File.WriteAllText("Temp/OutpostAssetGallery.json",Newtonsoft.Json.JsonConvert.SerializeObject(manifest,Newtonsoft.Json.Formatting.Indented));
        var view=SceneView.lastActiveSceneView;view.LookAt(new Vector3(219,3,(-75+z+rowDepth)*.5f),Quaternion.Euler(60,0,0),Mathf.Max(85,(z+rowDepth+79)*.55f),true,true);view.Repaint();
        return items.Length+" labeled assets on a compact flat gallery pad east of the terrain; no overlapping model bounds. FX emitters can be previewed on selection.";
    }
    public static string GalleryLabels()
    {
        Init();var gallery=SceneManager.GetSceneByPath("Assets/Scenes/Outpost_AssetGallery.unity");var root=gallery.GetRootGameObjects().Single().transform;
        foreach(var text in root.GetComponentsInChildren<TextMesh>().ToArray())UnityEngine.Object.DestroyImmediate(text.gameObject);
        var existing=root.Find("World-space asset labels");if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var board=new GameObject("World-space asset labels",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(board,gallery);board.transform.SetParent(root,false);board.transform.SetPositionAndRotation(new Vector3(219,Pad+.10f,8),Quaternion.Euler(90,0,0));board.transform.localScale=Vector3.one*.02f;board.GetComponent<RectTransform>().sizeDelta=new Vector2(9000,9000);board.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        int count=0;foreach(var category in root.Cast<Transform>().Where(t=>t!=board.transform&&t.childCount>0))foreach(var t in category.Cast<Transform>().Where(t=>char.IsDigit(t.name[0]))){
            int id=int.Parse(t.name.Substring(0,3));var pad=category.Find("Display plinth "+id);var bounds=pad.GetComponent<Renderer>().bounds;
            var g=new GameObject("Label "+t.name,typeof(RectTransform),typeof(UnityEngine.UI.Text));SceneManager.MoveGameObjectToScene(g,gallery);g.transform.SetParent(board.transform,false);var rect=g.GetComponent<RectTransform>();rect.pivot=Vector2.zero;rect.sizeDelta=new Vector2((pad.localScale.x-.8f)/.02f,1.5f/.02f);rect.position=new Vector3(bounds.min.x+.4f,Pad+.10f,bounds.min.z+.2f);
            var text=g.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=32;text.resizeTextForBestFit=true;text.resizeTextMinSize=6;text.resizeTextMaxSize=32;text.alignment=TextAnchor.LowerLeft;text.color=new Color(.95f,.93f,.8f);text.text=t.name;text.raycastTarget=false;count++;
        }
        EditorSceneManager.MarkSceneDirty(gallery);return count+" depth-tested world-space labels; text no longer shows through the main map buildings.";
    }
    static void EnsureFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder))return;string parent=Path.GetDirectoryName(folder).Replace("\\","/");EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }
    static void Infill(Transform parent,string name,float x0,float x1,float z0,float z1,Material material)
    {
        string path="Assets/TacticalOutpost/Models/Foundations/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}else{Undo.RegisterCompleteObjectUndo(mesh,"Close yard edges");mesh.Clear();}
        mesh.vertices=new[]{new Vector3(x0,Pad,z0),new Vector3(x0,Pad,z1),new Vector3(x1,Pad,z1),new Vector3(x1,Pad,z0)};mesh.triangles=new[]{0,1,2,0,2,3};
        mesh.uv=mesh.vertices.Select(v=>new Vector2((v.x-RoadX+5)/10,(v.z-RoadZ+5)/10)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var g=Child(parent,name).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;g.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static void FillPerimeter()
    {
        EnsureFolder("Assets/TacticalOutpost/Models/Foundations");var roads=Group("RoadsAndDecks");var p=roads.Find("Solid foundation and boundary infill")??Child(roads,"Solid foundation and boundary infill");Clear(p);
        var mat=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_floor")).GetComponentInChildren<Renderer>().sharedMaterial;
        float x0=RoadX-45,x1=RoadX+45,z0=RoadZ-35,z1=RoadZ+45;
        Infill(p,"West_south_infill",FenceW,x0,FenceS,RoadZ-5,mat);Infill(p,"West_north_infill",FenceW,x0,RoadZ+5,FenceN,mat);
        Infill(p,"East_infill",x1,FenceE,FenceS,FenceN,mat);
        Infill(p,"North_west_infill",x0,RoadX-5,z1,FenceN,mat);Infill(p,"North_east_infill",RoadX+5,x1,z1,FenceN,mat);
        Infill(p,"South_west_infill",x0,RoadX+5,FenceS,z0,mat);Infill(p,"South_east_infill",RoadX+25,x1,FenceS,z0,mat);
        string mp="Assets/TacticalOutpost/Materials/Outpost_Concrete_Foundation.mat";var concrete=AssetDatabase.LoadAssetAtPath<Material>(mp);
        if(!concrete){concrete=new Material(Shader.Find("Universal Render Pipeline/Lit"));concrete.color=new Color(.36f,.35f,.32f);concrete.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(concrete,mp);}
        var slab=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(slab,"Solid yard foundation");slab.name="Solid concrete footing • no open underside";slab.transform.SetParent(p,false);slab.transform.position=new Vector3((FenceW+FenceE)*.5f,.09f,(FenceN+FenceS)*.5f);slab.transform.localScale=new Vector3(FenceE-FenceW,.13f,FenceN-FenceS);slab.GetComponent<Renderer>().sharedMaterial=concrete;
    }
    public static string Closure()
    {
        Init();Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Outpost: foundation and perimeter");FillPerimeter();OuterFence();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);return "Solid foundation and outer fence with three open road entrances; no gates.";
    }
    static void Stock(Transform parent,float x,float z,int kind)
    {
        var bay=Child(parent,"Stored pallet • "+x+", "+z);
        if(kind==0){Place("Bags_on_pallet_v1_1",bay,x,z);return;}
        if(kind==1){Place("Palet_v1_set",bay,x,z);return;}
        var pallet=Place("Palet_v1_single",bay,x,z);float top=BoundsOf(pallet).max.y;
        if(kind==2){Place("Barrel_v2_quadro",bay,x,z,top);return;}
        Place("Wooden_box_v1_LD1square",bay,x-.415f,z,top);Place("Wooden_box_v1_LD1square",bay,x+.415f,z,top);
        if(kind==4)Place("Wooden_box_v1_LD1square",bay,x,z,top+.81207f);
    }
    static GameObject Native(string prefab,Transform parent,Vector3 position,Quaternion rotation)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefab)),scene);
        Undo.RegisterCreatedObjectUndo(g,"Artist assembly");g.transform.SetParent(parent,true);g.transform.SetPositionAndRotation(position,rotation);return g;
    }
    public static string Occupation()
    {
        Init();Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Outpost: functional compounds and cargo");
        var buildings=Group("Buildings");var additional=buildings.Find("Additional service buildings")??Child(buildings,"Additional service buildings");Clear(additional);
        Place("Hangar_v1_full",additional,-37,6.5f,Pad,90,"West checkpoint office • complete prefab");
        Place("Hangar_v1_full",additional,-30,-35.6f,Pad,0,"Maintenance spares store • complete prefab");
        var cargo=Group("Cargo");Clear(cargo);
        var west=Child(cargo,"Logistics stacks • 8m hull, 3m stacking pitch");
        foreach(float x in new[]{-13f,-8f})foreach(float z in new[]{21f,31f})Place("Cargo_container_v1_LD1close",west,x,z,Pad,90);
        Place("Cargo_container_v1_LD2close",west,-8,31,Pad+3,90);
        var east=Child(cargo,"Command reserve cargo • clear central approach");
        Place("Cargo_container_v1_LD2close",east,12.5f,22,Pad,90);Place("Cargo_container_v1_LD1close",east,12.5f,32,Pad,90);Place("Cargo_container_v1_LD1close",east,12.5f,32,Pad+3,90);
        Place("Cargo_container_v1_LD2close",east,42,22,Pad,90);Place("Cargo_container_v1_LD1close",east,42,22,Pad+3,90);
        Place("Cargo_container_v1_LD2close",cargo,34,43.5f,Pad,0,"Rear perimeter reserve container");Place("Cargo_container_v1_LD1close",cargo,-5,-14,Pad,0,"Fuel maintenance parts container");Place("Cargo_container_v1_LD2close",cargo,-22,4,Pad,0,"Warehouse dispatch queue container");
        Place("Cargo_container_v1_LD1open",cargo,-12,7,Pad,0,"Open inbound container • doors have clearance");
        Place("Cargo_container_v1_LD2open",cargo,-26,-15,Pad,0,"Open maintenance container • spares receiving");
        var utilities=Group("Utilities");Clear(utilities);
        var fuel=Child(utilities,"Fuel farm • artist tank and manifold assembly");
        // Map_v1 authored tank/manifold offsets stay together so their pipe ports still meet.
        var delta=new Vector3(-36.925f,Pad-1.630823f,14.59f);
        Native("Oil_tank_v1",fuel,new Vector3(23.91f,4.84f,-43.59f)+delta,Quaternion.identity);
        Native("Oil_tank_v1",fuel,new Vector3(36.94f,4.84f,-43.59f)+delta,Quaternion.identity);
        Native("Pipes_set_v1_V_set_v2",fuel,new Vector3(19.48f,4.44f,-40.01f)+delta,Quaternion.Euler(0,45.44098f,0));
        Native("Pipes_set_v1_V_set_v2",fuel,new Vector3(32.52f,4.46f,-40.01f)+delta,Quaternion.Euler(0,38.27768f,0));
        foreach(float x in new[]{.2f,2.4f,4.6f,6.8f})Place("Oil_tank_v2",fuel,x,-36);
        var power=Child(utilities,"Power station service bank");
        foreach(float z in new[]{-18f,-23f,-28f})Place("Generator_v1",power,32,z,Pad,90);
        Place("Electric_box_v3",power,32,-34);
        var workshop=Child(utilities,"Maintenance test generators");Place("Generator_v1",workshop,-39,-15);Place("Generator_v1",workshop,-34,-15);
        var command=Child(utilities,"Objective • command power relay");Place("Generator_v1",command,31,8);Place("Generator_v1",command,35,8);Place("Electric_box_v3",command,40,8);
        var perimeter=Group("Perimeter");var walls=perimeter.Find("Functional compound walls")??Child(perimeter,"Functional compound walls");Clear(walls);
        var fuelWalls=Child(walls,"Fuel containment • 13.97m service opening");var posts=new HashSet<string>();
        WallLine(fuelWalls,new Vector2(-21,-39),Vector2.right,7,posts);WallLine(fuelWalls,new Vector2(-21,-39),Vector2.up,4,posts);WallLine(fuelWalls,new Vector2(11.5955f,-39),Vector2.up,4,posts);
        WallLine(fuelWalls,new Vector2(-21,-20.374f),Vector2.right,2,posts);WallLine(fuelWalls,new Vector2(2.2825f,-20.374f),Vector2.right,2,posts);
        var warehouseWalls=Child(walls,"Warehouse service boundary");posts=new HashSet<string>();WallLine(warehouseWalls,new Vector2(-44.5f,39),Vector2.right,6,posts);WallLine(warehouseWalls,new Vector2(-44.5f,39),Vector2.down,5,posts);
        var commandWalls=Child(walls,"Command service boundary");posts=new HashSet<string>();WallLine(commandWalls,new Vector2(18.5f,40),Vector2.right,6,posts);WallLine(commandWalls,new Vector2(46.439f,40),Vector2.down,5,posts);
        var powerWall=Child(walls,"Power station rear boundary");WallLine(powerWall,new Vector2(29,-40),Vector2.right,4,new HashSet<string>());
        var props=Group("Props");Clear(props);
        var receiving=Child(props,"Warehouse receiving and sorted stock");
        Stock(receiving,-19,11,0);Stock(receiving,-23,11,4);Stock(receiving,-27,11,0);Stock(receiving,-30,8,3);Stock(receiving,-24,7,2);Stock(receiving,-20,7,1);
        Stock(receiving,-34,37.3f,1);Stock(receiving,-29,37.3f,3);Stock(receiving,-24,37.3f,2);Place("Dumpsters_v1_garbadge",receiving,-40.5f,37.4f);
        var cargoStock=Child(props,"Cargo staging • outside doors and traffic lane");Stock(cargoStock,-15,13,3);Stock(cargoStock,-11,13,2);Stock(cargoStock,-8,13,1);Stock(cargoStock,-12,37.5f,4);Stock(cargoStock,12.5f,38.5f,1);Stock(cargoStock,17,20,3);Stock(cargoStock,17,30,2);
        var maintenance=Child(props,"Maintenance courtyard • parts and waste");Stock(maintenance,-29,-22,4);Stock(maintenance,-26,-22,2);Stock(maintenance,-26,-26,3);Stock(maintenance,-39,-32,2);Stock(maintenance,-36,-35.5f,1);Place("Dumpsters_v1_empty",maintenance,-46,-30,Pad,90);
        var fuelStock=Child(props,"Fuel service stock");Stock(fuelStock,-17,-36.5f,1);Stock(fuelStock,7,-23,2);Stock(fuelStock,-18,-17,3);Stock(fuelStock,-13,-17,2);
        var powerStock=Child(props,"Power service spares");Stock(powerStock,30.5f,-37,3);Stock(powerStock,33.5f,-37,1);Stock(powerStock,33,-12,2);
        var commandStock=Child(props,"Command supply reserve");Stock(commandStock,19,10,4);Stock(commandStock,18,17,0);Stock(commandStock,41,35,3);Stock(commandStock,41,30,2);Stock(commandStock,26,38,1);
        var defenses=Child(props,"Defensive chicanes • traffic gaps stay open");
        Place("Road_block_v1",defenses,-40,-6.2f,Pad,90);Place("Road_block_v1",defenses,-28,-2.5f,Pad,90);
        Place("Road_block_v1",defenses,1,35);Place("Road_block_v1",defenses,4,24);
        Place("Road_block_v1",defenses,21.1f,-30);Place("Road_block_v1",defenses,20.5f,-18);
        Place("Road_block_v1",defenses,27,4);Place("Road_block_v1",defenses,38,4);
        Place("Road_block_v1",defenses,-16.5f,2,Pad,15);Place("Road_block_v1",defenses,-32,2);
        Place("Road_block_v1",defenses,-46,-2.4f,Pad,90);Place("Road_block_v1",defenses,-34,-6.2f,Pad,90);Place("Road_block_v1",defenses,-17,-6.2f,Pad,90);
        Place("Road_block_v1",defenses,4,44);Place("Road_block_v1",defenses,1,14);Place("Road_block_v1",defenses,17.8f,-12);Place("Road_block_v1",defenses,15.5f,-46);Place("Road_block_v1",defenses,25,11);
        EditorSceneManager.MarkSceneDirty(scene);
        return "Occupation complete: 6 complete buildings, 15 containers, 18 barriers, authored fuel ports, shared-post walls and sorted stock bays.";
    }
    public static string SouthAlignment()
    {
        Init();var roads=Group("RoadsAndDecks");var caps=roads.Find("Aligned south service lane paving")??Child(roads,"Aligned south service lane paving");Clear(caps);
        var material=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_floor")).GetComponentInChildren<Renderer>().sharedMaterial;
        for(int j=-3;j<0;j++){
            float z=RoadZ+j*10;
            var tile=roads.GetComponentsInChildren<Transform>().Single(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&Source(t.gameObject)=="Road_set_v1_s_tile_1"&&Mathf.Abs(t.position.z-z)<.01f&&t.position.x>14&&t.position.x<25);
            Seat(tile.gameObject,SouthX,z,Pad,90);
            foreach(var t in roads.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&Source(t.gameObject)=="Road_set_v1_s_floor"&&Mathf.Abs(t.position.x-(RoadX+10))<.01f&&Mathf.Abs(t.position.z-z)<.01f).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
            Infill(caps,"South_lane_west_"+(-j),RoadX+5,SouthX-5,z-5,z+5,material);
            Infill(caps,"South_lane_east_"+(-j),SouthX+5,RoadX+25,z-5,z+5,material);
        }
        EditorSceneManager.MarkSceneDirty(scene);return "South lane centered on gate with native road widths and disjoint side infill.";
    }
    public static string Junctions()
    {
        Init();EnsureFolder("Assets/TacticalOutpost/Models/Foundations");
        var ps=EditorSceneManager.NewPreviewScene();Mesh mesh;Material material;
        try {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_tile_1")),ps);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var f=g.GetComponentsInChildren<MeshFilter>().First();var src=f.sharedMesh;material=f.GetComponent<Renderer>().sharedMaterial;var vv=src.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray();float low=vv.Min(p=>p.y);
            var ids=new List<int>();var ts=src.triangles;
            for(int i=0;i<ts.Length;i+=3){int a=ts[i],b=ts[i+1],c=ts[i+2];if(Mathf.Abs(vv[a].y-low)<.0001f&&Mathf.Abs(vv[b].y-low)<.0001f&&Mathf.Abs(vv[c].y-low)<.0001f&&Vector3.Cross(vv[b]-vv[a],vv[c]-vv[a]).y>0){ids.Add(a);ids.Add(b);ids.Add(c);}}
            if(ids.Count==0)throw new InvalidOperationException("Road asphalt surface not found");
            var unique=ids.Distinct().ToArray();float x0=unique.Min(i=>vv[i].x),x1=unique.Max(i=>vv[i].x),z0=unique.Min(i=>vv[i].z),z1=unique.Max(i=>vv[i].z);
            string path="Assets/TacticalOutpost/Models/Foundations/Native_road_junction.asset";mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh{name="Native asphalt junction"};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.vertices=unique.Select(i=>new Vector3((vv[i].x-(x0+x1)*.5f)*10/(x1-x0),0,(vv[i].z-(z0+z1)*.5f)*10/(z1-z0))).ToArray();mesh.uv=unique.Select(i=>src.uv[i]).ToArray();mesh.triangles=ids.Select(i=>Array.IndexOf(unique,i)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        } finally {EditorSceneManager.ClosePreviewScene(ps);}
        var roads=Group("RoadsAndDecks");var junctions=roads.Find("Continuous native asphalt intersections")??Child(roads,"Continuous native asphalt intersections");Clear(junctions);
        var spots=new[]{new Vector2(RoadX,RoadZ),new Vector2(RoadX+10,RoadZ),new Vector2(RoadX+20,RoadZ),new Vector2(RoadX+10,RoadZ-40),new Vector2(RoadX+20,RoadZ-40),new Vector2(SouthX,RoadZ-50),new Vector2(SouthX,RoadZ-60)};
        foreach(var spot in spots){foreach(var t in roads.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&(Source(t.gameObject)=="Road_set_v1_s_floor"||Source(t.gameObject)=="Road_set_v1_s_tile_1")&&Vector2.Distance(new Vector2(t.position.x,t.position.z),spot)<.01f).ToArray())Undo.DestroyObjectImmediate(t.gameObject);var g=Child(junctions,"Asphalt connection").gameObject;g.transform.position=new Vector3(spot.x,Pad,spot.y);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;g.AddComponent<MeshCollider>().sharedMesh=mesh;}
        EditorSceneManager.MarkSceneDirty(scene);return Crossroads();
    }
    struct RoadVertex
    {
        public Vector3 position, normal;
        public Vector2 uv;
    }
    static List<RoadVertex> ClipRoad(List<RoadVertex> polygon, float edge, bool keepRight)
    {
        var result=new List<RoadVertex>();
        for(int i=0;i<polygon.Count;i++){
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];
            bool insideA=keepRight?a.position.x>=edge:a.position.x<=edge,insideB=keepRight?b.position.x>=edge:b.position.x<=edge;
            if(insideA)result.Add(a);
            if(insideA!=insideB){float t=(edge-a.position.x)/(b.position.x-a.position.x);result.Add(new RoadVertex{position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)});}
        }
        return result;
    }
    static void RoadConnector(Transform parent,string name,float x0,float x1,MeshFilter source)
    {
        var mesh=source.sharedMesh;if(mesh.subMeshCount!=1||mesh.uv.Length!=mesh.vertexCount||mesh.normals.Length!=mesh.vertexCount)throw new InvalidOperationException("Unexpected road vertex layout");
        var positions=mesh.vertices.Select(source.transform.TransformPoint).ToArray();var normals=mesh.normals.Select(source.transform.TransformDirection).ToArray();var uvs=mesh.uv;
        float left=positions.Min(v=>v.x),right=left+x1-x0,bottom=positions.Min(v=>v.y);var vertices=new List<RoadVertex>();var triangles=new List<int>();var indices=mesh.triangles;
        for(int i=0;i<indices.Length;i+=3){
            var polygon=new List<RoadVertex>();for(int j=0;j<3;j++){int k=indices[i+j];polygon.Add(new RoadVertex{position=positions[k],normal=normals[k],uv=uvs[k]});}
            polygon=ClipRoad(ClipRoad(polygon,left,true),right,false);if(polygon.Count<3)continue;
            int start=vertices.Count;vertices.AddRange(polygon);for(int j=1;j<polygon.Count-1;j++){triangles.Add(start);triangles.Add(start+j);triangles.Add(start+j+1);}
        }
        var clipped=new Mesh{name=name};clipped.vertices=vertices.Select(v=>v.position-new Vector3((left+right)*.5f,bottom,0)).ToArray();clipped.normals=vertices.Select(v=>v.normal).ToArray();clipped.uv=vertices.Select(v=>v.uv).ToArray();clipped.triangles=triangles.ToArray();clipped.RecalculateBounds();clipped.RecalculateTangents();
        if(Mathf.Abs(clipped.bounds.size.x-(x1-x0))>.001f||Mathf.Abs(clipped.bounds.size.z-10)>.001f||Mathf.Abs(clipped.bounds.min.y)>.001f)throw new InvalidOperationException("Clipped road dimensions changed");
        var saved=SaveCrossingMesh(clipped);
        var g=Child(parent,name).gameObject;g.transform.position=new Vector3((x0+x1)*.5f,Pad,RoadZ);g.AddComponent<MeshFilter>().sharedMesh=saved;g.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<Renderer>().sharedMaterial;g.AddComponent<MeshCollider>().sharedMesh=saved;
    }
    static Mesh SaveCrossingMesh(Mesh mesh)
    {
        string path="Assets/TacticalOutpost/Models/Foundations/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!saved){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        Undo.RegisterCompleteObjectUndo(saved,"Fit crossing pavement");EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
    }
    public static string CurbGap()
    {
        Init();var roads=Group("RoadsAndDecks");Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Finish native T-junction pavement");
        var preview=EditorSceneManager.NewPreviewScene();
        try {
            var source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_tile_1")),preview);
            source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var f=source.GetComponentInChildren<MeshFilter>();var m=f.sharedMesh;
            var points=m.vertices.Select(f.transform.TransformPoint).ToArray();float low=points.Min(p=>p.y),high=points.Max(p=>p.y);
            var indices=new List<int>();var triangles=m.triangles;
            for(int i=0;i<triangles.Length;i+=3){var ids=new[]{triangles[i],triangles[i+1],triangles[i+2]};if(ids.All(k=>points[k].z<0)&&ids.Any(k=>points[k].y>low+.001f))indices.AddRange(ids);}
            if(indices.Count==0)throw new InvalidOperationException("Native south curb faces missing");
            var unique=indices.Distinct().ToArray();var mesh=new Mesh{name="Main_T_junction_south_curb"};mesh.vertices=unique.Select(k=>points[k]-Vector3.up*low).ToArray();mesh.normals=unique.Select(k=>f.transform.TransformDirection(m.normals[k])).ToArray();mesh.uv=unique.Select(k=>m.uv[k]).ToArray();mesh.triangles=indices.Select(k=>Array.IndexOf(unique,k)).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(Mathf.Abs(mesh.bounds.size.x-10)>.001f||Mathf.Abs(mesh.bounds.size.z-1.530011f)>.001f||Mathf.Abs(mesh.bounds.size.y-(high-low))>.001f)throw new InvalidOperationException("Curb profile differs from native road");
            mesh=SaveCrossingMesh(mesh);var group=roads.Find("Main T-junction finished pavement");if(!group)group=Child(roads,"Main T-junction finished pavement");Clear(group);
            var curb=Child(group,"Continuous south pavement • native profile").gameObject;curb.transform.position=new Vector3(RoadX,Pad,RoadZ);curb.AddComponent<MeshFilter>().sharedMesh=mesh;curb.AddComponent<MeshRenderer>().sharedMaterial=f.GetComponent<Renderer>().sharedMaterial;curb.AddComponent<MeshCollider>().sharedMesh=mesh;
            var crossing=roads.Find("Command four-way crossing");
            foreach(var t in crossing.Cast<Transform>().Where(t=>new[]{"Connected pavement corner 3 (1)","Connected pavement corner 3 (2)","Connected pavement corner 3 (3)"}.Contains(t.name)).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
            var cornerMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TacticalOutpost/Models/Foundations/Crossing_corner_pavement.asset");if(!cornerMesh)throw new InvalidOperationException("Native corner cap missing");
            float a=cornerMesh.bounds.extents.x;for(int i=0;i<2;i++){var g=Child(group,i==0?"NW pavement socket":"NE pavement socket").gameObject;g.transform.SetPositionAndRotation(new Vector3(RoadX+(i==0?-5+a:5-a),Pad,RoadZ+5-a),Quaternion.Euler(0,i*90,0));g.AddComponent<MeshFilter>().sharedMesh=cornerMesh;g.AddComponent<MeshRenderer>().sharedMaterial=f.GetComponent<Renderer>().sharedMaterial;g.AddComponent<MeshCollider>().sharedMesh=cornerMesh;}
            Physics.SyncTransforms();for(float x=RoadX-4.99f;x<RoadX+5;x+=.1f){if(!curb.GetComponent<Collider>().Raycast(new Ray(new Vector3(x,1,RoadZ-4.25f),Vector3.down),out var hit,2)||Mathf.Abs(hit.point.y-(Pad+high-low))>.001f)throw new InvalidOperationException("Curb top has a gap");}
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);return "10m south curb connected; two north corner caps aligned; native width, height, material and UV retained.";
        }catch {Undo.RevertAllDownToGroup(undo);throw;}finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string NorthForecourtCurb()
    {
        Init();var roads=Group("RoadsAndDecks");var crossing=roads.Find("Command four-way crossing");var arm=crossing.Find("North arm to command forecourt");
        if(!arm)throw new InvalidOperationException("North forecourt road missing");
        var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TacticalOutpost/Models/Foundations/Main_T_junction_south_curb.asset");if(!source)throw new InvalidOperationException("Native curb profile missing");
        var bounds=BoundsOf(arm.gameObject);float half=bounds.extents.x-source.bounds.size.z;var points=source.vertices;var normals=source.normals;var uv=source.uv;var indices=source.triangles;var vertices=new List<RoadVertex>();var triangles=new List<int>();
        for(int i=0;i<indices.Length;i+=3){var polygon=new List<RoadVertex>();for(int j=0;j<3;j++){int k=indices[i+j];polygon.Add(new RoadVertex{position=points[k],normal=normals[k],uv=uv[k]});}
            polygon=ClipRoad(ClipRoad(polygon,-half,true),half,false);if(polygon.Count<3)continue;int start=vertices.Count;vertices.AddRange(polygon);for(int j=1;j<polygon.Count-1;j++)triangles.AddRange(new[]{start,start+j,start+j+1});
        }
        var copies=roads.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh==source&&Mathf.Abs(f.GetComponent<Renderer>().bounds.center.x-bounds.center.x)<1&&Mathf.Abs(f.GetComponent<Renderer>().bounds.center.z-bounds.max.z)<3).ToArray();
        if(copies.Length>1)throw new InvalidOperationException("Several copied curbs at forecourt; inspect before replacing");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Fit forecourt curb without overlapping surfaces");
        try {
            var mesh=new Mesh{name="Command_forecourt_fitted_end_curb"};mesh.vertices=vertices.Select(v=>v.position).ToArray();mesh.normals=vertices.Select(v=>v.normal).ToArray();mesh.uv=vertices.Select(v=>v.uv).ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();mesh=SaveCrossingMesh(mesh);
            var t=crossing.Find("North forecourt end curb • fitted native profile");if(!t&&copies.Length==1)t=copies[0].transform;if(!t)t=Child(crossing,"North forecourt end curb • fitted native profile");
            Undo.RecordObject(t,"Align forecourt curb sockets");if(t.parent!=crossing)Undo.SetTransformParent(t,crossing,"Move curb to forecourt");t.name="North forecourt end curb • fitted native profile";t.localScale=Vector3.one;
            t.SetPositionAndRotation(new Vector3(bounds.center.x,bounds.min.y,bounds.max.z+source.bounds.min.z),Quaternion.Euler(0,180,0));
            var filter=t.GetComponent<MeshFilter>();if(!filter)filter=Undo.AddComponent<MeshFilter>(t.gameObject);Undo.RecordObject(filter,"Fit curb length");filter.sharedMesh=mesh;
            var renderer=t.GetComponent<MeshRenderer>();if(!renderer)renderer=Undo.AddComponent<MeshRenderer>(t.gameObject);Undo.RecordObject(renderer,"Keep native curb material");renderer.sharedMaterial=arm.GetComponentInChildren<Renderer>().sharedMaterial;
            var collider=t.GetComponent<MeshCollider>();if(!collider)collider=Undo.AddComponent<MeshCollider>(t.gameObject);Undo.RecordObject(collider,"Fit curb collider");collider.sharedMesh=mesh;Physics.SyncTransforms();
            var b=renderer.bounds;if(Mathf.Abs(b.min.x-(bounds.center.x-half))>.001f||Mathf.Abs(b.max.x-(bounds.center.x+half))>.001f||Mathf.Abs(b.max.z-bounds.max.z)>.001f)throw new InvalidOperationException("Curb does not meet road sockets");
            foreach(var f in roads.GetComponentsInChildren<MeshFilter>().Where(f=>f!=filter&&f.GetComponent<Renderer>())){var other=f.GetComponent<Renderer>().bounds;if(other.size.y>.002f||Mathf.Abs(other.center.y-b.max.y)>.002f)continue;var overlap=Vector3.Min(b.max,other.max)-Vector3.Max(b.min,other.min);if(overlap.x>.0001f&&overlap.z>.0001f)throw new InvalidOperationException("Curb still overlaps raised paving: "+f.name);}
            for(float x=b.min.x+.01f;x<b.max.x;x+=.1f)if(!collider.Raycast(new Ray(new Vector3(x,1,b.center.z),Vector3.down),out var hit,2)||Mathf.Abs(hit.point.y-b.max.y)>.001f)throw new InvalidOperationException("Forecourt curb has a gap");
            EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=t.gameObject;Undo.CollapseUndoOperations(undo);
            File.WriteAllText("Temp/OutpostForecourtCurbAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{width=b.size.x,depth=b.size.z,top=b.max.y,roadEnd=bounds.max.z,backEdge=b.max.z,raisedPavingOverlaps=0,originalNativeMeshUnchanged=true},Newtonsoft.Json.Formatting.Indented));
            return "Forecourt curb trimmed to "+b.size.x.ToString("F5")+"m, aligned to road end and side curbs; no coplanar overlap with yard.";
        }catch {Undo.RevertAllDownToGroup(undo);throw;}
    }
    static void CrossingCorners(Transform parent,MeshFilter source)
    {
        var m=source.sharedMesh;var p=m.vertices.Select(source.transform.TransformPoint).ToArray();var n=m.normals;var uv=m.uv;float low=p.Min(v=>v.y),high=p.Max(v=>v.y);
        var top=Enumerable.Range(0,p.Length).Where(i=>p[i].z>0&&Mathf.Abs(p[i].y-high)<.0001f&&n[i].y>.9f).ToArray();float inner=top.Min(i=>p[i].z),outer=top.Max(i=>p[i].z),width=outer-inner,height=high-low;
        float vInner=uv[top.First(i=>Mathf.Abs(p[i].z-inner)<.0001f)].y,vOuter=uv[top.First(i=>Mathf.Abs(p[i].z-outer)<.0001f)].y,vLow=uv[Enumerable.Range(0,p.Length).First(i=>Mathf.Abs(p[i].y-low)<.0001f&&Mathf.Abs(p[i].z-inner)<.0001f)].y;
        int left=top.OrderBy(i=>p[i].x).First(),right=top.OrderByDescending(i=>p[i].x).First();Func<float,float> u=x=>Mathf.Lerp(uv[left].x,uv[right].x,(x-p[left].x)/(p[right].x-p[left].x));
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var coords=new List<Vector2>();var triangles=new List<int>();float a=width*.5f;
        Action<Vector3[],Vector2[],Vector3> quad=(points,tex,normal)=>{int first=vertices.Count;vertices.AddRange(points);coords.AddRange(tex);normals.AddRange(Enumerable.Repeat(normal,4));bool forward=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)>0;triangles.AddRange(forward?new[]{first,first+1,first+2,first,first+2,first+3}:new[]{first,first+2,first+1,first,first+3,first+2});};
        quad(new[]{new Vector3(-a,height,-a),new Vector3(-a,height,a),new Vector3(a,height,a),new Vector3(a,height,-a)},new[]{new Vector2(u(-a),vInner),new Vector2(u(-a),vOuter),new Vector2(u(a),vOuter),new Vector2(u(a),vInner)},Vector3.up);
        quad(new[]{new Vector3(-a,0,-a),new Vector3(-a,height,-a),new Vector3(a,height,-a),new Vector3(a,0,-a)},new[]{new Vector2(u(-a),vLow),new Vector2(u(-a),vInner),new Vector2(u(a),vInner),new Vector2(u(a),vLow)},Vector3.back);
        quad(new[]{new Vector3(a,0,-a),new Vector3(a,height,-a),new Vector3(a,height,a),new Vector3(a,0,a)},new[]{new Vector2(u(-a),vLow),new Vector2(u(-a),vInner),new Vector2(u(a),vInner),new Vector2(u(a),vLow)},Vector3.right);
        var mesh=new Mesh{name="Crossing_corner_pavement"};mesh.vertices=vertices.ToArray();mesh.normals=normals.ToArray();mesh.uv=coords.ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();mesh=SaveCrossingMesh(mesh);
        if(Mathf.Abs(mesh.bounds.size.x-width)>.001f||Mathf.Abs(mesh.bounds.size.y-height)>.001f)throw new InvalidOperationException("Corner curb profile changed");
        for(int i=0;i<4;i++){var g=Child(parent,"Connected pavement corner "+i).gameObject;var rotation=Quaternion.Euler(0,i*90,0);g.transform.SetPositionAndRotation(new Vector3(SouthX,Pad,RoadZ)+rotation*new Vector3(-outer+a,0,outer-a),rotation);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<Renderer>().sharedMaterial;g.AddComponent<MeshCollider>().sharedMesh=mesh;}
    }
    public static string Crossroads()
    {
        Init();EnsureFolder("Assets/TacticalOutpost/Models/Foundations");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TacticalOutpost/Models/Foundations/Native_road_junction.asset");if(!mesh)throw new InvalidOperationException("Create the native asphalt junction first");
        Undo.IncrementCurrentGroup();int undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Outpost: four-way command crossing");
        var roads=Group("RoadsAndDecks");var crossing=roads.Find("Command four-way crossing")??Child(roads,"Command four-way crossing");Clear(crossing);
        var junctions=roads.Find("Continuous native asphalt intersections");
        foreach(var t in junctions.Cast<Transform>().Where(t=>Mathf.Abs(t.position.z-RoadZ)<.01f&&(Mathf.Abs(t.position.x-(RoadX+10))<.01f||Mathf.Abs(t.position.x-(RoadX+20))<.01f)).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
        foreach(var t in roads.GetComponentsInChildren<Transform>().Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)&&Source(t.gameObject)=="Road_set_v1_s_floor"&&Mathf.Abs(t.position.z-(RoadZ+10))<.01f&&(Mathf.Abs(t.position.x-(RoadX+10))<.01f||Mathf.Abs(t.position.x-(RoadX+20))<.01f)).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
        var roadMaterial=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_tile_1")).GetComponentInChildren<Renderer>().sharedMaterial;
        var center=Child(crossing,"Four-way asphalt center 10m x 10m").gameObject;center.transform.position=new Vector3(SouthX,Pad,RoadZ);center.AddComponent<MeshFilter>().sharedMesh=mesh;center.AddComponent<MeshRenderer>().sharedMaterial=roadMaterial;center.AddComponent<MeshCollider>().sharedMesh=mesh;
        var preview=EditorSceneManager.NewPreviewScene();
        try{var source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_tile_1")),preview);source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var f=source.GetComponentInChildren<MeshFilter>();RoadConnector(crossing,"Crossing_west_connector",RoadX+5,SouthX-5,f);RoadConnector(crossing,"Crossing_east_connector",SouthX+5,RoadX+25,f);CrossingCorners(crossing,f);}finally{EditorSceneManager.ClosePreviewScene(preview);}
        Place("Road_set_v1_s_tile_1",crossing,SouthX,RoadZ+10,Pad,90,"North arm to command forecourt");
        var floorMaterial=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Road_set_v1_s_floor")).GetComponentInChildren<Renderer>().sharedMaterial;
        Infill(crossing,"Command_arm_west_paving",RoadX+5,SouthX-5,RoadZ+5,RoadZ+15,floorMaterial);Infill(crossing,"Command_arm_east_paving",SouthX+5,RoadX+25,RoadZ+5,RoadZ+15,floorMaterial);
        var stock=Group("Props").GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="Stored pallet • 19, 10");if(stock){Undo.RecordObject(stock,"Clear command access");stock.position+=new Vector3(-2,0,2.5f);stock.name="Stored pallet • 17, 12.5";}
        var barrier=Group("Props").GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="Road_block_v1"&&Mathf.Abs(t.position.x-25)<.01f&&Mathf.Abs(t.position.z-11)<.01f);if(barrier)Seat(barrier.gameObject,26.5f,11.5f,Pad,0);
        Physics.SyncTransforms();var verges=roads.Find("South gate verge paving")??Child(roads,"South gate verge paving");
        foreach(var span in new[]{new Vector2(RoadX+5,SouthX-5),new Vector2(SouthX+5,RoadX+25)}){
            var probes=new[]{new Vector2(span.x+.05f,FenceS+.05f),new Vector2(span.y-.05f,FenceS+.05f),new Vector2(span.x+.05f,RoadZ-35-.05f),new Vector2(span.y-.05f,RoadZ-35-.05f)};
            // Fill only uncovered verges; a full-width apron already covers these rectangles in a fresh build.
            int missing=probes.Count(p=>!Physics.RaycastAll(new Vector3(p.x,1,p.y),Vector3.down,2).Any(h=>h.transform.IsChildOf(roads)&&h.point.y>.17f));
            if(missing==4)Infill(verges,span.x<10?"South_gate_west_verge":"South_gate_east_verge",span.x,span.y,FenceS,RoadZ-35,floorMaterial);
            else if(missing!=0)throw new InvalidOperationException("Partially covered south verge; inspect before filling");
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undoGroup);
        return "Four-way junction centered at (19.62226, -4.24014); north arm serves the command forecourt, west/east connectors retain native curb profile and UVs.";
    }
    public static string StreetLightsExterior()
    {
        Init();var terrain=Group("World").GetComponentInChildren<Terrain>();var parent=Group("Lighting");const string name="Exterior street lights • Sat Productions";
        var positions=new List<(Vector2 p,float yaw,bool entry)>();
        foreach(float x in new[]{-37f,-20f,25f,43f})positions.Add((new Vector2(x,FenceN+3.5f),270,false));
        foreach(float x in new[]{-38f,-18f,0f,39f,51f})positions.Add((new Vector2(x,FenceS-3.5f),90,false));
        foreach(float z in new[]{-34f,-17f,14f,35f})positions.Add((new Vector2(FenceW-3.5f,z),180,false));
        foreach(float z in new[]{-32f,-11f,12f,35f})positions.Add((new Vector2(FenceE+3.5f,z),0,false));
        positions.Add((new Vector2(RoadX-8,FenceN+5),0,true));positions.Add((new Vector2(RoadX+8,FenceN+5),0,true));
        positions.Add((new Vector2(FenceW-5,RoadZ-8),90,true));positions.Add((new Vector2(FenceW-5,RoadZ+8),90,true));
        var preview=EditorSceneManager.NewPreviewScene();GameObject stage=null;var manifest=new List<object>();
        try {
            stage=new GameObject(name);SceneManager.MoveGameObjectToScene(stage,preview);
            foreach(var item in positions){
                string prefab="Street_Light_A_"+(item.entry?"09":"10");var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sat Productions/G-01/Street Lights Pack 01/Prefabs/"+prefab+".prefab");if(!asset)throw new InvalidOperationException("Street light prefab missing");
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,preview);g.transform.SetParent(stage.transform,false);float ground=terrain.SampleHeight(new Vector3(item.p.x,0,item.p.y))+terrain.transform.position.y;
                g.transform.SetPositionAndRotation(new Vector3(item.p.x,ground-.015f,item.p.y),Quaternion.Euler(0,item.yaw,0));g.name=(item.entry?"Open entrance double lamp":"Perimeter walkway lamp")+" • "+(manifest.Count+1);
                if(g.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m||m.shader.name=="Hidden/InternalErrorShader")))throw new InvalidOperationException("Lamp material is not ready");
                // Keep the native fixture size; wider cones cover the outside walkway.
                foreach(var light in g.GetComponentsInChildren<Light>()){light.color=new Color(1,.83f,.62f);light.intensity=4;light.range=8;light.spotAngle=80;light.innerSpotAngle=48;light.shadows=item.entry?LightShadows.Soft:LightShadows.None;}
                var b=BoundsOf(g);if(Mathf.Abs(b.min.y-(ground-.015f))>.003f||g.transform.localScale!=Vector3.one)throw new InvalidOperationException("Street light base/scale mismatch");
                manifest.Add(new{g.name,prefab,x=item.p.x,z=item.p.y,ground,item.yaw,lights=g.GetComponentsInChildren<Light>().Length});
            }
            if(manifest.Count!=21)throw new InvalidOperationException("Expected 21 exterior poles");
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Exterior street lights");var previous=parent.Find(name);if(previous)Undo.DestroyObjectImmediate(previous.gameObject);
            SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(parent,true);Undo.RegisterCreatedObjectUndo(stage,"Exterior street lights");
            File.WriteAllText("Temp/OutpostStreetLights.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{count=manifest.Count,poles=manifest,limits="Native fixture scale, terrain-seated bases; lights use warm 80-degree cones. Only four entrance poles cast dynamic shadows; no day/night controller added."},Newtonsoft.Json.Formatting.Indented));
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);stage=null;return "21 terrain-seated exterior light poles: 17 single fixtures and four double entrance lamps; URP materials ready.";
        }finally {if(stage)UnityEngine.Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string DryTreeExterior()
    {
        Init();var terrain=Group("World").GetComponentInChildren<Terrain>();var nature=Group("Nature");
        var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dry_Trees/Prefab"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
        if(prefabs.Length!=7)throw new InvalidOperationException("Expected seven Dry Trees variants");
        var bark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Dry_Trees/Materials/Bark.mat");if(!bark||bark.shader.name!="Universal Render Pipeline/Lit"||!bark.GetTexture("_BaseMap"))throw new InvalidOperationException("Prepare URP bark before placing trees");
        const string name="Dry tree clusters • exterior";var previous=nature.Find(name);
        var rocks=nature.GetComponentsInChildren<MeshRenderer>().Where(r=>!previous||!r.transform.IsChildOf(previous)).Select(r=>r.bounds).ToArray();
        var clusters=new[]{new Vector2(-100,85),new Vector2(-97,-65),new Vector2(-80,33),new Vector2(-82,-30),new Vector2(90,83),new Vector2(104,-15),new Vector2(86,-101),new Vector2(20,98),new Vector2(-36,-72),new Vector2(-58,-103),new Vector2(47,-103)};
        var counts=new[]{5,6,4,4,6,5,6,4,6,5,5};var random=new System.Random(20102026);var occupied=new List<Bounds>();var trunks=new List<Vector2>();var manifest=new List<object>();
        var preview=EditorSceneManager.NewPreviewScene();GameObject stage=null;
        try {
            stage=new GameObject(name);SceneManager.MoveGameObjectToScene(stage,preview);int total=0;
            for(int cluster=0;cluster<clusters.Length;cluster++){
                var patch=new GameObject("Natural patch "+(cluster+1));SceneManager.MoveGameObjectToScene(patch,preview);patch.transform.SetParent(stage.transform,false);int accepted=0;
                for(int attempt=0;attempt<800&&accepted<counts[cluster];attempt++){
                    float angle=(float)random.NextDouble()*Mathf.PI*2,radius=Mathf.Sqrt((float)random.NextDouble())*22;var p=clusters[cluster]+new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                    if(p.x<-119||p.x>119||p.y<-119||p.y>119||trunks.Any(t=>Vector2.Distance(t,p)<7))continue;
                    if(p.x<FenceW&&Mathf.Abs(p.y-RoadZ)<13||p.y>FenceN&&Mathf.Abs(p.x-RoadX)<13||p.y<FenceS&&Mathf.Abs(p.x-SouthX)<13)continue;
                    if(rocks.Any(b=>p.x>b.min.x-1&&p.x<b.max.x+1&&p.y>b.min.z-1&&p.y<b.max.z+1))continue;
                    var normalized=(new Vector3(p.x,0,p.y)-terrain.transform.position);normalized.x/=terrain.terrainData.size.x;normalized.z/=terrain.terrainData.size.z;
                    if(terrain.terrainData.GetSteepness(normalized.x,normalized.z)>18)continue;
                    var prefab=prefabs[total%prefabs.Length];var tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);tree.transform.SetParent(patch.transform,false);
                    float scale=.72f+(float)random.NextDouble()*.38f,yaw=(float)random.NextDouble()*360;tree.transform.localScale=Vector3.one*scale;
                    float ground=terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y;tree.transform.SetPositionAndRotation(new Vector3(p.x,ground-.15f,p.y),Quaternion.Euler(0,yaw,0));var b=BoundsOf(tree);
                    bool conflict=b.max.x>FenceW-8&&b.min.x<FenceE+8&&b.max.z>FenceS-8&&b.min.z<FenceN+8;
                    conflict|=b.min.x<FenceW&&b.max.z>RoadZ-7&&b.min.z<RoadZ+7;
                    conflict|=b.max.z>FenceN&&b.max.x>RoadX-7&&b.min.x<RoadX+7;
                    conflict|=b.min.z<FenceS&&b.max.x>SouthX-7&&b.min.x<SouthX+7;
                    // Keep two clear footprints for the requested exterior ruined buildings.
                    conflict|=b.max.x>-91&&b.min.x<-61&&b.max.z>48&&b.min.z<78;
                    conflict|=b.max.x>62&&b.min.x<94&&b.max.z>-80&&b.min.z<-48;
                    conflict|=b.min.x<-125||b.max.x>125||b.min.z<-125||b.max.z>125;
                    conflict|=occupied.Any(o=>Mathf.Min(b.max.x,o.max.x)-Mathf.Max(b.min.x,o.min.x)>.1f&&Mathf.Min(b.max.z,o.max.z)-Mathf.Max(b.min.z,o.min.z)>.1f);
                    if(conflict){UnityEngine.Object.DestroyImmediate(tree);continue;}
                    tree.name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(prefab))+" • "+(total+1);occupied.Add(b);trunks.Add(p);manifest.Add(new{tree.name,cluster=cluster+1,x=p.x,z=p.y,ground,pivotY=tree.transform.position.y,scale,yaw});total++;accepted++;
                }
                if(accepted!=counts[cluster])throw new InvalidOperationException("Tree patch "+(cluster+1)+" cannot fit without overlaps; stage remains hidden");
            }
            if(total!=counts.Sum()||occupied.Count!=total)throw new InvalidOperationException("Exterior tree count mismatch");
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Exterior dry tree clusters");if(previous)Undo.DestroyObjectImmediate(previous.gameObject);
            SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.SetParent(nature,false);Undo.RegisterCreatedObjectUndo(stage,"Exterior dry tree clusters");
            File.WriteAllText("Temp/OutpostDryTrees.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{count=total,seed=20102026,patches=clusters.Length,trees=manifest,limits="Trees outside fence; three approach corridors and two ruin footprints reserved; roots embedded 0.15m into sampled terrain."},Newtonsoft.Json.Formatting.Indented));
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);stage=null;return total+" dry trees, seven variants, "+clusters.Length+" irregular exterior patches; roads and ruin footprints clear.";
        }finally {if(stage)UnityEngine.Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Environment()
    {
        Init();var n=Group("Nature");var oldMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Namaqualand_Boulders.mat");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Models/namaqualand_boulders_01_2k.fbx");var variants=model.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh).ToArray();
        foreach(var t in n.Cast<Transform>().Where(t=>t.name!="Dry trees and stumps").ToArray())Undo.DestroyObjectImmediate(t.gameObject);
        var clusters=Child(n,"Geological outcrops • terrain seated");var centers=new[]{new Vector2(-67,49),new Vector2(29,65),new Vector2(71,28),new Vector2(-65,-50),new Vector2(62,-58)};
        var offsets=new[]{new Vector2(0,0),new Vector2(5,2),new Vector2(-4,3),new Vector2(2,7),new Vector2(-5,-4),new Vector2(7,-4)};
        int count=0;
        for(int c=0;c<centers.Length;c++)for(int i=0;i<offsets.Length;i++){
            var rotated=Quaternion.Euler(0,c*67,0)*new Vector3(offsets[i].x,0,offsets[i].y);var pos=centers[c]+new Vector2(rotated.x,rotated.z);var f=variants[count%variants.Length];var g=Child(clusters,"Outcrop "+c+" rock "+i);var part=Child(g,"Scanned rock").gameObject;part.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;part.AddComponent<MeshRenderer>().sharedMaterial=oldMaterial;part.transform.localRotation=f.transform.rotation;part.transform.localScale=f.transform.lossyScale;
            g.rotation=Quaternion.Euler(i*2-5,c*53+i*71,i%3*3-3);var b=BoundsOf(g.gameObject);float size=i==0?7.2f:3.2f+i*.42f;g.localScale*=size/Mathf.Max(b.size.x,b.size.z);b=BoundsOf(g.gameObject);g.position+=new Vector3(pos.x-b.center.x,Ground(pos.x,pos.y)-size*.10f-b.min.y,pos.y-b.center.z);part.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;count++;
        }
        var details=new[]{new Vector2(-54,35),new Vector2(-55,38),new Vector2(59,35),new Vector2(61,39),new Vector2(48,53),new Vector2(43,53),new Vector2(-34,-50),new Vector2(-30,-51),new Vector2(44,-51),new Vector2(48,-53)};
        foreach(var pos in details){var f=variants[count%variants.Length];var g=Child(clusters,"Small exposed rock "+count).gameObject;g.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;g.AddComponent<MeshRenderer>().sharedMaterial=oldMaterial;g.transform.rotation=f.transform.rotation*Quaternion.Euler(0,count*47,0);g.transform.localScale=f.transform.lossyScale;var b=BoundsOf(g);float size=1.1f+(count%3)*.35f;g.transform.localScale*=size/Mathf.Max(b.size.x,b.size.z);b=BoundsOf(g);g.transform.position+=new Vector3(pos.x-b.center.x,Ground(pos.x,pos.y)-.12f-b.min.y,pos.y-b.center.z);g.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;count++;}
        EditorSceneManager.MarkSceneDirty(scene);return count+" rocks placed in asymmetric geological clusters; main spawn corridors remain clear.";
    }
    public static string PowerPipeline()
    {
        Init();Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Outpost: fuel to power station pipe");var utilities=Group("Utilities");var line=utilities.Find("Fuel to power station pipeline")??Child(utilities,"Fuel to power station pipeline");Clear(line);
        var fuel=utilities.Cast<Transform>().Single(t=>t.name.StartsWith("Fuel farm"));foreach(var t in fuel.Cast<Transform>().Where(t=>Source(t.gameObject)=="Pipes_set_v1_V_set_v2"&&BoundsOf(t.gameObject).min.y>7).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
        // Measured open mesh sockets preserve the artist's 4.583913m pipe pitch and full native scale.
        var socketA=new Vector3(-.137152255f,-2.291957f,-.014509714f);var socketB=new Vector3(-.137152255f,2.29195619f,-.014509774f);
        float axis=Pad+2.84336138f+2.49673128f;var start=new Vector3(5.28f,axis,-29);var rotation=Quaternion.Euler(0,0,-90);var next=start;var joints=new List<object>();float lowest=float.MaxValue;
        for(int i=0;i<7;i++){
            var g=Native("Pipes_set_v1_tile_s2",line,next-rotation*socketA,rotation);g.name=i==6?"Power station inlet":"Fuel header module "+(i+1);
            float error=Vector3.Distance(g.transform.TransformPoint(socketA),next);if(error>.001f)throw new InvalidOperationException("Pipe socket gap exceeds 1mm");joints.Add(new{module=i+1,gap=error});next=g.transform.TransformPoint(socketB);lowest=Mathf.Min(lowest,BoundsOf(g).min.y);
        }
        var concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Outpost_Concrete_Foundation.mat");
        foreach(float x in new[]{7.7f,13.4f,25.9f,34.5f}){
            float supportY=axis-2.16972852f,baseHeight=supportY-2.57542014f-Pad;
            var baseObject=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(baseObject,scene);Undo.RegisterCreatedObjectUndo(baseObject,"Pipe footing");baseObject.name="Pipe support footing "+x;baseObject.transform.SetParent(line,false);baseObject.transform.position=new Vector3(x,Pad+baseHeight*.5f,-29+.0412423f);baseObject.transform.localScale=new Vector3(1.25f,baseHeight,2.1f);baseObject.GetComponent<Renderer>().sharedMaterial=concrete;
            var support=Native("Pipes_set_v1_support",line,new Vector3(x,supportY,-29+.0412423f),Quaternion.Euler(0,90,0));support.name="Native pipe trestle "+x;
            if(Mathf.Abs(BoundsOf(support).min.y-baseObject.GetComponent<Renderer>().bounds.max.y)>.002f)throw new InvalidOperationException("Pipe support floats above its footing");
            var b=BoundsOf(support);if(b.min.x<24.62226f&&b.max.x>14.62226f)throw new InvalidOperationException("Trestle blocks the service lane");
        }
        float clearance=lowest-Pad;if(clearance<4.5f)throw new InvalidOperationException("Insufficient overhead clearance");
        var station=Group("Buildings").Cast<Transform>().Single(t=>Source(t.gameObject)=="Hangar_v4");float wall=BoundsOf(station.gameObject).min.x;
        if(next.x<=wall||next.x>wall+1.5f)throw new InvalidOperationException("Pipe must terminate just inside the power station wall");
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);File.WriteAllText("Temp/OutpostPipelineAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{start=start.ToString(),end=next.ToString(),roadClearance=clearance,joints,expectedContact="Last module enters the power station wall; tank end meets the east vessel wall."},Newtonsoft.Json.Formatting.Indented));
        return "Seven native pipe modules connected to power station; four supported trestles; road clearance "+clearance.ToString("F2")+"m; socket gaps <1mm.";
    }
    static float PavingHeight(Collider[] surfaces, float x, float z)
    {
        float y=float.NegativeInfinity;
        foreach(var c in surfaces)if(c.Raycast(new Ray(new Vector3(x,30,z),Vector3.down),out var hit,60))y=Mathf.Max(y,hit.point.y);
        return float.IsNegativeInfinity(y)?Pad:y;
    }
    public static string RaiseYardToCurbs()
    {
        Init();var roads=Group("RoadsAndDecks");Physics.SyncTransforms();
        var floors=roads.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().bounds.size.y<.002f&&f.sharedMesh.name!="Native_road_junction").ToArray();
        if(floors.Length==0)throw new InvalidOperationException("No yard surfaces found");
        float target=roads.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name=="Road_set_v1_s_tile_1").GetComponent<Renderer>().bounds.max.y;
        float current=floors.Average(f=>f.GetComponent<Renderer>().bounds.center.y),lift=target-current;
        if(Mathf.Abs(lift)<.001f)return "Yard already matches curb height.";
        if(lift<0||lift>.25f||floors.Any(f=>Mathf.Abs(f.GetComponent<Renderer>().bounds.center.y-current)>.003f))throw new InvalidOperationException("Yard levels need individual inspection before lifting");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Raise yard and supported assemblies to curb height");
        var moved=new List<object>();
        try {
            foreach(var f in floors){Undo.RecordObject(f.transform,"Raise yard paving");f.transform.position+=Vector3.up*lift;}
            foreach(var name in new[]{"Buildings","Cargo","Utilities","Perimeter"}){
                var t=Group(name);Undo.RecordObject(t,"Lift complete connected assembly");t.position+=Vector3.up*lift;moved.Add(new{name,delta=lift});
            }
            Physics.SyncTransforms();var surfaces=roads.GetComponentsInChildren<MeshCollider>().Cast<Collider>().ToArray();
            // Keep pallet contents together; road barriers use their own supporting surface.
            foreach(var category in Group("Props").Cast<Transform>())foreach(var t in category.Cast<Transform>()){
                var b=BoundsOf(t.gameObject);float support=float.NegativeInfinity;
                for(int iz=0;iz<3;iz++)for(int ix=0;ix<3;ix++)support=Mathf.Max(support,PavingHeight(surfaces,Mathf.Lerp(b.min.x+.02f,b.max.x-.02f,ix*.5f),Mathf.Lerp(b.min.z+.02f,b.max.z-.02f,iz*.5f)));
                float dy=support-b.min.y;if(Mathf.Abs(dy)>.3f)throw new InvalidOperationException("Unexpected prop support offset: "+t.name+" "+dy);
                Undo.RecordObject(t,"Seat prop assembly on raised paving");t.position+=Vector3.up*dy;
                if(Mathf.Abs(BoundsOf(t.gameObject).min.y-support)>.002f)throw new InvalidOperationException("Prop seating failed: "+t.name);
                moved.Add(new{name=AnimationUtility.CalculateTransformPath(t,root),delta=dy});
            }
            // Close the raised yard underside without filling the lower traffic lanes.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var f in floors){var b=f.GetComponent<Renderer>().bounds;int i=vertices.Count;float bottom=Mathf.Min(.14f,b.min.y-.02f),top=b.max.y-.002f;
                vertices.AddRange(new[]{new Vector3(b.min.x,bottom,b.min.z),new Vector3(b.max.x,bottom,b.min.z),new Vector3(b.max.x,bottom,b.max.z),new Vector3(b.min.x,bottom,b.max.z),new Vector3(b.min.x,top,b.min.z),new Vector3(b.max.x,top,b.min.z),new Vector3(b.max.x,top,b.max.z),new Vector3(b.min.x,top,b.max.z)});
                triangles.AddRange(new[]{0,1,2,0,2,3,4,7,6,4,6,5,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0}.Select(n=>i+n));
            }
            EnsureFolder("Assets/TacticalOutpost/Models/Foundations");const string path="Assets/TacticalOutpost/Models/Foundations/Raised_yard_support.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh{name="Raised yard support"};AssetDatabase.CreateAsset(mesh,path);}else {Undo.RegisterCompleteObjectUndo(mesh,"Raise yard support");mesh.Clear();}
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var foundation=roads.Find("Raised yard solid support")??Child(roads,"Raised yard solid support");
            var filter=foundation.GetComponent<MeshFilter>();if(!filter)filter=Undo.AddComponent<MeshFilter>(foundation.gameObject);filter.sharedMesh=mesh;
            var renderer=foundation.GetComponent<MeshRenderer>();if(!renderer)renderer=Undo.AddComponent<MeshRenderer>(foundation.gameObject);renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalOutpost/Materials/Outpost_Concrete_Foundation.mat");
            var collider=foundation.GetComponent<MeshCollider>();if(!collider)collider=Undo.AddComponent<MeshCollider>(foundation.gameObject);collider.sharedMesh=mesh;
            Physics.SyncTransforms();var player=Group("Player").GetComponentInChildren<CharacterController>();Undo.RecordObject(player.transform,"Seat player on raised yard");var position=player.transform.position;position.y=PavingHeight(surfaces,position.x,position.z);player.transform.position=position;
            if(floors.Any(f=>Mathf.Abs(f.GetComponent<Renderer>().bounds.center.y-target)>.002f))throw new InvalidOperationException("Raised paving is not flush with curb");
            File.WriteAllText("Temp/OutpostYardHeightAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{previousY=current,yardY=target,lift,pavingCount=floors.Length,moved,limits="Prop assemblies sampled at nine footprint points; connected tank and pipeline assemblies moved intact."},Newtonsoft.Json.Formatting.Indented));
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);return "Raised "+floors.Length+" yard surfaces by "+lift.ToString("F5")+"m; complete assemblies and props seated; traffic lanes preserved.";
        }catch {Undo.RevertAllDownToGroup(undo);throw;}
    }
    public static string Navigation()
    {
        Init();Physics.SyncTransforms();var markers=Group("GameplayMarkers");
        var target=new Vector3(33,Pad,5.8f);markers.Find("Objective_Main").position=target;
        markers.Find("PlayerStart").position=new Vector3(31,Pad,2.5f);
        markers.Find("BossArena_Center").position=new Vector3(10,Pad,-4);
        markers.Find("Defense_W").position=new Vector3(-19,Pad,.8f);
        markers.Find("Defense_N").position=new Vector3(8,Pad,14);
        markers.Find("Defense_S").position=new Vector3(26,Pad,-12);
        var routes=new Dictionary<string,Vector3[]> {
            {"Route_West",new[]{new Vector3(-60,Pad,RoadZ),new Vector3(FenceW,Pad,RoadZ),new Vector3(-30,Pad,RoadZ),new Vector3(-10,Pad,RoadZ),new Vector3(RoadX,Pad,RoadZ),new Vector3(22,Pad,RoadZ),target}},
            {"Route_North",new[]{new Vector3(RoadX,Pad,55),new Vector3(RoadX,Pad,FenceN),new Vector3(RoadX,Pad,28),new Vector3(RoadX,Pad,12),new Vector3(12,Pad,10),new Vector3(24,Pad,6),target}},
            {"Route_South",new[]{new Vector3(SouthX,Pad,-55),new Vector3(SouthX,Pad,FenceS),new Vector3(SouthX,Pad,-28),new Vector3(SouthX,Pad,-16),new Vector3(22,Pad,RoadZ),target}}
        };
        foreach(var pair in routes){var p=markers.Find(pair.Key);Clear(p);int i=0;foreach(var point in pair.Value){var t=Child(p,"Waypoint "+i++);t.position=point;}}
        return RebakeNavigation();
    }
    public static string RebakeNavigation()
    {
        Init();Physics.SyncTransforms();var markers=Group("GameplayMarkers");var surfaces=Group("RoadsAndDecks").GetComponentsInChildren<MeshCollider>().Cast<Collider>().ToArray();
        foreach(var t in markers.GetComponentsInChildren<Transform>().Where(t=>t!=markers&&t.childCount==0)){Undo.RecordObject(t,"Ground navigation marker");var p=t.position;p.y=PavingHeight(surfaces,p.x,p.z);t.position=p;}
        var routes=markers.Cast<Transform>().Where(t=>t.name.StartsWith("Route_")).ToDictionary(t=>t.name,t=>t.Cast<Transform>().Select(p=>p.position).ToArray());
        var s=root.GetComponent<NavMeshSurface>();Undo.RecordObject(s,"Bake current layout");s.collectObjects=CollectObjects.Volume;s.center=new Vector3(2,5,0);s.size=new Vector3(160,30,145);s.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;s.overrideVoxelSize=true;s.voxelSize=.10f;
        s.BuildNavMesh();if(!s.navMeshData)throw new InvalidOperationException("NavMesh bake produced no data");
        s.navMeshData.name="OutpostNavigation";
        const string path="Assets/TacticalOutpost/OutpostNavigation.asset";var saved=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
        if(saved){var generated=s.navMeshData;s.RemoveData();EditorUtility.CopySerialized(generated,saved);s.navMeshData=saved;UnityEngine.Object.DestroyImmediate(generated);s.AddData();EditorUtility.SetDirty(saved);}else AssetDatabase.CreateAsset(s.navMeshData,path);
        var checks=new List<object>();var failures=new List<string>();
        foreach(var pair in routes)for(int i=0;i<pair.Value.Length-1;i++){
            UnityEngine.AI.NavMeshHit a,b;bool ok=UnityEngine.AI.NavMesh.SamplePosition(pair.Value[i],out a,.8f,-1)&UnityEngine.AI.NavMesh.SamplePosition(pair.Value[i+1],out b,.8f,-1);
            var navPath=new UnityEngine.AI.NavMeshPath();if(ok)ok=UnityEngine.AI.NavMesh.CalculatePath(a.position,b.position,-1,navPath)&&navPath.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
            checks.Add(new{route=pair.Key,segment=i,complete=ok});if(!ok)failures.Add(pair.Key+" segment "+i);
        }
        EditorSceneManager.MarkSceneDirty(scene);Directory.CreateDirectory("Temp");File.WriteAllText("Temp/OutpostNavigationAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));
        if(failures.Count>0)throw new InvalidOperationException("Unreachable route segments: "+string.Join(", ",failures));return checks.Count+" route segments connected; persisted NavMesh asset.";
    }
    public static string Build() {Foundation();SouthAlignment();Occupation();PowerPipeline();Junctions();CurbGap();Environment();RaiseYardToCurbs();NorthForecourtCurb();OuterFence();return Navigation();}
}
