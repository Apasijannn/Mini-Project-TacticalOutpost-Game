using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

public static class CheckNorthGatePreservation
{
    static string Number(float n) => Math.Round(n,4).ToString("F4",CultureInfo.InvariantCulture);
    static string Vector(Vector3 v) => Number(v.x)+","+Number(v.y)+","+Number(v.z);
    static string Key(Transform t) => t.name+"|"+Vector(t.position)+"|"+Vector(t.rotation.eulerAngles)+"|"+Vector(t.localScale)+"|"+t.gameObject.activeSelf;
    static Transform[] All(Scene s) => s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    public static object Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var before=EditorSceneManager.OpenPreviewScene("C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_NorthGateLaneWalls/TacticalOutpost_Solo.unity");
        var after=EditorSceneManager.OpenPreviewScene(Path.GetFullPath("Assets/Scenes/TacticalOutpost_Solo.unity"));
        try
        {
            var old=All(before);var current=All(after).ToList();var missing=old.Where(t=>{int i=current.FindIndex(c=>Key(c)==Key(t));if(i<0)return true;current.RemoveAt(i);return false;}).Select(t=>t.name).ToArray();
            var result=new{passed=missing.Length==0,original=old.Length,unmatchedOriginal=missing,added=current.Select(t=>t.name).ToArray(),method="Saved scene multiset: name, world transform, local scale, active flag at 0.0001 precision"};
            File.WriteAllText("Docs/Integration/NorthGatePreservationChecks.json",JsonConvert.SerializeObject(result,Formatting.Indented));
            if(!result.passed)throw new InvalidOperationException("Original environment objects changed: "+string.Join(", ",missing));
            return result;
        }
        finally{EditorSceneManager.ClosePreviewScene(after);EditorSceneManager.ClosePreviewScene(before);}
    }
}
