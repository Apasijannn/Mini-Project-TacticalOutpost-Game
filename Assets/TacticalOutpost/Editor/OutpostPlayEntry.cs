using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Outpost.UI;

[InitializeOnLoad]
static class OutpostPlayEntry
{
    static OutpostPlayEntry()
    {
        EditorApplication.delayCall += Configure;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Configure();
        };
    }

    static void Configure()
    {
        if (EditorApplication.isPlaying) return;
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameMenu.MenuScene);
        if (menu == null) { Debug.LogError("BLACKOUT main menu scene is missing: " + GameMenu.MenuScene); return; }
        EditorSceneManager.playModeStartScene = menu;
    }
}
