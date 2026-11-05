using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Outpost.Player;

namespace Outpost.PCG
{
    /// <summary>
    /// Panel mode PCG. Ringkas selalu tampil (seed + status). F10 = panel detail + titik kandidat di dunia
    /// (hijau diterima, merah ditolak). F5 reroll supply, F6 seed baru, F7 ulangi seed, F9 eksperimen otomatis.
    /// </summary>
    public sealed class PcgDebugView : MonoBehaviour
    {
        bool details = false;   // F10: panel detail + titik kandidat PCG
        int supplyRoll;
        GUIStyle label;
        string lastExperiment;

        void Update()
        {
            var s = PcgSession.Instance;
            var kb = Keyboard.current;
            if (s == null || !s.Ready || kb == null) return;
            if (kb.f10Key.wasPressedThisFrame) details = !details;
            if (Time.timeScale <= 0f) return;
            if (kb.f5Key.wasPressedThisFrame) s.RerollSupply(++supplyRoll);
            if (kb.f6Key.wasPressedThisFrame) PcgMode.Restart(System.Environment.TickCount & 0x7fffffff);
            if (kb.f7Key.wasPressedThisFrame) PcgMode.Restart(s.Seed);
            if (kb.f11Key.wasPressedThisFrame)   // debug/demo: hancurkan generator untuk melihat sekuens kekalahan
                foreach (var g in new System.Collections.Generic.List<Outpost.Enemy.GeneratorHealth>(Outpost.Enemy.GeneratorHealth.All)) if (g != null) g.Damage(g.Health);
            if (kb.f9Key.wasPressedThisFrame)
            {
                lastExperiment = s.RunExperiments();
                HudEvents.ShowToast("PCG EXPERIMENTS SAVED: " + System.IO.Path.GetFileName(lastExperiment), new Color(.5f, 1f, .6f));
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var director = Outpost.Enemy.WaveDirector.Instance;   // sembunyikan panel saat cinematic kekalahan
            if (director != null && director.CurrentPhase == Outpost.Enemy.WaveDirector.Phase.Defeat) return;
            var s = PcgSession.Instance;
            if (s == null || !s.Ready || s.Arena == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
            var a = s.Arena;

            if (!details)
            {
                Panel(new List<string> { "PCG MODE   SEED " + s.Seed, "F10 details | F6 new seed | F7 replay" }, 300f);
                return;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 cp = cam.transform.position;
                foreach (var m in a.markers)
                {
                    if ((m.position - cp).sqrMagnitude > 70f * 70f) continue;
                    var sp = cam.WorldToScreenPoint(m.position + Vector3.up * 0.25f);
                    if (sp.z <= 0f) continue;
                    var prev = GUI.color;
                    GUI.color = m.rule == PcgRule.Accepted ? new Color(.3f, 1f, .35f, .9f) : new Color(1f, .25f, .2f, .7f);
                    float size = m.rule == PcgRule.Accepted ? 7f : 4f;
                    GUI.DrawTexture(new Rect(sp.x - size * .5f, Screen.height - sp.y - size * .5f, size, size), Texture2D.whiteTexture);
                    GUI.color = prev;
                }
            }

            var lines = new List<string>
            {
                "PCG MODE   SEED " + s.Seed + "   history: " + string.Join(", ", PcgMode.SeedHistory.Skip(1).Take(4)),
                "Arena " + (a.half * 2f).ToString("0") + " m   gates: " + string.Join("  ", a.gates.Select(g => g.lane + "@" + g.startSpan)),
                "Layout tries " + a.layoutAttempts + "   repair removals " + a.repairRemovals + "   connected " + (a.connected ? "YES" : "NO"),
                a.obstacleStats + "   rejected: " + a.obstacleStats.RejectionSummary(),
                "  " + string.Join("  ", a.obstacleCounts.Where(k => k.Value > 0).OrderByDescending(k => k.Value).Select(k => Short(k.Key) + " " + k.Value)),
            };
            foreach (var g in a.gates)
                lines.Add("  path " + g.lane + " gate -> generator: " + (a.gateReport.TryGetValue(g.lane, out var r) ? r : "-"));
            lines.Add(a.spawnStats + "  fallback " + a.spawnStats.fallback + "   rejected: " + a.spawnStats.RejectionSummary());
            foreach (var pl in s.wavePlans)
                lines.Add("Enemy plan W" + pl.wave + ": " + pl.Summary());
            if (s.ActivePlan != null)
                lines.Add("  spawned W" + s.ActivePlan.wave + ": " + string.Join("  ", s.spawnedThisWave.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)) +
                          (s.spawnedBosses > 0 ? "  boss " + s.spawnedBosses : ""));
            lines.Add(s.supplyStats + "  " + string.Join(" ", s.supplyCounts.Select(k => k.Key + ":" + k.Value)) + "   rejected: " + s.supplyStats.RejectionSummary());
            lines.Add(a.dressingStats + "   rejected: " + a.dressingStats.RejectionSummary());
            lines.Add("Green = accepted, red = rejected candidate");
            lines.Add("F10 hide | F5 reroll supply | F6 new seed | F7 replay seed | F9 experiments");
            Panel(lines, 560f);
        }

        static string Short(string prefab)
        {
            return prefab.Replace("_v1", "").Replace("PRE_", "").Replace("_LD1close", "").Replace("_LD2square", "").Replace("_garbadge", "");
        }

        void Panel(List<string> lines, float width)
        {
            float h = 16f * lines.Count + 12f;
            var rect = new Rect(Screen.width - width - 10f, 120f, width, h);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            label.normal.textColor = new Color(1f, .86f, .45f);
            for (int i = 0; i < lines.Count; i++) GUI.Label(new Rect(rect.x + 8f, rect.y + 5f + i * 16f, width - 12f, 18f), lines[i], label);
        }
    }
}
