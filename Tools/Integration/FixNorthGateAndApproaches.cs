using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Outpost;

public static class FixNorthGateAndApproaches
{
    static Bounds BoundsOf(GameObject g)
    {
        var renderers=g.GetComponentsInChildren<MeshRenderer>(true);
        var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
    }

    public static object Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var environment=SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
        var game=SceneManager.GetSceneByPath("Assets/Scenes/Gameplay/TacticalOutpost_Game.unity");
        var session=game.GetRootGameObjects().Select(g=>g.GetComponent<IndustrialGameSession>()).First(s=>s);
        var renderers=environment.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        if(renderers.Any(r=>r.transform.parent.name=="North gate fence to pillar connectors")||session.transform.Find("Approach lane invisible walls"))throw new InvalidOperationException("Fix already applied.");
        var column=renderers.Single(r=>r.name.StartsWith("Concrete_fence_v1_column")&&r.bounds.center.z>46&&r.bounds.center.z<48&&r.bounds.center.x>6&&r.bounds.center.x<9);
        var panelWest=renderers.Single(r=>r.name=="Concrete_fence_v2_S"&&r.transform.parent.name=="North span 7");
        var panelEast=renderers.Single(r=>r.name=="Concrete_fence_v2_S"&&r.transform.parent.name=="North span 11");
        var group=new GameObject("North gate fence to pillar connectors");SceneManager.MoveGameObjectToScene(group,environment);
        group.transform.SetParent(panelWest.transform.parent.parent,false);Undo.RegisterCreatedObjectUndo(group,"Complete north gate fence");
        var pillarRoot=PrefabUtility.GetOutermostPrefabInstanceRoot(column.gameObject);
        var pillar=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(column.gameObject)),group.transform);
        pillar.name="West north-gate pillar";pillar.transform.SetPositionAndRotation(pillarRoot.transform.position,pillarRoot.transform.rotation);pillar.transform.localScale=pillarRoot.transform.lossyScale;
        var b=BoundsOf(pillar);float targetX=2*2.6f-column.bounds.center.x;
        pillar.transform.position+=Vector3.right*(targetX-b.center.x);b=BoundsOf(pillar);
        if(Mathf.Abs(b.center.x-targetX)>.001f)throw new InvalidOperationException("Pillar alignment failed.");
        var spans=new[]{new Vector2(panelWest.bounds.max.x-.01f,b.min.x+.01f),new Vector2(column.bounds.max.x-.01f,panelEast.bounds.min.x+.01f)};
        var lengths=new float[2];
        for(int i=0;i<2;i++)
        {
            var span=spans[i];lengths[i]=span.y-span.x;
            if(lengths[i]<=0||lengths[i]>4)throw new InvalidOperationException("Unexpected gate gap.");
            var source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panelEast.gameObject);
            var piece=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),group.transform);
            piece.name=i==0?"West fence to pillar":"East fence to pillar";
            piece.transform.rotation=Quaternion.Euler(0,90,0);piece.transform.localScale=new Vector3(1,1,lengths[i]/panelEast.bounds.size.x);
            var mesh=piece.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Concrete_fence_v2_S");
            piece.transform.position+=new Vector3((span.x+span.y)*.5f-mesh.bounds.center.x,panelEast.bounds.min.y-mesh.bounds.min.y,panelEast.bounds.center.z-mesh.bounds.center.z);
            if(Mathf.Abs(mesh.bounds.min.x-span.x)>.002f||Mathf.Abs(mesh.bounds.max.x-span.y)>.002f)throw new InvalidOperationException("Fence endpoint alignment failed.");
        }

        var walls=new GameObject("Approach lane invisible walls");SceneManager.MoveGameObjectToScene(walls,game);
        walls.transform.SetParent(session.transform,false);Undo.RegisterCreatedObjectUndo(walls,"Keep enemies on exterior approach lanes");
        Action<string,Vector3,Vector3> wall=(name,center,size)=>
        {
            var go=new GameObject(name,typeof(BoxCollider),typeof(NavMeshModifier));go.transform.SetParent(walls.transform,false);
            go.transform.position=center;go.GetComponent<BoxCollider>().size=size;
            var modifier=go.GetComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
        };
        wall("West road north edge",new Vector3(-58.4f,3,.96f),new Vector3(20.5f,10,.35f));
        wall("West road south edge",new Vector3(-58.4f,3,-9.44f),new Vector3(20.5f,10,.35f));
        wall("West spawn outer end",new Vector3(-68.65f,3,-4.24f),new Vector3(.35f,10,10.75f));
        wall("North road west edge",new Vector3(-2.6f,3,55.5f),new Vector3(.35f,10,17.8f));
        wall("North road east edge",new Vector3(7.8f,3,55.5f),new Vector3(.35f,10,17.8f));
        wall("North spawn outer end",new Vector3(2.6f,3,64.4f),new Vector3(10.75f,10,.35f));
        Physics.SyncTransforms();
        if(walls.GetComponentsInChildren<Renderer>().Length!=0||walls.GetComponentsInChildren<BoxCollider>().Length!=6)throw new InvalidOperationException("Invalid invisible walls.");
        var surfaces=new[]{session.navigation}.Concat(session.additionalNavigation).ToArray();
        foreach(var surface in surfaces)
        {
            var saved=surface.navMeshData;surface.BuildNavMesh();var built=surface.navMeshData;
            if(!built)throw new InvalidOperationException("Navigation bake failed.");
            surface.RemoveData();EditorUtility.CopySerialized(built,saved);surface.navMeshData=saved;
            saved.name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(saved));UnityEngine.Object.DestroyImmediate(built);EditorUtility.SetDirty(saved);
        }
        EditorSceneManager.MarkSceneDirty(environment);EditorSceneManager.MarkSceneDirty(game);
        if(!EditorSceneManager.SaveScene(environment)||!EditorSceneManager.SaveScene(game))throw new IOException("Scene save failed.");
        AssetDatabase.SaveAssets();
        var result=new{northGatePillars=2,connectionLengths=lengths,wallColliders=6,navMeshes=surfaces.Length};
        File.WriteAllText("Docs/Integration/NorthGateAndLaneWalls.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
