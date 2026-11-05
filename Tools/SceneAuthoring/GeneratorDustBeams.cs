using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class GeneratorDustBeams
{
    public static string Add()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity")throw new InvalidOperationException("Open map in Edit Mode.");
        var group=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Generator red security floodlights");
        var lights=group.GetComponentsInChildren<Light>(true);
        if(lights.Length==0||lights.Any(l=>l.type!=LightType.Spot||l.color.r<=l.color.g))throw new InvalidOperationException("Expected red generator spotlights.");
        var shader=Shader.Find("TacticalOutpost/DustLightBeam");
        if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Dust beam shader did not compile.");
        var backup="C:/Users/Faiz/OneDrive/Documents/TacticalOutpostBackups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_DustBeamsFire";
        Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/Before_Map.unity",true))throw new IOException("Backup failed.");
        const string meshPath="Assets/TacticalOutpost/Materials/DustBeamCone.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(!mesh){
            const int segments=64;var vertices=new List<Vector3>{Vector3.zero,new Vector3(0,0,1)};var triangles=new List<int>();
            for(int i=0;i<segments;i++){float angle=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),1));}
            for(int i=0;i<segments;i++){int a=i+2,b=(i+1)%segments+2;triangles.AddRange(new[]{0,b,a,1,a,b});}
            mesh=new Mesh{name="Unit dust cone"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            if(mesh.vertexCount!=66||mesh.triangles.Length!=384)throw new InvalidOperationException("Cone topology incomplete.");
            AssetDatabase.CreateAsset(mesh,meshPath);
        }
        const string matPath="Assets/TacticalOutpost/Materials/Generator_DustBeam.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(shader){name="Generator dust scattering"};AssetDatabase.CreateAsset(material,matPath);}
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Red floodlight rays in desert dust");
        try{
            foreach(var light in lights){
                Undo.RecordObject(light,"Stronger broader red floodlight");light.intensity=600;light.range=45;light.spotAngle=130;light.innerSpotAngle=100;PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                var existing=light.transform.Find("Dust scattering beam");
                if(existing)throw new InvalidOperationException("Dust beam already exists; refine the existing effect.");
                var obj=new GameObject("Dust scattering beam");SceneManager.MoveGameObjectToScene(obj,scene);obj.transform.SetParent(light.transform,false);Undo.RegisterCreatedObjectUndo(obj,"Local dust beam");
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var beam=obj.AddComponent<OutpostDustBeam>();beam.source=light;beam.length=11;beam.density=.075f;beam.Refresh();
                if(renderer.enabled!=light.enabled||Vector3.Angle(obj.transform.forward,light.transform.forward)>.01f)throw new InvalidOperationException("Beam is not synchronized with lamp.");
            }
            if(group.GetComponentsInChildren<OutpostDustBeam>(true).Length!=lights.Length)throw new InvalidOperationException("Missing ray.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new IOException("Map save failed.");Undo.CollapseUndoOperations(undo);
            File.WriteAllText(backup+"/Status.md","# Dust light rays\n"+lights.Length+" red generator lamps: intensity 600, range 45 m, outer 130 degrees, inner 100 degrees. Local 16-sample dust volumes follow each light and fade with depth, core angle limited to 80 degrees, length 11 m. They switch off with Pagi and on with Malam. No global pipeline settings changed.\n\nCamera-depth occlusion only; no shadowed light-path scattering. No Play Mode/FPS test. Fire package being acquired.");
            return backup;
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
