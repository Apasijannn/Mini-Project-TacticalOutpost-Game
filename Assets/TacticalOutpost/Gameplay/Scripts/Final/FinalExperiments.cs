using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Outpost.Final
{
    /// <summary>
    /// Eksperimen otomatis mode Final (F9) → Docs/Final/ExperimentResults.md.
    /// DDA dan event planner diuji sebagai model murni dengan profil player sintetis; titik taktis diuji di map yang sedang dimuat.
    /// </summary>
    public static class FinalExperiments
    {
        struct Persona { public string name; public PerformanceWindow window; }

        static PerformanceWindow W(int shots, float acc, float dmg, float gen, int kills)
        {
            return new PerformanceWindow { shots = shots, hits = Mathf.RoundToInt(shots * acc), damageTaken = dmg, generatorLoss = gen, kills = kills, seconds = 60f };
        }

        static readonly Persona[] Personas =
        {
            new Persona { name = "Weak",    window = W(120, 0.15f, 80f, 220f, 6) },
            new Persona { name = "Average", window = W(150, 0.35f, 35f, 80f, 12) },
            new Persona { name = "Strong",  window = W(160, 0.60f, 8f, 15f, 22) },
        };

        public static string Run(FinalSession s)
        {
            var p = s.Profile;
            var sb = new StringBuilder();
            sb.AppendLine("# Final Project Experiment Results — Tactical Outpost (MODE 04)");
            sb.AppendLine();
            sb.AppendLine("Dibuat otomatis (F9) " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". Seed sesi " + s.Seed + ". Profil player sintetis per jendela 60 detik.");
            sb.AppendLine();

            // ---- 1. respon DDA per persona
            sb.AppendLine("## Eksperimen 1 — Respon DDA terhadap tipe player (start difficulty " + p.startDifficulty.ToString("0.00") + ")");
            sb.AppendLine();
            sb.AppendLine("| Persona | Akurasi | Damage/menit | Generator hilang/menit | Kill/menit | Performance | D eval 1 | D eval 2 | D eval 3 | D eval 4 | Label akhir |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (var persona in Personas)
            {
                var c = DdaModel.Score(p, persona.window);
                float skill = p.startDifficulty, d = p.startDifficulty;
                var ds = new List<float>();
                for (int i = 0; i < 4; i++) { DdaModel.Step(p, c.score, ref skill, ref d); ds.Add(d); }
                sb.AppendLine(string.Format("| {0} | {1:0%} | {2:0} | {3:0} | {4} | {5:0.00} | {6} | {7} |", persona.name, persona.window.Accuracy, persona.window.damageTaken,
                    persona.window.generatorLoss, persona.window.kills, c.score, string.Join(" | ", ds.Select(x => x.ToString("0.00")).ToArray()), DdaModel.Label(d)));
            }
            sb.AppendLine();

            // ---- 2. output gameplay per difficulty
            sb.AppendLine("## Eksperimen 2 — Parameter gameplay hasil DDA");
            sb.AppendLine();
            sb.AppendLine("| Difficulty | Label | Zombie wave 1/2/3 | Spawn interval | HP zombie | Speed zombie | Drop chance | Walker : Runner : Skinless : Bloater | Ally support / repair / intercept | Blackout flicker / off / shift |");
            sb.AppendLine("|---:|---|---|---:|---:|---:|---:|---|---|---|");
            int[] baseCounts = { 24, 36, 48 };
            foreach (float d in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var o = DdaModel.Output(p, d);
                var weights = new[] { "walker", "runner", "skinless", "bloater" }.Select(id => DdaModel.VariantWeight(p, id, d, 1f)).ToArray();
                float total = weights.Sum();
                sb.AppendLine(string.Format("| {0:0.00} | {1} | {2} | {3:0.00} s | ×{4:0.00} | ×{5:0.00} | {6:0%} | {7} | {8:0.00} / {9:0.00} / {10:0.00} | ×{11:0.00} / ×{12:0.00} / {13:+0%;-0%;0%} |", d, DdaModel.Label(d),
                    string.Join(" / ", baseCounts.Select(b => (Mathf.Max(2, Mathf.RoundToInt(b / 2 * o.countMultiplier)) * 2).ToString()).ToArray()),
                    o.spawnInterval, o.enemyHealth, o.enemySpeed, o.dropChance, string.Join(" : ", weights.Select(x => (x / total).ToString("0%")).ToArray()),
                    o.supportWeight, o.repairWeight, o.interceptWeight, o.blackoutFlickerScale, o.blackoutOffScale, o.blackoutThresholdShift));
            }
            sb.AppendLine();

            // ---- 3. perubahan performa mendadak (smoothing)
            sb.AppendLine("## Eksperimen 3 — Player berubah mendadak (Strong 3× lalu Weak 3×), pengaruh smoothing");
            sb.AppendLine();
            sb.AppendLine("| Smoothing | E1 | E2 | E3 | E4 | E5 | E6 |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---|");
            foreach (float sm in new[] { 0.3f, p.smoothing, 1f }.Distinct())
            {
                var copy = Object.Instantiate(p); copy.smoothing = sm;
                float skill = copy.startDifficulty, d = copy.startDifficulty;
                var ds = new List<string>();
                for (int i = 0; i < 6; i++)
                {
                    var c = DdaModel.Score(copy, (i < 3 ? Personas[2] : Personas[0]).window);
                    DdaModel.Step(copy, c.score, ref skill, ref d); ds.Add(d.ToString("0.00"));
                }
                Object.Destroy(copy);
                sb.AppendLine("| " + sm.ToString("0.00") + " | " + string.Join(" | ", ds.ToArray()) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("maxStep " + p.maxStep.ToString("0.00") + " membatasi lompatan difficulty per wave, jadi perubahan terasa bertahap.");
            sb.AppendLine();

            // ---- 4. distribusi event per difficulty
            sb.AppendLine("## Eksperimen 4 — Distribusi event PCG wave 2 (1000 seed, previous = Clear Night)");
            sb.AppendLine();
            var events = System.Enum.GetValues(typeof(WaveEvent)).Cast<WaveEvent>().ToArray();
            sb.AppendLine("| Difficulty | Budget | " + string.Join(" | ", events.Select(e => WaveEventPlanner.Info(e).title).ToArray()) + " | +Supply assist | Rejected |");
            sb.AppendLine("|---:|---:|" + string.Concat(Enumerable.Repeat("---:|", events.Length)) + "---:|---|");
            foreach (float d in new[] { 0.2f, 0.5f, 0.8f })
            {
                var count = events.ToDictionary(e => e, e => 0);
                var rejected = new Dictionary<string, int>();
                int assist = 0; float budget = 0f;
                for (int seed = 1; seed <= 1000; seed++)
                {
                    var plan = WaveEventPlanner.Plan(p, seed, 2, 3, d, WaveEvent.ClearNight);
                    count[plan.primary]++; if (plan.assistSupply) assist++; budget = plan.budget;
                    foreach (var kv in plan.rejected) { int n; rejected.TryGetValue(kv.Key, out n); rejected[kv.Key] = n + kv.Value; }
                }
                sb.AppendLine("| " + d.ToString("0.0") + " | " + budget.ToString("0.0") + " | " + string.Join(" | ", events.Select(e => (count[e] / 10f).ToString("0.0") + "%").ToArray())
                    + " | " + (assist / 10f).ToString("0.0") + "% | " + string.Join(", ", rejected.Select(kv => kv.Key + " " + kv.Value).ToArray()) + " |");
            }
            sb.AppendLine();

            // ---- 5. reproducibility
            sb.AppendLine("## Eksperimen 5 — Reproducibility event (seed 500, difficulty 0.5 → 0.6 → 0.7)");
            sb.AppendLine();
            sb.AppendLine("| Run | Wave 1 | Wave 2 | Wave 3 |");
            sb.AppendLine("|---:|---|---|---|");
            var runs = new List<string>();
            for (int run = 1; run <= 3; run++)
            {
                WaveEvent? prev = null; var titles = new List<string>();
                for (int wv = 1; wv <= 3; wv++)
                {
                    var plan = WaveEventPlanner.Plan(p, 500, wv, 3, 0.4f + 0.1f * wv, prev);
                    prev = plan.primary; titles.Add(plan.Title);
                }
                runs.Add(string.Join("|", titles.ToArray()));
                sb.AppendLine("| " + run + " | " + string.Join(" | ", titles.ToArray()) + " |");
            }
            sb.AppendLine();
            sb.AppendLine(runs.Distinct().Count() == 1 ? "**Identik**: seed + difficulty sama menghasilkan event yang sama." : "**Berbeda** — periksa RNG.");
            sb.AppendLine();

            // ---- 6. titik taktis di map statis
            sb.AppendLine("## Eksperimen 6 — PCG titik taktis sekutu di map statis");
            sb.AppendLine();
            sb.AppendLine("| Seed | Kandidat | Valid | Terpilih | Skor rata-rata | Lihat generator | Rejected |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---|");
            foreach (int seed in new[] { 100, 200, 300, s.Seed })
            {
                var r = TacticalPoints.Generate(p, seed, s.Generator, s.AgentTypeId);
                sb.AppendLine(string.Format("| {0} | {1} | {2} | {3} | {4:0.00} | {5} | {6} |", seed, r.candidates, r.valid, r.points.Count,
                    r.points.Count > 0 ? r.points.Average(t => t.score) : 0f, r.points.Count(t => t.seesGenerator), r.RejectionText));
            }
            sb.AppendLine();

            // ---- 7. sesi berjalan
            sb.AppendLine("## Sesi yang sedang dimainkan");
            sb.AppendLine();
            foreach (var h in s.History) sb.AppendLine("- " + h);
            foreach (var a in AllyBrain.All) sb.AppendLine("- Ally " + a.Callsign + ": kills " + a.Kills + ", generator repaired " + a.Repaired.ToString("0") + " HP, action now " + AllyBrain.ActionNames[(int)a.Action]);
            sb.AppendLine();

            string dir = Application.isEditor ? Path.Combine(Application.dataPath, "../Docs/Final") : Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "ExperimentResults.md"));
            File.WriteAllText(path, sb.ToString());
            s.Log("Experiments saved: " + path);
            return path;
        }
    }
}
