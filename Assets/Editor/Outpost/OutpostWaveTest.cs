#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Audio;
using Outpost.Enemy;
using Outpost.Player;

// Uji otomatis alur wave: masuk Play Mode, catat tiap wave (jumlah kiri/kanan, boss, skala), bunuh semua zombie
// supaya wave maju, render beberapa gambar, lalu keluar. Hasil: BuilderStatus/wave.txt
[InitializeOnLoad]
public static class OutpostWaveTest
{
    const string PrefKey = "OutpostWaveTest.active";
    static double t0, lastLog; static StringBuilder log; static WaveDirector wd; static PlayerHealth hp;
    static int lastWave = -1; static WaveDirector.Phase lastPhase = WaveDirector.Phase.Waiting;
    static double fightStart; static bool bossShot, spawnShot, doneNote; static double endAt;
    static string Dir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus")); } }

    static OutpostWaveTest()
    {
        EditorApplication.playModeStateChanged += OnState;
        if (SessionState.GetBool(PrefKey, false) && EditorApplication.isPlaying) Begin();
    }

    public static string Start()
    {
        SessionState.SetBool(PrefKey, true);
        File.WriteAllText(Dir + "/wave.txt", "mulai...\n");
        if (EditorApplication.isPlaying) { Begin(); return "wavetest dimulai (sudah di play mode)"; }
        EditorApplication.EnterPlaymode();
        return "wavetest dimulai";
    }

    static void OnState(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PrefKey, false)) Begin();
        if (s == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(PrefKey, false);
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
            log.AppendLine("[" + type + "] " + msg + "\n   " + (stack ?? "").Split('\n')[0]);
    }

    static void Begin()
    {
        log = new StringBuilder();
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Time.maximumDeltaTime = 1f; Time.timeScale = 4f;   // editor tanpa fokus jalan lambat; percepat waktu game
        t0 = EditorApplication.timeSinceStartup; lastLog = 0; lastWave = -1; bossShot = false; spawnShot = false; doneNote = false; endAt = 0;
    }

    static void Shot(string name, Vector3 pos, Vector3 look)
    {
        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var cgo = new GameObject("TMP_WT"); var cam = cgo.AddComponent<Camera>();
        var cd = cgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = true;
        cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f; cam.fieldOfView = 60f;
        cgo.transform.position = pos; cgo.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        cam.Render();
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(Dir + "/" + name + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(cgo);
    }

    static void Describe()
    {
        int wc = 0, ec = 0, boss = 0, off = 0; var kinds = new Dictionary<string, int>();
        foreach (var e in EnemyHealth.Alive)
        {
            if (e == null) continue;
            if (e.transform.position.x < 0f) wc++; else ec++;
            string k = e.config != null ? e.config.displayName : "?"; int c; kinds.TryGetValue(k, out c); kinds[k] = c + 1;
            if (e.config != null && e.config.kind == EnemyKind.Boss) boss++;
            var ag = e.GetComponent<NavMeshAgent>(); if (ag == null || !ag.isOnNavMesh) off++;
        }
        log.AppendLine("   hidup: " + EnemyHealth.Alive.Count + " | barat(x<0)=" + wc + " timur(x>0)=" + ec + " boss=" + boss + " diLuarNavMesh=" + off);
        var sb = new StringBuilder("   jenis: "); foreach (var kv in kinds) sb.Append(kv.Key + "=" + kv.Value + "  "); log.AppendLine(sb.ToString());
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        double t = EditorApplication.timeSinceStartup - t0;
        if (wd == null) { wd = Object.FindFirstObjectByType<WaveDirector>(); hp = Object.FindFirstObjectByType<PlayerHealth>(); if (wd != null) { wd.betweenWavesDelay = 2f; wd.bannerTime = 1.5f; log.AppendLine("WaveDirector ketemu; AudioManager=" + (OutpostAudio.Instance != null)); } else if (t > 3) { log.AppendLine("WaveDirector TIDAK ADA"); Finish(); } return; }
        if (t - lastLog >= 5) { lastLog = t; log.AppendLine("diag t=" + t.ToString("F0") + " Time.time=" + Time.time.ToString("F2") + " frame=" + Time.frameCount + " dt=" + Time.deltaTime.ToString("F3") + " scale=" + Time.timeScale + " paused=" + EditorApplication.isPaused + " phase=" + wd.CurrentPhase + " enabled=" + wd.enabled + " active=" + wd.gameObject.activeInHierarchy); }
        if (hp != null && hp.Health < hp.maxHealth) hp.ResetHealth();   // player tidak boleh mati selama uji

        if (wd.CurrentWave != lastWave || wd.CurrentPhase != lastPhase)
        {
            log.AppendLine("t=" + t.ToString("F1") + "s wave=" + wd.CurrentWave + " fase=" + wd.CurrentPhase + " alive(tracked)=" + wd.EnemiesAlive);
            if (wd.CurrentPhase == WaveDirector.Phase.Fighting) { fightStart = t; Describe(); }
            lastWave = wd.CurrentWave; lastPhase = wd.CurrentPhase;
        }

        // gambar: tepat saat wave 1 selesai spawn (kedua gerbang) dan saat boss muncul
        if (!spawnShot && wd.CurrentWave == 1 && wd.CurrentPhase == WaveDirector.Phase.Fighting && t - fightStart > 0.5)
        {
            spawnShot = true; Shot("wave_gen", new Vector3(22f, 8f, -0.7f), new Vector3(1.6f, 5f, -0.7f)); Shot("wave_w1_west", new Vector3(-30f, 14f, -4f), new Vector3(-54f, 0.5f, 0f)); Shot("wave_w1_east", new Vector3(30f, 14f, -4f), new Vector3(54f, 0.5f, 0f));
        }
        foreach (var e in EnemyHealth.Alive)
        {
            if (e == null || e.config == null || e.config.kind != EnemyKind.Boss) continue;
            if (!bossShot && t - lastLog >= 0)
            {
                bossShot = true;
                var ag = e.GetComponent<NavMeshAgent>();
                log.AppendLine("BOSS: " + e.name + " localScale " + e.transform.localScale + " lossy " + e.transform.lossyScale + " modelScale " + e.transform.GetChild(0).lossyScale + " HP " + e.MaxHealth + " pos " + e.transform.position + " onNav " + ag.isOnNavMesh + " agentR " + ag.radius + " H " + ag.height + " audio " + (e.GetComponent<EnemyAudio>() != null));
                Zombie_Near(e);
            }
        }
        if (bossShot && wd.CurrentPhase == WaveDirector.Phase.Fighting && !bossShotDone && t - bossT > 2.0)
        {
            bossShotDone = true;
            foreach (var e in EnemyHealth.Alive) if (e != null && e.config != null && e.config.kind == EnemyKind.Boss)
            { var p = e.transform.position; Shot("wave_boss", p + new Vector3(0f, 2.6f, 0f) + (-e.transform.forward * 7f), p + Vector3.up * 1.6f); log.AppendLine("BOSS setelah 2s: pos " + e.transform.position + " state " + e.GetComponent<EnemyBrain>().CurrentState); }
        }

        // simulasi player: 4 detik setelah wave mulai bertarung, bunuh semua yang hidup (boss: 8 detik)
        bool boss = false; foreach (var e in EnemyHealth.Alive) if (e != null && e.config != null && e.config.kind == EnemyKind.Boss) boss = true;
        if (wd.CurrentPhase == WaveDirector.Phase.Fighting && t - fightStart > (boss ? 9.0 : 4.0))
        {
            int n = EnemyHealth.Alive.Count; EnemyDebugSpawner.KillAll(); log.AppendLine("t=" + t.ToString("F1") + " KillAll (" + n + ")");
        }
        if (wd.CurrentPhase == WaveDirector.Phase.Victory && endAt == 0) { endAt = t + 3; log.AppendLine("t=" + t.ToString("F1") + " VICTORY tercapai"); Shot("wave_victory", new Vector3(0, 3, 0), new Vector3(0, 3, 10)); }
        if (endAt > 0 && t > endAt) Finish();
        if (t > 60) { log.AppendLine("TIMEOUT fase " + wd.CurrentPhase + " wave " + wd.CurrentWave); Finish(); }
    }
    static bool bossShotDone; static double bossT;
    static void Zombie_Near(EnemyHealth boss) { bossT = EditorApplication.timeSinceStartup - t0; bossShotDone = false; }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        log.AppendLine("selesai.");
        File.WriteAllText(Dir + "/wave.txt", log.ToString());
        SessionState.SetBool(PrefKey, false);
        wd = null; Time.timeScale = 1f;
        EditorApplication.ExitPlaymode();
    }
}
#endif
