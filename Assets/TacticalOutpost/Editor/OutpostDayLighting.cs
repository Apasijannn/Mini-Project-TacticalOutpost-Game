using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class OutpostDayLighting
{
    [MenuItem("Tactical Outpost/Lighting/Pagi")]
    public static void Pagi()=>Apply("Pagi");
    [MenuItem("Tactical Outpost/Lighting/Noon")]
    public static void Noon()=>Apply("Noon");
    [MenuItem("Tactical Outpost/Lighting/Afternoon")]
    public static void Afternoon()=>Apply("Afternoon");
    [MenuItem("Tactical Outpost/Lighting/Sore")]
    public static void Sore()=>Apply("Sore");

    [MenuItem("Tactical Outpost/Lighting/Pagi",true)]
    [MenuItem("Tactical Outpost/Lighting/Noon",true)]
    [MenuItem("Tactical Outpost/Lighting/Afternoon",true)]
    [MenuItem("Tactical Outpost/Lighting/Sore",true)]
    public static bool Available()=>!EditorApplication.isPlaying&&SceneManager.GetActiveScene().path=="Assets/Scenes/TacticalOutpost_Solo.unity";

    public static void Apply(string preset)
    {
        if(!Available())throw new InvalidOperationException("Open TacticalOutpost_Solo in Edit Mode.");
        if(preset!="Pagi"&&preset!="Noon"&&preset!="Afternoon"&&preset!="Sore")throw new ArgumentException("Unknown daylight preset.");
        var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Outpost lighting - "+preset);
        var settings=(UnityEngine.Object)typeof(RenderSettings).GetMethod("GetRenderSettings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);Undo.RecordObject(settings,"Day environment");
        float elevation=65,intensity=1.4f,exposure=1.15f;Color sunColor=new Color(1,.98f,.94f),skyAmbient=new Color(.55f,.63f,.75f),equator=new Color(.42f,.47f,.54f);
        if(preset=="Pagi"){elevation=35;intensity=1.3f;sunColor=new Color(1,.94f,.83f);}
        if(preset=="Afternoon"){elevation=40;intensity=1.3f;exposure=1.1f;sunColor=new Color(1,.92f,.78f);skyAmbient=new Color(.51f,.59f,.71f);equator=new Color(.42f,.44f,.49f);}
        if(preset=="Sore"){elevation=18;intensity=1.1f;exposure=1;sunColor=new Color(1,.76f,.50f);skyAmbient=new Color(.46f,.50f,.62f);equator=new Color(.37f,.36f,.42f);}
        string skyPath="Assets/TacticalOutpost/Materials/Outpost_"+preset+"Sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(!sky){sky=new Material(Shader.Find("Skybox/Procedural")){name="Outpost "+preset+" Sky"};AssetDatabase.CreateAsset(sky,skyPath);}else Undo.RecordObject(sky,"Day sky");
        sky.SetFloat("_SunDisk",2);sky.SetFloat("_SunSize",.04f);sky.SetFloat("_SunSizeConvergence",5);sky.SetFloat("_Exposure",exposure);sky.SetFloat("_AtmosphereThickness",1);sky.SetColor("_SkyTint",new Color(.5f,.5f,.5f));sky.SetColor("_GroundColor",new Color(.37f,.35f,.34f));EditorUtility.SetDirty(sky);
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;RenderSettings.ambientSkyColor=skyAmbient;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=preset=="Sore"?new Color(.24f,.23f,.25f):new Color(.30f,.31f,.33f);
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;RenderSettings.reflectionIntensity=.8f;RenderSettings.fog=false;
        var sun=root.GetComponentsInChildren<Light>(true).Single(l=>l.type==LightType.Directional);Undo.RecordObject(sun,"Day sunlight");Undo.RecordObject(sun.transform,"Sun elevation");sun.enabled=true;sun.intensity=intensity;sun.color=sunColor;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(elevation,-35,0);RenderSettings.sun=sun;
        var night=root.Find("Lighting/Night atmosphere");if(night){Undo.RecordObject(night.gameObject,"Disable night grading");night.gameObject.SetActive(false);}
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/SpaceZeta_StreetLamps/Materials/StreetLamp2Concrete_mat.mat");
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith("Assets/SpaceZeta_StreetLamps/"))){
            foreach(var light in t.GetComponentsInChildren<Light>(true)){Undo.RecordObject(light,"Street lamps off during daytime");light.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
            foreach(var renderer in t.GetComponentsInChildren<Renderer>(true)){Undo.RecordObject(renderer,"Day fixture material");renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&AssetDatabase.GetAssetPath(m)=="Assets/TacticalOutpost/Materials/StreetLamp2_Concrete_Night.mat"?source:m).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        }
        foreach(var light in root.GetComponentsInChildren<Light>(true).Where(l=>l.name=="Outward flood beam"&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(l.gameObject)=="Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab")){Undo.RecordObject(light,"Tower lights off during daytime");light.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
        foreach(SceneView view in SceneView.sceneViews){view.sceneLighting=true;view.sceneViewState.showSkybox=true;view.sceneViewState.showImageEffects=true;view.Repaint();}
        if(!sky.shader.isSupported||RenderSettings.sun!=sun||night&&night.gameObject.activeSelf)throw new InvalidOperationException("Daylight setup incomplete.");
        DynamicGI.UpdateEnvironment();EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);Debug.Log("Outpost daylight: "+preset+". Save the scene to keep this preset.");
    }
}

public class OutpostLightingToggle : EditorWindow
{
    public static bool IsNight()
    {
        var root=SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(g=>g.name=="TacticalOutpost");
        var night=root?root.transform.Find("Lighting/Night atmosphere"):null;
        return night&&night.gameObject.activeSelf;
    }

    [MenuItem("Tactical Outpost/Lighting/Panel Pagi - Malam",false,0)]
    public static void Open()
    {
        var window=GetWindow<OutpostLightingToggle>("Outpost Lighting");
        window.minSize=new Vector2(250,85);window.Show();
    }

    [MenuItem("Tactical Outpost/Lighting/Toggle Pagi - Malam %&l",false,1)]
    public static void Toggle()=>SetNight(!IsNight());
    [MenuItem("Tactical Outpost/Lighting/Toggle Pagi - Malam %&l",true)]
    static bool CanToggle()=>OutpostDayLighting.Available();

    public static void SetNight(bool night)
    {
        if(!OutpostDayLighting.Available())throw new InvalidOperationException("Open TacticalOutpost_Solo in Edit Mode.");
        if(night)OutpostNightPreset.Apply();else OutpostDayLighting.Pagi();
        if(IsNight()!=night)throw new InvalidOperationException("Lighting toggle did not apply.");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    void OnGUI()
    {
        bool available=OutpostDayLighting.Available();
        EditorGUILayout.LabelField("Suasana",EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(!available)){
            int current=IsNight()?1:0;
            int next=GUILayout.Toolbar(current,new[]{"Pagi","Malam"},GUILayout.Height(30));
            if(next!=current)SetNight(next==1);
        }
        if(!available)EditorGUILayout.HelpBox("Buka TacticalOutpost_Solo di Edit Mode.",MessageType.Info);
        else EditorGUILayout.LabelField("Ctrl + Alt + L untuk toggle. Scene otomatis disimpan.",EditorStyles.miniLabel);
    }
    void OnInspectorUpdate()=>Repaint();
}
