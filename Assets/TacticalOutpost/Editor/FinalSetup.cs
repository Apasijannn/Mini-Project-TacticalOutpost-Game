using System.IO;
using UnityEditor;
using UnityEngine;
using Outpost.Final;

namespace Outpost.EditorTools
{
    /// <summary>Menu Tactical Outpost → Final: membuat/memilih Final_Profile dan membuka hasil eksperimen.</summary>
    public static class FinalSetup
    {
        const string Folder = "Assets/TacticalOutpost/Gameplay/Resources/Final";
        const string AssetPath = Folder + "/Final_Profile.asset";

        [MenuItem("Tactical Outpost/Final/Create or Select Profile")]
        public static void CreateOrSelect()
        {
            var profile = AssetDatabase.LoadAssetAtPath<FinalProfile>(AssetPath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/TacticalOutpost/Gameplay/Resources", "Final");
                profile = ScriptableObject.CreateInstance<FinalProfile>();
                AssetDatabase.CreateAsset(profile, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[FINAL] Profile dibuat: " + AssetPath);
            }
            if (!Application.isBatchMode) { Selection.activeObject = profile; EditorGUIUtility.PingObject(profile); }
        }

        [MenuItem("Tactical Outpost/Final/Open Experiment Results")]
        static void OpenResults()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Final/ExperimentResults.md"));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[FINAL] Belum ada hasil. Main mode Final lalu tekan F9.");
        }

        /// <summary>Dipanggil dari command line: -executeMethod Outpost.EditorTools.FinalSetup.Batch</summary>
        public static void Batch() { CreateOrSelect(); }
    }
}
