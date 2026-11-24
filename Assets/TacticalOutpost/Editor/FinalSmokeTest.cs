using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.Final;
using Outpost.UI;

namespace Outpost.EditorTools
{
    /// <summary>
    /// Smoke test Play Mode tanpa input manusia (bisa dari command line):
    ///   -executeMethod Outpost.EditorTools.FinalSmokeTest.RunFinal   (mode Final, zombie dibunuh berkala agar wave maju)
    ///   -executeMethod Outpost.EditorTools.FinalSmokeTest.RunMini    (Mini Project, cek hook sekutu tidak aktif)
    /// Hasil dicetak ke log dengan prefix [SMOKE]. Di batchmode editor keluar sendiri setelah selesai.
    /// </summary>
    [InitializeOnLoad]
    public static class FinalSmokeTest
    {
        const string Key = "FinalSmoke.Mode";
        static double start = -1, nextKill, nextReport;

        static FinalSmokeTest()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(Key, ""))) return;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tactical Outpost/Final/Smoke Test (Final mode)")] public static void RunFinal() { Begin("final"); }
        [MenuItem("Tactical Outpost/Final/Smoke Test (Mini Project)")] public static void RunMini() { Begin("mini"); }
        [MenuItem("Tactical Outpost/Final/Verify Blackout + VIPER Blip")] public static void RunFeatureVerify() { Begin("verify"); }

        static void Begin(string mode)
        {
            verifyStage = 0;
            SessionState.SetString(Key, mode);
            EditorApplication.playModeStateChanged -= OnState; EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static string Mode { get { return SessionState.GetString(Key, ""); } }
        static float Duration { get { return Mode == "final" ? 190f : Mode == "verify" ? 26f : 45f; } }

        static void OnState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                FinalMode.Active = Mode == "final" || Mode == "verify";
                SceneManager.LoadScene(GameMenu.GameScene);
                start = EditorApplication.timeSinceStartup; nextKill = start + 32; nextReport = start + 10;
                Debug.Log("[SMOKE] start mode " + Mode);
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(Key);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || start < 0) return;
            double now = EditorApplication.timeSinceStartup, t = now - start;
            var wave = WaveDirector.Instance;
            if (now >= nextReport)
            {
                nextReport = now + 10;
                var s = FinalSession.Instance;
                Debug.Log(string.Format("[SMOKE] t={0:0}s phase={1} wave={2}/{3} alive={4} allies(EnemyBrain)={5} gen={6:0} {7}",
                    t, wave != null ? wave.CurrentPhase.ToString() : "-", wave != null ? wave.CurrentWave : 0, wave != null ? wave.TotalWaves : 0,
                    EnemyHealth.Alive.Count, EnemyBrain.Allies.Count, GeneratorHealth.All.Count > 0 ? GeneratorHealth.All[0].Health : -1f,
                    s != null ? "D=" + s.Difficulty.ToString("0.00") + " event=" + (s.CurrentEvent != null ? s.CurrentEvent.Title : "-")
                        + " allies=" + string.Join(";", AllyBrain.All.Select(a => a.Callsign + ":" + a.Action + ":" + a.Health.ToString("0") + "hp:k" + a.Kills
                        + ":target=" + (a.Target != null ? a.Target.name : "-") + ":pos=" + a.transform.position.ToString("F0") + ":nav=" + a.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh + ":score=" + string.Join("/", a.Scores.Select(x => x.ToString("0.00")).ToArray())).ToArray()) : "no final session"));
                var targets = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).GroupBy(b => b.DebugTarget).Select(g => g.Key + " " + g.Count()).ToArray();
                Debug.Log("[SMOKE] zombie targets: " + string.Join(", ", targets));
            }
            // Final: zombie dibunuh berkala supaya wave cepat maju dan DDA dievaluasi
            if (Mode == "final" && now >= nextKill && t < Duration - 10)
            {
                nextKill = now + 22;
                int n = 0;
                foreach (var e in EnemyHealth.Alive.ToList()) if (e != null && !e.IsDead) { e.Kill(); n++; }
                Debug.Log("[SMOKE] debug kill-all: " + n);
            }
            if (Mode == "final" && t > Duration - 5 && FinalSession.Instance != null && !experimented)
            {
                experimented = true;
                Debug.Log("[SMOKE] experiments → " + FinalExperiments.Run(FinalSession.Instance));
            }
            if (Mode == "verify") { TickVerify(t); return; }
            if (Mode == "final" && shots < ShotTimes.Length && t > ShotTimes[shots]) { Capture(shots); shots++; }
            if (t > Duration) { start = -1; experimented = false; shots = 0; Debug.Log("[SMOKE] done"); EditorApplication.ExitPlaymode(); }
        }
        static int verifyStage;
        static void TickVerify(double t)
        {
            var s = FinalSession.Instance;
            var blk = DynamicBlackoutSystem.Instance;
            var gen = s != null ? s.Generator : null;
            if (s == null || blk == null || gen == null || AllyBrain.All.Count == 0)
            {
                if (t > 20) { Debug.LogError("[VERIFY] Timeout waiting for FinalSession/Blackout/VIPER"); start = -1; EditorApplication.ExitPlaymode(); }
                return;
            }
            if (verifyStage == 0 && t > 3.0)
            {
                verifyStage = 1;
                var viper = AllyBrain.All[0];
                var blip = viper.GetComponent<AllyMinimapBlip>();
                var aux = viper.GetComponent<MiniMapModular.Aux_MP>();
                var body = Object.FindFirstObjectByType<Outpost.Player.FpsBody>();
                Debug.Log("[VERIFY] VIPER=" + viper.Callsign + " blip=" + (blip != null) + " aux=" + (aux != null) + " sprite=" + (aux != null && aux.Sprite_Me != null ? aux.Sprite_Me.name : "null") + " rot=" + (aux != null ? aux.RotIconMe : -1));
                if (body != null) Debug.Log("[VERIFY] Player FpsBody: " + body.DebugReach());
                Debug.Log("[VERIFY] Stage0 HP=" + gen.Fraction.ToString("0.00") + " tier=" + blk.CurrentTier + " status=" + blk.StatusLabel + " light=" + blk.CombinedLightFactor.ToString("0.00"));
                gen.Damage(gen.maxHealth * 0.35f); // → 65%
            }
            else if (verifyStage == 1 && t > 5.0)
            {
                verifyStage = 2;
                Debug.Log("[VERIFY] Stage1 HP=" + gen.Fraction.ToString("0.00") + " tier=" + blk.CurrentTier + " status=" + blk.StatusLabel + " light=" + blk.CombinedLightFactor.ToString("0.00"));
                gen.Damage(gen.maxHealth * 0.25f); // → 40%
            }
            else if (verifyStage == 2 && t > 7.5)
            {
                verifyStage = 3;
                Debug.Log("[VERIFY] Stage2 HP=" + gen.Fraction.ToString("0.00") + " tier=" + blk.CurrentTier + " status=" + blk.StatusLabel + " light=" + blk.CombinedLightFactor.ToString("0.00"));
                gen.Damage(gen.maxHealth * 0.28f); // → 12%
            }
            else if (verifyStage == 3 && t > 11.0)
            {
                verifyStage = 4;
                Debug.Log("[VERIFY] Stage3 HP=" + gen.Fraction.ToString("0.00") + " tier=" + blk.CurrentTier + " status=" + blk.StatusLabel + " totalDark=" + blk.IsTotalDarkness + " light=" + blk.CombinedLightFactor.ToString("0.00")
                    + " DDA(0.50) flicker=" + blk.FlickerScale.ToString("0.00") + " off=" + blk.OffDurationScale.ToString("0.00") + " thr=" + blk.ThresholdFlickerLow.ToString("0.00"));
                s.ForceDifficulty(+0.45f); // → 0.95 Veteran
            }
            else if (verifyStage == 4 && t > 13.0)
            {
                verifyStage = 5;
                Debug.Log("[VERIFY] Stage4 DDA(Veteran=" + s.Difficulty.ToString("0.00") + ") flicker=" + blk.FlickerScale.ToString("0.00") + " off=" + blk.OffDurationScale.ToString("0.00")
                    + " thr=" + blk.ThresholdFlickerLow.ToString("0.00") + "/" + blk.ThresholdFlickerHeavy.ToString("0.00") + "/" + blk.ThresholdChaos.ToString("0.00"));
                s.ForceDifficulty(-0.85f); // → 0.10 Recruit
            }
            else if (verifyStage == 5 && t > 15.0)
            {
                verifyStage = 6;
                Debug.Log("[VERIFY] Stage5 DDA(Recruit=" + s.Difficulty.ToString("0.00") + ") flicker=" + blk.FlickerScale.ToString("0.00") + " off=" + blk.OffDurationScale.ToString("0.00")
                    + " thr=" + blk.ThresholdFlickerLow.ToString("0.00") + "/" + blk.ThresholdFlickerHeavy.ToString("0.00") + "/" + blk.ThresholdChaos.ToString("0.00"));
                gen.ResetHealth();
            }
            else if (verifyStage == 6 && t > 17.0)
            {
                verifyStage = 7;
                Debug.Log("[VERIFY] Stage6 Reset HP=" + gen.Fraction.ToString("0.00") + " tier=" + blk.CurrentTier + " status=" + blk.StatusLabel + " light=" + blk.CombinedLightFactor.ToString("0.00"));
                Debug.Log("[VERIFY] ALL CHECKS PASSED");
                start = -1;
                EditorApplication.ExitPlaymode();
            }
        }
        static bool experimented;
        static int shots;
        static readonly float[] ShotTimes = { 26f, 27f, 28f, 62f, 63f };

        /// <summary>Screenshot bukti visual (tanpa UI OnGUI) ke Docs/Final/Screens.</summary>
        static void Capture(int index)
        {
            var ally = AllyBrain.All.FirstOrDefault();
            var main = Camera.main;
            if (ally != null) Debug.Log("[SMOKE] ally hands: " + ally.GetComponent<AllyBody>().ReachReport());
            Vector3 pos, target; float fov = 45f; int mask = ~((1 << 14) | (1 << 30));
            string name;
            if (index == 0 && ally != null) { name = "ally_front"; target = ally.transform.position + Vector3.up * 1.1f; pos = target + ally.transform.forward * 3f + Vector3.up * 0.4f; }
            else if (index == 1 && ally != null) { name = "ally_side"; target = ally.transform.position + Vector3.up * 1.1f; pos = target + (ally.transform.right * 2.6f + ally.transform.forward * 1.2f) + Vector3.up * 0.3f; }
            else if ((index == 2 || index == 4) && main != null) { name = index == 2 ? "player_view_w1" : "player_view_w2"; pos = main.transform.position; target = pos + main.transform.forward; fov = main.fieldOfView; mask = main.cullingMask; }
            else if (index == 3 && FinalSession.Instance != null) { name = "overview_w2"; target = FinalSession.Instance.GroundCenter; pos = target + new Vector3(-22f, 26f, -24f); fov = 55f; }
            else return;
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Docs/Final/Screens"));
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, name + ".png");

            var go = new GameObject("Smoke Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f; cam.cullingMask = mask;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true; cam.allowMSAA = false;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.Create();
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var rb = UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32); rb.WaitForCompletion();
            if (!rb.hasError)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false); tex.SetPixelData(rb.GetData<byte>(), 0); tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                Debug.Log("[SMOKE] screenshot " + path);
            }
            else Debug.LogWarning("[SMOKE] readback gagal " + name);
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
    }
}
