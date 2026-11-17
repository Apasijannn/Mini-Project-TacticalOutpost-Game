using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Outpost.Enemy;
using Outpost.Player;

namespace Outpost.Final
{
    /// <summary>
    /// Panel mode Final. Ringkas selalu tampil (seed, DDA, event, sekutu) + tag nama sekutu di dunia.
    /// F10 detail (indikator performa, skor, output DDA, skor utility sekutu, titik taktis di dunia, log).
    /// F5 DDA on/off · F6/F7 difficulty +/- 0,2 (demo) · F9 eksperimen → Docs/Final/ExperimentResults.md.
    /// </summary>
    public sealed class FinalDebugView : MonoBehaviour
    {
        bool details;
        GUIStyle label, tagStyle;

        void Update()
        {
            var s = FinalSession.Instance;
            var kb = Keyboard.current;
            if (s == null || kb == null || s.Profile == null) return;
            if (kb.f10Key.wasPressedThisFrame) details = !details;
            if (Time.timeScale <= 0f) return;
            if (kb.f5Key.wasPressedThisFrame) { s.DdaEnabled = !s.DdaEnabled; s.Log("DDA " + (s.DdaEnabled ? "ON" : "OFF (baseline Mini Project values)")); }
            if (kb.f6Key.wasPressedThisFrame) s.ForceDifficulty(+0.2f);
            if (kb.f7Key.wasPressedThisFrame) s.ForceDifficulty(-0.2f);
            if (kb.f9Key.wasPressedThisFrame)
            {
                string path = FinalExperiments.Run(s);
                HudEvents.ShowToast("FINAL EXPERIMENTS SAVED: " + System.IO.Path.GetFileName(path), new Color(.5f, 1f, .6f));
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var director = WaveDirector.Instance;
            if (director != null && director.CurrentPhase == WaveDirector.Phase.Defeat) return;
            var s = FinalSession.Instance;
            if (s == null || s.Profile == null || s.Tracker == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
                tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            var cam = Camera.main;
            if (cam != null) { AllyTags(cam); if (details) TacticalMarkers(cam, s); }

            var lines = new List<string>
            {
                "FINAL MODE   SEED " + s.Seed + "   DDA " + (s.DdaEnabled ? "ON" : "OFF") + "   D " + s.Difficulty.ToString("0.00") + " " + DdaModel.Label(s.Difficulty) + "   skill " + s.Skill.ToString("0.00"),
                "Event: " + (s.CurrentEvent != null ? s.CurrentEvent.Title : "-") + "   next: " + (s.NextEvent != null ? s.NextEvent.Title + " (D' " + s.PreviewDifficulty.ToString("0.00") + ")" : "-"),
            };
            foreach (var a in AllyBrain.All)
                lines.Add("Ally " + a.Callsign + "  " + AllyBrain.ActionNames[(int)a.Action] + "  HP " + a.Health.ToString("0") + "/" + a.MaxHealth.ToString("0") + "  kills " + a.Kills);
            if (!details) { lines.Add("F10 details | F5 DDA on/off | F6/F7 difficulty +/-"); Panel(lines, 470f); return; }

            var w = s.Tracker.Current;
            var sc = s.PreviewScore;
            var o = s.DdaOut;
            lines.Add("");
            lines.Add("PLAYER PERFORMANCE (current wave window, " + w.seconds.ToString("0") + " s)");
            lines.Add(string.Format("  accuracy {0}/{1} = {2:0%} → {3:0.00}   damage taken {4:0} → {5:0.00}", w.hits, w.shots, w.Accuracy, sc.accuracy, w.damageTaken, sc.damage));
            lines.Add(string.Format("  generator lost {0:0} → {1:0.00}   kills {2} (ally {3}) → {4:0.00}   performance {5:0.00}", w.generatorLoss, sc.generator, w.kills, w.allyKills, sc.killRate, sc.score));
            lines.Add("DDA OUTPUT (active)");
            lines.Add(string.Format("  zombies x{0:0.00}  spawn interval {1:0.00}s  enemy HP x{2:0.00}  speed x{3:0.00}  drop {4:0%}",
                o.countMultiplier, o.spawnInterval, o.enemyHealth, o.enemySpeed, o.dropChance));
            lines.Add(string.Format("  ally weights: support {0:0.00}  repair {1:0.00}  intercept {2:0.00}", o.supportWeight, o.repairWeight, o.interceptWeight));
            if (s.CurrentEvent != null)
                lines.Add("PCG EVENT W" + s.CurrentEvent.wave + ": " + s.CurrentEvent.Title + "  budget " + s.CurrentEvent.budget.ToString("0.0") + "  attempts " + s.CurrentEvent.attempts + "  rejected " + s.CurrentEvent.RejectionText);
            lines.Add("Tactical points " + s.Tactical.points.Count + "  (candidates " + s.Tactical.candidates + ", valid " + s.Tactical.valid + ")  rejected: " + s.Tactical.RejectionText);
            foreach (var a in AllyBrain.All)
            {
                lines.Add("UTILITY " + a.Callsign + ":  " + string.Join("  ", Enumerable.Range(0, a.Scores.Length)
                    .Select(i => (i == (int)a.Action ? ">" : "") + AllyBrain.ActionNames[i] + " " + a.Scores[i].ToString("0.00")).ToArray()));
                lines.Add("  target " + (a.Target != null ? a.Target.name.Replace("(Clone)", "") : "-") + "  ammo " + (a.Reloading ? "reload" : a.Ammo.ToString()) + "  repaired " + a.Repaired.ToString("0"));
            }
            lines.Add("HISTORY");
            foreach (var h in s.History) lines.Add("  " + h);
            lines.Add("LOG");
            foreach (var m in s.Messages.Take(6)) lines.Add("  " + m);
            lines.Add("Green = tactical point (cyan = occupied), red = rejected candidate");
            lines.Add("F10 hide | F5 DDA on/off | F6/F7 difficulty +/- 0.2 | F9 experiments");
            Panel(lines, 640f);
        }

        void AllyTags(Camera cam)
        {
            foreach (var a in AllyBrain.All)
            {
                Vector3 p = a.transform.position + Vector3.up * 2.15f;
                var sp = cam.WorldToScreenPoint(p);
                if (sp.z <= 0f || sp.z > 70f) continue;
                float y = Screen.height - sp.y;
                tagStyle.normal.textColor = a.Action == AllyAction.Down ? new Color(1f, .35f, .3f) : new Color(.45f, .85f, 1f);
                GUI.Label(new Rect(sp.x - 100f, y - 22f, 200f, 20f), a.Callsign + " · " + AllyBrain.ActionNames[(int)a.Action], tagStyle);
                float f = Mathf.Clamp01(a.Health / a.MaxHealth);
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, .6f); GUI.DrawTexture(new Rect(sp.x - 30f, y, 60f, 5f), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, .3f, .2f), new Color(.4f, .9f, 1f), f); GUI.DrawTexture(new Rect(sp.x - 30f, y, 60f * f, 5f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        void TacticalMarkers(Camera cam, FinalSession s)
        {
            var prev = GUI.color;
            foreach (var r in s.Tactical.rejectedPositions) Dot(cam, r, new Color(1f, .25f, .2f, .6f), 4f);
            foreach (var p in s.Tactical.points)
            {
                Dot(cam, p.position, p.owner != null ? new Color(.3f, .9f, 1f, .95f) : new Color(.3f, 1f, .35f, .9f), 9f);
                var sp = cam.WorldToScreenPoint(p.position + Vector3.up * 0.4f);
                if (sp.z > 0f && sp.z < 60f)
                {
                    label.normal.textColor = Color.white;
                    GUI.Label(new Rect(sp.x + 6f, Screen.height - sp.y - 8f, 160f, 16f), p.score.ToString("0.00") + (p.seesGenerator ? " G" : "") + " " + p.bestLane, label);
                }
            }
            GUI.color = prev;
        }

        static void Dot(Camera cam, Vector3 world, Color color, float size)
        {
            var sp = cam.WorldToScreenPoint(world + Vector3.up * 0.2f);
            if (sp.z <= 0f || sp.z > 80f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(sp.x - size * .5f, Screen.height - sp.y - size * .5f, size, size), Texture2D.whiteTexture);
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
