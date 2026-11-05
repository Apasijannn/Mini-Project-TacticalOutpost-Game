using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Outpost.Enemy;
using Outpost.UI;
public static class SetupTargetQuota
{
    public static object Main()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var scene=SceneManager.GetSceneByPath(GameMenu.GameScene);
        if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new InvalidOperationException("Open a clean gameplay scene first.");
        var generator=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratorHealth>(true)).Single();
        Undo.RecordObject(generator,"Set generator health to 1000");generator.maxHealth=1000;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{generatorMaxHealth=generator.maxHealth,scene=scene.path};
    }
}
