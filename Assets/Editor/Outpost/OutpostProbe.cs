#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class OutpostProbe
{
    static OutpostProbe()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus"));
            Directory.CreateDirectory(dir);
            string rp = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in";
            string txt = "Probe OK " + DateTime.Now.ToString("HH:mm:ss") +
                "\nUnity " + Application.unityVersion +
                "\nRenderPipeline " + rp +
                "\nActiveScene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path +
                "\nNavMeshSurfaceType " + (Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation") != null) +
                "\nInputSystemOnly " + PlayerSettings.GetPropertyBool("useOnlyNewInputSystem") ;
            File.WriteAllText(Path.Combine(dir, "probe.txt"), txt);
        }
        catch (Exception e)
        {
            Debug.LogError("OutpostProbe failed: " + e);
        }
    }
}
#endif
