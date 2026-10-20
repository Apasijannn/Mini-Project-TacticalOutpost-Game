#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Outpost.Player;

// Smoke test otomatis: masuk Play Mode, simulasi input (maju, tembak, reload), catat hasil + error ke BuilderStatus/play.txt, lalu keluar.
[InitializeOnLoad]
public static class OutpostPlayTest
{
    const string PrefKey = "OutpostPlayTest.active";
    static double t0; static int stage; static Vector3 startPos; static StringBuilder log;
    static FpsController ctrl; static FpsWeapon wpn; static PlayerHealth hp;

    static OutpostPlayTest()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(PrefKey, false) && EditorApplication.isPlaying) Begin();
    }

    static string Out { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus/play.txt")); } }

    public static string Start()
    {
        SessionState.SetBool(PrefKey, true);
        File.WriteAllText(Out, "mulai...\n");
        if (EditorApplication.isPlaying) { Begin(); return "playtest dimulai (sudah di play mode)"; }
        EditorApplication.EnterPlaymode();
        return "playtest dimulai";
    }

    static void OnState(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PrefKey, false)) Begin();
        if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PrefKey, false)) { SessionState.SetBool(PrefKey, false); }
    }

    static void Begin()
    {
        log = new StringBuilder();
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        t0 = EditorApplication.timeSinceStartup; stage = 0;
        // agar input simulasi diterima walau Game view tidak fokus (hanya selama test; dipulihkan di Finish)
        if (!prevSaved) { prevBehavior = InputSystem.settings.editorInputBehaviorInPlayMode; prevSaved = true; }
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if (!prevBgSaved) { prevBg = InputSystem.settings.backgroundBehavior; prevBgSaved = true; }
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }
    static bool prevBgSaved; static InputSettings.BackgroundBehavior prevBg;
    static bool prevSaved; static InputSettings.EditorInputBehaviorInPlayMode prevBehavior;

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
            log.AppendLine("[" + type + "] " + msg + "\n   " + (stack ?? "").Split('\n')[0]);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        double t = EditorApplication.timeSinceStartup - t0;
        if (stage == 0 && t > 1.0)
        {
            ctrl = Object.FindFirstObjectByType<FpsController>(); wpn = Object.FindFirstObjectByType<FpsWeapon>(); hp = Object.FindFirstObjectByType<PlayerHealth>();
            if (ctrl == null) { log.AppendLine("FpsController TIDAK DITEMUKAN"); Finish(); return; }
            foreach (var d in InputSystem.devices) { if (!d.enabled) { InputSystem.EnableDevice(d); log.AppendLine("enable device " + d.name); } }
            log.AppendLine("keyboard enabled " + (Keyboard.current != null && Keyboard.current.enabled) + " focus " + Application.isFocused + " bg " + InputSystem.settings.backgroundBehavior + " fps " + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("F0"));
            startPos = ctrl.transform.position;
            log.AppendLine("start pos " + startPos + " kamera " + (Camera.main != null) + " grounded " + ctrl.IsGrounded + " cursor " + Cursor.lockState + " ammo " + wpn.Ammo);
            if (Keyboard.current == null) { log.AppendLine("Keyboard.current NULL"); }
            else InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.S));   // mundur (arah depan = tembok)
            stage = 1;
        }
        else if (stage == 1 && t > 4.0)
        {
            log.AppendLine("frame " + Time.frameCount + " time " + Time.time.ToString("F2") + " setelah menekan S: pos " + ctrl.transform.position + " jarak " + Vector3.Distance(startPos, ctrl.transform.position).ToString("F2") + " grounded " + ctrl.IsGrounded);
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left, true));
            stage = 2;
        }
        else if (stage == 2 && t > 7.0)
        {
            log.AppendLine("frame " + Time.frameCount + " time " + Time.time.ToString("F2") + " setelah tembak: ammo " + wpn.Ammo + "/" + wpn.Reserve + " reloading " + wpn.IsReloading);
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            if (hp != null) hp.TakeDamage(30f, ctrl.transform.position, Vector3.forward);
            log.AppendLine("darah setelah 30 damage: " + (hp != null ? hp.Health.ToString() : "-"));
            stage = 3;
        }
        else if (stage == 3 && t > 7.5) Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        log.AppendLine("selesai.");
        File.WriteAllText(Out, log.ToString());
        SessionState.SetBool(PrefKey, false);
        if (prevSaved) { InputSystem.settings.editorInputBehaviorInPlayMode = prevBehavior; prevSaved = false; }
        if (prevBgSaved) { InputSystem.settings.backgroundBehavior = prevBg; prevBgSaved = false; }
        EditorApplication.ExitPlaymode();
    }
}
#endif
