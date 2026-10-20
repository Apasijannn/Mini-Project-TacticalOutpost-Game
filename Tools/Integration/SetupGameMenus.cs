using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.UI;

public static class SetupGameMenus
{
    public static object Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        const string atlas="Assets/ThirdParty/StrategicWarfareUI/Textures/HUF Pack.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(atlas);
        importer.maxTextureSize=4096;importer.SaveAndReimport();
        var button=AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().Single(s=>s.name=="UI Starter Pack_1");
        var gameplay=SceneManager.GetSceneByPath(GameMenu.GameScene);
        if(!gameplay.IsValid())throw new InvalidOperationException("Open gameplay scene first.");
        EditorSceneManager.SaveScene(gameplay,"C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_MenuAndMarkers/GameplayAuthoringBefore.unity",true);
        var session=gameplay.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Outpost.IndustrialGameSession>(true)).Single();
        var pause=session.systems.GetComponent<GameMenu>();
        if(pause==null)pause=Undo.AddComponent<GameMenu>(session.systems);
        pause.primaryButton=pause.secondaryButton=button;EditorUtility.SetDirty(pause);
        EditorSceneManager.MarkSceneDirty(gameplay);EditorSceneManager.SaveScene(gameplay);
        var previous=SceneManager.GetActiveScene();
        var menu=SceneManager.GetSceneByPath(GameMenu.MenuScene);
        if(menu.IsValid())throw new InvalidOperationException("Menu scene already open; inspect before rerunning.");
        if(System.IO.File.Exists(GameMenu.MenuScene))throw new InvalidOperationException("Menu already exists.");
        menu=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(menu);
            var root=new GameObject("Main menu");var controller=root.AddComponent<GameMenu>();controller.mainMenu=true;controller.primaryButton=controller.secondaryButton=button;
            var camera=new GameObject("Menu camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.028f,.045f);camera.cullingMask=0;
            EditorSceneManager.SaveScene(menu,GameMenu.MenuScene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(GameMenu.MenuScene,true),new EditorBuildSettingsScene(GameMenu.GameScene,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=GameMenu.MenuScene&&s.path!=GameMenu.GameScene)).ToArray();
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(GameMenu.MenuScene);
        }
        finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(menu,true);}
        return new{menu=GameMenu.MenuScene,pauseAdded=true,atlas=atlas,button=button.name,startScene=EditorSceneManager.playModeStartScene.name};
    }
}
