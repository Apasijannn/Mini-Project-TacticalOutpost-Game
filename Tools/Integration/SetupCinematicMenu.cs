using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.UI;
using Outpost;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.Player;
using Unity.Collections;
using System.Collections.Generic;

public static class SetupCinematicMenu
{
    const string Fonts = "Assets/TacticalOutpost/Gameplay/UI/Fonts/";
    const string Models = "Assets/TacticalOutpost/Gameplay/UI/PreviewModels/";
    static void Folder(string path) { if(System.IO.Directory.Exists(path)) return; System.IO.Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
    static GameObject Visual(GameObject source, string id)
    {
        string path=Models+id+".prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing != null) return existing;
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var go=UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(go,preview); go.name=id;
            go.transform.localPosition=Vector3.zero; go.transform.localRotation=Quaternion.identity; go.SetActive(false);
            foreach(var brain in go.GetComponentsInChildren<EnemyBrain>(true)) UnityEngine.Object.DestroyImmediate(brain);
            foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) UnityEngine.Object.DestroyImmediate(script);
            foreach(var particles in go.GetComponentsInChildren<ParticleSystem>(true).Reverse()) if(particles != null) UnityEngine.Object.DestroyImmediate(particles.gameObject);
            foreach(var component in go.GetComponentsInChildren<Component>(true).Reverse())
                if(component != null && !(component is Transform) && !(component is Renderer && !(component is ParticleSystemRenderer) && !(component is LineRenderer)) && !(component is MeshFilter) && !(component is Animator) && !(component is LODGroup)) UnityEngine.Object.DestroyImmediate(component);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)) if(renderer is ParticleSystemRenderer || renderer is LineRenderer) UnityEngine.Object.DestroyImmediate(renderer);
            foreach(var animator in go.GetComponentsInChildren<Animator>(true)) { animator.applyRootMotion=false; animator.updateMode=AnimatorUpdateMode.UnscaledTime; }
            go.tag="Untagged"; go.SetActive(true);
            if(go.GetComponentsInChildren<MonoBehaviour>(true).Length>0 || go.GetComponentsInChildren<Collider>(true).Length>0) throw new InvalidOperationException("Combat component in visual prefab.");
            return PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent)+"/"+t.name;
    static GameObject Debris(MeshRenderer renderer, string id, int nx, int ny, int nz)
    {
        // shortcut: chunks have no interior caps; use closed authored shards for close-up destruction.
        const string root="Assets/TacticalOutpost/Gameplay/Effects/Destruction/";
        Folder(root);
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
        using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh);
        int n=mesh.vertexCount;
        using var va=new NativeArray<Vector3>(n,Allocator.Temp); using var na=new NativeArray<Vector3>(n,Allocator.Temp); using var uv=new NativeArray<Vector2>(n,Allocator.Temp);
        data[0].GetVertices(va); data[0].GetNormals(na); data[0].GetUVs(0,uv);
        var vertices=va.ToArray().ToList(); var normals=na.ToArray().ToList(); var uvs=uv.ToArray().ToList();
        int cells=nx*ny*nz;
        var groups=new List<int>[cells,mesh.subMeshCount];
        for(int i=0;i<cells;i++) for(int s=0;s<mesh.subMeshCount;s++) groups[i,s]=new List<int>();
        var bounds=mesh.bounds;
        float edgeLimit=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)/(Mathf.Max(nx,ny,nz)*1.8f);
        int Midpoint(int a,int b) { int index=vertices.Count; vertices.Add((vertices[a]+vertices[b])*.5f); normals.Add((normals[a]+normals[b]).normalized); uvs.Add((uvs[a]+uvs[b])*.5f); return index; }
        void Triangle(int a,int b,int c,int submesh,int depth)
        {
            if(depth<6 && Mathf.Max((vertices[a]-vertices[b]).magnitude,(vertices[b]-vertices[c]).magnitude,(vertices[c]-vertices[a]).magnitude)>edgeLimit)
            {
                int ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                Triangle(a,ab,ca,submesh,depth+1);Triangle(ab,b,bc,submesh,depth+1);Triangle(ca,bc,c,submesh,depth+1);Triangle(ab,bc,ca,submesh,depth+1);return;
            }
            var p=(vertices[a]+vertices[b]+vertices[c])/3-bounds.min;
            int x=Mathf.Clamp((int)(p.x/bounds.size.x*nx),0,nx-1), y=Mathf.Clamp((int)(p.y/bounds.size.y*ny),0,ny-1), z=Mathf.Clamp((int)(p.z/bounds.size.z*nz),0,nz-1);
            groups[x+y*nx+z*nx*ny,submesh].AddRange(new[]{a,b,c});
        }
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            using var indices=new NativeArray<int>((int)mesh.GetIndexCount(s),Allocator.Temp); data[0].GetIndices(indices,s,true);
            for(int i=0;i<indices.Length;i+=3)
                Triangle(indices[i],indices[i+1],indices[i+2],s,0);
        }
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var go=new GameObject(id+" debris"); SceneManager.MoveGameObjectToScene(go,preview);
            for(int i=0;i<cells;i++)
            {
                if(!Enumerable.Range(0,mesh.subMeshCount).Any(s=>groups[i,s].Count>0))continue;
                var used=Enumerable.Range(0,mesh.subMeshCount).SelectMany(s=>groups[i,s]).Distinct().ToArray();
                var remap=used.Select((old,index)=>new{old,index}).ToDictionary(x=>x.old,x=>x.index);
                var points=used.Select(index=>vertices[index]).ToArray();
                var box=new Bounds(points[0],Vector3.zero);foreach(var p in points)box.Encapsulate(p);
                var part=new Mesh { name=id+" shard "+i,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=points.Select(p=>p-box.center).ToArray(),normals=used.Select(index=>normals[index]).ToArray(),uv=used.Select(index=>uvs[index]).ToArray(),subMeshCount=mesh.subMeshCount };
                for(int s=0;s<mesh.subMeshCount;s++) part.SetTriangles(groups[i,s].Select(index=>remap[index]).ToArray(),s);
                part.RecalculateBounds();
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(root+part.name+".asset");
                if(previous!=null){EditorUtility.CopySerialized(part,previous);UnityEngine.Object.DestroyImmediate(part);part=previous;EditorUtility.SetDirty(part);}
                else AssetDatabase.CreateAsset(part,root+part.name+".asset");
                var piece=new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer)); piece.transform.SetParent(go.transform,false);
                piece.transform.localPosition=box.center;
                piece.GetComponent<MeshFilter>().sharedMesh=part; piece.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            return PrefabUtility.SaveAsPrefabAsset(go,root+id+"Debris.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    static TMP_FontAsset FontAsset(string file)
    {
        string path = Fonts + file + " SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        var source = AssetDatabase.LoadAssetAtPath<Font>(Fonts + file + ".ttf");
        if (source == null) throw new InvalidOperationException("Missing font: " + file);
        asset = TMP_FontAsset.CreateFontAsset(source);
        asset.name = file + " SDF";
        string characters = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
        if (!asset.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("Missing font glyphs: " + missing);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures)
        {
            texture.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(texture, asset);
        }
        EditorUtility.SetDirty(asset); return asset;
    }
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var heading = FontAsset("BarlowCondensed-SemiBold");
        var body = FontAsset("BarlowCondensed-Medium");
        var mono = FontAsset("ShareTechMono-Regular");
        Folder(Models);
        var session=UnityEngine.Object.FindObjectsByType<IndustrialGameSession>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var wave=session.systems.GetComponentInChildren<WaveDirector>(true);
        var entries=new List<ManualEntry>();
        string[] descriptions={
            "A steady close-range threat. Never let a group reach the station unchecked.",
            "A heavy shambler. Use open lanes to keep distance and aim for the head.",
            "A fast pursuer. Track your flanks and avoid reloading in the open.",
            "An exposed infected. Keep moving and exploit its headshot multiplier.",
            "The final-wave heavy. One enters through each gate. Enrages as its health drops.",
            "An alternate heavy variant in the catalog. Not selected by the current wave director.",
            "An alternate heavy variant in the catalog. Not selected by the current wave director."};
        for(int i=0;i<wave.catalog.variants.Count;i++)
        {
            var v=wave.catalog.variants[i];
            entries.Add(new ManualEntry{id=v.id,visual=Visual(v.prefab,v.id),enemy=v.prefab.GetComponent<EnemyHealth>().config,inCurrentWaves=v.kind==EnemyKind.Normal || v.id==wave.bossVariantId,description=descriptions[i]});
        }
        var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Gameplay/Prefabs/Adapted/Player_Industrial.prefab");
        foreach(var weapon in player.GetComponentsInChildren<FpsWeapon>(true))
            entries.Add(new ManualEntry{id=weapon.name,visual=Visual(weapon.gameObject,weapon.name),weapon=weapon,inCurrentWaves=true,description=weapon.kind==WeaponKind.Rifle ? "Your primary automatic rifle. Fire short bursts for control; aim down sights for distant contacts." : "A semi-automatic backup. Switch when the rifle runs dry and make each shot count."});
        var spawns=session.systems.GetComponentsInChildren<EnvSpawnPoint>(true).Where(p=>p.group=="Enemy").Select(p=>p.transform.position).ToArray();
        var targets=session.systems.GetComponentsInChildren<EnvGeneratorSlot>(true).SelectMany(g=>g.attackPoints).Select(t=>t.position).ToArray();
        var death=session.systems.GetComponentInChildren<PlayerDeathView>(true);
        if(death==null) session.systems.GetComponentInChildren<PlayerHealth>(true).gameObject.AddComponent<PlayerDeathView>();
        var destruction=wave.GetComponent<GeneratorDestruction>()??wave.gameObject.AddComponent<GeneratorDestruction>();
        destruction.explosion=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TacticalOutpost/Gameplay/Effects/WarFX/Generator explosion URP.prefab");
        destruction.fire=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Medium.prefab");
        var env=SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
        var environmental=env.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var generator=environmental.Single(r=>r.name.Contains("076") && r.name.Contains("Generator_v1"));
        destruction.generatorPath=Hierarchy(generator.transform); destruction.debris=Debris(generator,"Generator",8,3,5);
        var station=environmental.Single(r=>r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>()!=null && r.GetComponent<MeshFilter>().sharedMesh.name=="Hangar_v4");
        destruction.buildingPath=Hierarchy(station.transform); destruction.buildingCenter=station.bounds.center; destruction.buildingDebris=Debris(station,"PowerStation",8,3,5);
        var center=generator.bounds.center;
        var tanks=environmental.Where(r=>r.gameObject.activeInHierarchy && r.name.Contains("Oil_tank") && Vector3.Distance(r.bounds.center,center)<26).ToArray();
        var tankDebris=Debris(tanks.First(),"OilTank",2,3,1);
        destruction.tanks=tanks.Select(r=>new GeneratorDestruction.Breakable{path=Hierarchy(r.transform),debris=tankDebris}).ToArray();
        destruction.blastSites=tanks.GroupBy(r=>(r.bounds.center.x<center.x?0:1)+(r.bounds.center.z<center.z?0:2)).Select(g=>new Vector3(g.Average(r=>r.bounds.center.x),g.Average(r=>r.bounds.center.y)+2,g.Average(r=>r.bounds.center.z))).ToArray();
        var walls=environmental.Where(r=>r.gameObject.activeInHierarchy && r.name.StartsWith("Concrete_fence_v1_s") && r.transform.parent!=null && r.transform.parent.name.StartsWith("Fuel containment")).ToArray();
        var wallPrefabs=walls.GroupBy(r=>r.GetComponent<MeshFilter>().sharedMesh.name).ToDictionary(g=>g.Key,g=>Debris(g.First(),g.Key,3,2,1));
        destruction.containmentWalls=walls.Select(r=>new GeneratorDestruction.Breakable{path=Hierarchy(r.transform),debris=wallPrefabs[r.GetComponent<MeshFilter>().sharedMesh.name]}).ToArray();
        destruction.pipePaths=environmental.Where(r=>r.gameObject.activeInHierarchy && (r.name.StartsWith("Pipes_set") && Vector3.Distance(r.bounds.center,center)<24 || r.transform.parent!=null && r.transform.parent.name=="Fuel to power station pipeline" && !r.name.StartsWith("Pipe support footing"))).Select(r=>Hierarchy(r.transform)).ToArray();
        if(destruction.explosion==null || destruction.blastSites.Length!=4 || destruction.tanks.Length<30 || destruction.containmentWalls.Length<10)throw new InvalidOperationException("Missing explosion VFX or refinery targets.");
        var previous = SceneManager.GetActiveScene();
        var menu = SceneManager.GetSceneByPath(GameMenu.MenuScene);
        bool opened = !menu.IsValid() || !menu.isLoaded;
        if (opened) menu = EditorSceneManager.OpenScene(GameMenu.MenuScene, OpenSceneMode.Additive);
        try
        {
            foreach (string path in new[] { GameMenu.MenuScene, GameMenu.GameScene })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Expected an open scene: " + path);
                var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameMenu>(true)).Single();
                Undo.RecordObject(controller, "Style cinematic menu");
                controller.headingFont = heading; controller.bodyFont = body; controller.monoFont = mono;
                controller.manualEntries=entries.ToArray(); controller.previewSpawns=spawns; controller.previewTargets=targets;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save menu fonts.");
            }
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameMenu.MenuScene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(menu, true);
        }
        return "Cinematic main menu and pause menu configured; three SDF fonts saved; Play starts at main menu.";
    }
}
