using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OutpostNightPreset
{
    public static string Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TacticalOutpost_Solo.unity"||EditorApplication.isPlaying)throw new InvalidOperationException("Open the outpost in Edit Mode");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="TacticalOutpost").transform;
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Outpost horror night lighting");
        var renderSettings=(UnityEngine.Object)typeof(RenderSettings).GetMethod("GetRenderSettings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
        Undo.RecordObject(renderSettings,"Night environment");
        const string skyPath="Assets/TacticalOutpost/Materials/Outpost_NightSky.mat";
        var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(!sky){sky=new Material(Shader.Find("Skybox/Procedural")){name="Outpost Night Sky"};AssetDatabase.CreateAsset(sky,skyPath);}else Undo.RecordObject(sky,"Night sky");
        sky.SetFloat("_SunDisk",0);sky.SetFloat("_Exposure",.18f);sky.SetFloat("_AtmosphereThickness",.7f);
        sky.SetColor("_SkyTint",new Color(.12f,.18f,.3f));sky.SetColor("_GroundColor",new Color(.008f,.012f,.022f));EditorUtility.SetDirty(sky);
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
        RenderSettings.ambientSkyColor=new Color(.28f,.33f,.45f);RenderSettings.ambientEquatorColor=new Color(.18f,.22f,.30f);RenderSettings.ambientGroundColor=new Color(.10f,.12f,.17f);
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;RenderSettings.reflectionIntensity=.18f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.007f;RenderSettings.fogColor=new Color(.018f,.026f,.044f);
        var moon=root.GetComponentsInChildren<Light>(true).Single(l=>l.type==LightType.Directional);
        Undo.RecordObject(moon,"Cold moonlight");Undo.RecordObject(moon.transform,"Moon angle");moon.enabled=true;moon.color=new Color(.6f,.72f,1);moon.intensity=.7f;moon.shadows=LightShadows.Soft;moon.transform.rotation=Quaternion.Euler(28,-35,0);RenderSettings.sun=moon;
        var lighting=root.Find("Lighting");var volumeTransform=lighting.Find("Night atmosphere");
        if(!volumeTransform){var g=new GameObject("Night atmosphere");SceneManager.MoveGameObjectToScene(g,scene);g.transform.SetParent(lighting,false);Undo.RegisterCreatedObjectUndo(g,"Night atmosphere");volumeTransform=g.transform;}
        Undo.RecordObject(volumeTransform.gameObject,"Enable night atmosphere");volumeTransform.gameObject.SetActive(true);
        var volume=volumeTransform.GetComponent<Volume>();if(!volume)volume=Undo.AddComponent<Volume>(volumeTransform.gameObject);Undo.RecordObject(volume,"Night grading");volume.isGlobal=true;volume.priority=20;volume.weight=1;
        const string profilePath="Assets/TacticalOutpost/Materials/Outpost_NightProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Outpost Night Profile";AssetDatabase.CreateAsset(profile,profilePath);}volume.sharedProfile=profile;
        if(!profile.TryGet<Tonemapping>(out var tonemapping))tonemapping=profile.Add<Tonemapping>();tonemapping.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<ColorAdjustments>(out var color))color=profile.Add<ColorAdjustments>();color.postExposure.Override(.6f);color.contrast.Override(5);color.saturation.Override(-12);
        if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>();bloom.threshold.Override(.9f);bloom.intensity.Override(.35f);bloom.scatter.Override(.65f);
        if(!profile.TryGet<Vignette>(out var vignette))vignette=profile.Add<Vignette>();vignette.intensity.Override(.22f);vignette.smoothness.Override(.5f);
        foreach(var component in profile.components){if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(component);}EditorUtility.SetDirty(profile);
        foreach(var camera in root.GetComponentsInChildren<Camera>(true)){var data=camera.GetComponent<UniversalAdditionalCameraData>();if(!data)data=Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);Undo.RecordObject(data,"Enable night colour grading");data.renderPostProcessing=true;}
        const string lampPath="Assets/TacticalOutpost/Materials/StreetLamp2_Concrete_Night.mat";
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/SpaceZeta_StreetLamps/Materials/StreetLamp2Concrete_mat.mat");var lampMaterial=AssetDatabase.LoadAssetAtPath<Material>(lampPath);
        if(!lampMaterial){lampMaterial=new Material(source){name="Street Lamp Concrete - Night"};AssetDatabase.CreateAsset(lampMaterial,lampPath);}
        lampMaterial.SetColor("_EmissionColor",new Color(3.2f,2.7f,1.8f));EditorUtility.SetDirty(lampMaterial);
        var lamps=root.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith("Assets/SpaceZeta_StreetLamps/")).ToArray();
        foreach(var renderer in lamps.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true))) {
            Undo.RecordObject(renderer,"Night fixture emission");renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m==source||m==lampMaterial?lampMaterial:m).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        foreach(var light in lamps.SelectMany(t=>t.GetComponentsInChildren<Light>(true))){Undo.RecordObject(light,"Street lamp night pool");light.enabled=true;if(light.type==LightType.Spot){light.intensity=45;light.range=20;light.spotAngle=115;light.innerSpotAngle=65;}PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
        foreach(var light in root.GetComponentsInChildren<Light>(true).Where(l=>l.name=="Outward flood beam"&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(l.gameObject)=="Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab")){Undo.RecordObject(light,"Enable tower and generator floodlights");light.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(light);}
        foreach(SceneView view in SceneView.sceneViews){view.sceneLighting=true;view.sceneViewState.showFog=true;view.sceneViewState.showSkybox=true;view.sceneViewState.showImageEffects=true;view.Repaint();}
        DynamicGI.UpdateEnvironment();EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        if(RenderSettings.sun.intensity>.8f||!RenderSettings.fog||!volume.sharedProfile||!sky.shader.isSupported||profile.components.Any(c=>!AssetDatabase.Contains(c)))throw new InvalidOperationException("Night lighting setup is incomplete");
        return "Night sky, cold moonlight, warm street lamps, distance fog and subtle grading applied; main scene saved.";
    }
}
