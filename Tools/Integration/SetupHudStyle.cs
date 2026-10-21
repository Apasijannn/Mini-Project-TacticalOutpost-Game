using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Outpost.UI;
public static class SetupHudStyle
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TacticalOutpost/Gameplay/UI/Fonts/ShareTechMono-Regular.ttf");
        if(font==null)throw new InvalidOperationException("Menu monospace font is missing.");
        var previous=SceneManager.GetActiveScene();var menu=SceneManager.GetSceneByPath(GameMenu.MenuScene);
        bool opened=!menu.IsValid()||!menu.isLoaded;if(opened)menu=EditorSceneManager.OpenScene(GameMenu.MenuScene,OpenSceneMode.Additive);
        try
        {
            foreach(string path in new[]{GameMenu.MenuScene,GameMenu.GameScene})
            {
                var scene=SceneManager.GetSceneByPath(path);
                if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new InvalidOperationException("Expected clean open scene: "+path);
                var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameMenu>(true)).Single();
                Undo.RecordObject(controller,"Match minimap typography to BLACKOUT menu");controller.font=font;
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save HUD font.");
            }
        }
        finally {if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(menu,true);}
        return "HUD and minimap reuse BLACKOUT menu fonts; environment scene unchanged.";
    }
}
