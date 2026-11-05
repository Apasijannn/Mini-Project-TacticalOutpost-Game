using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Outpost.UI;
using Outpost.Enemy;
using Outpost.Player;
public static class PlayEntryChecks
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static object Prepare()
    {
        Check(!Application.isPlaying,"Stop Play Mode first.");
        Check(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)==GameMenu.MenuScene,"Startup callback did not configure the menu.");
        Directory.CreateDirectory("Docs/Integration");
        File.WriteAllText("Temp/PlayEntryEnvironmentHash.txt",Hash());
        EditorSceneManager.playModeStartScene=null;
        return new{startupConfigured=true,clearedSettingToCheckPlayCallback=true};
    }
    public static async Task<object> Main()
    {
        Check(Application.isPlaying,"Press Play first.");
        bool background=Application.runInBackground;Application.runInBackground=true;
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        try
        {
            float deadline=Time.realtimeSinceStartup+20;
            while(GameMenu.Instance==null||!GameMenu.Instance.mainMenu||GameMenu.Instance.GetComponent<MenuBackdrop>()==null||!GameMenu.Instance.GetComponent<MenuBackdrop>().Ready)
            {Check(Application.isPlaying&&Time.realtimeSinceStartup<deadline,"Play did not reach the ready main menu.");await Task.Delay(20);}
            Check(SceneManager.GetSceneByPath(GameMenu.MenuScene).isLoaded,"Menu scene was not loaded.");
            Check(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)==GameMenu.MenuScene,"Play callback did not restore the startup setting.");
            Check(WaveDirector.Instance==null&&PlayerHealth.Instance==null&&EnemyHealth.Alive.Count==0,"Combat started behind the main menu.");
            Check(Hash()==File.ReadAllText("Temp/PlayEntryEnvironmentHash.txt"),"Environment file changed.");
            int frame=Time.frameCount;await Task.Delay(200);Check(Time.frameCount>frame,"Menu is frozen.");
            var result=new{passed=true,menuReady=true,restoresMissingStartSetting=true,noGameplayOrWaveStarted=true,environmentUnchanged=true,menuAdvancing=true};
            File.WriteAllText("Docs/Integration/PlayEntryChecks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally {Application.runInBackground=background;}
    }
    static string Hash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Scenes/TacticalOutpost_Solo.unity"))).Replace("-","");}
}
