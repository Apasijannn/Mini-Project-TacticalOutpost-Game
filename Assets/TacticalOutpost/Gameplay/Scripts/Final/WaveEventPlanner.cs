using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Outpost.Final
{
    public enum WaveEvent { ClearNight, SupplyDrop, Fog, Blackout, Frenzy, Horde }

    /// <summary>Hasil generate event untuk satu wave.</summary>
    public sealed class WaveEventPlan
    {
        public int wave;
        public float difficulty, budget;
        public WaveEvent primary = WaveEvent.ClearNight;
        public bool assistSupply;                 // Supply Drop tambahan karena difficulty rendah
        public int attempts;
        public readonly Dictionary<string, int> rejected = new Dictionary<string, int>();

        public bool Has(WaveEvent e) { return primary == e || e == WaveEvent.SupplyDrop && assistSupply; }
        public string Title { get { return WaveEventPlanner.Info(primary).title + (assistSupply && primary != WaveEvent.SupplyDrop ? " + SUPPLY DROP" : ""); } }
        public string RejectionText
        {
            get
            {
                if (rejected.Count == 0) return "-";
                var sb = new StringBuilder();
                foreach (var kv in rejected) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// PCG event wave: weighted random (bobot bergeser oleh difficulty DDA) + budget + generate-and-test.
    /// Seed sama + difficulty sama = event sama (System.Random per wave, bukan UnityEngine.Random).
    /// Aturan: MinWave, Budget, NoRepeat, NoHeavyStreak, FinalNoHorde.
    /// </summary>
    public static class WaveEventPlanner
    {
        public struct EventInfo
        {
            public WaveEvent id;
            public string title, subtitle;
            public float cost;
            public Vector2 weight;               // easy → hard
            public int minWave;
            public bool Heavy { get { return cost >= 2f; } }
        }

        static readonly EventInfo[] table =
        {
            new EventInfo { id = WaveEvent.ClearNight, title = "CLEAR NIGHT", subtitle = "No anomalies reported.", cost = 0f, weight = new Vector2(4f, 1f), minWave = 1 },
            new EventInfo { id = WaveEvent.SupplyDrop, title = "SUPPLY DROP", subtitle = "Command dropped ammo and medkits near the generator.", cost = -0.5f, weight = new Vector2(3f, 0.5f), minWave = 1 },
            new EventInfo { id = WaveEvent.Fog, title = "DENSE FOG", subtitle = "Visibility reduced. Listen for movement.", cost = 1f, weight = new Vector2(2f, 2f), minWave = 1 },
            new EventInfo { id = WaveEvent.Blackout, title = "BLACKOUT", subtitle = "Grid failure. Outpost lights are down.", cost = 1.5f, weight = new Vector2(1f, 2.5f), minWave = 2 },
            new EventInfo { id = WaveEvent.Frenzy, title = "FRENZY", subtitle = "The infected are faster and more aggressive.", cost = 2f, weight = new Vector2(0.5f, 2.5f), minWave = 2 },
            new EventInfo { id = WaveEvent.Horde, title = "HORDE RUSH", subtitle = "A larger horde is moving in fast.", cost = 2.5f, weight = new Vector2(0.3f, 2f), minWave = 2 },
        };

        public static IReadOnlyList<EventInfo> Table { get { return table; } }
        public static EventInfo Info(WaveEvent e) { foreach (var t in table) if (t.id == e) return t; return table[0]; }

        public static float Budget(FinalProfile p, int wave, float difficulty)
        {
            return p.eventBudgetBase + p.eventBudgetPerWave * (wave - 1) + p.eventBudgetByDifficulty * difficulty;
        }

        public static WaveEventPlan Plan(FinalProfile p, int seed, int wave, int totalWaves, float difficulty, WaveEvent? previous)
        {
            var plan = new WaveEventPlan { wave = wave, difficulty = difficulty, budget = Budget(p, wave, difficulty) };
            var rng = new System.Random(seed ^ 0x5EED ^ (wave * 7919));
            bool previousHeavy = previous.HasValue && Info(previous.Value).Heavy;
            for (int attempt = 0; attempt < p.maxEventAttempts; attempt++)
            {
                plan.attempts = attempt + 1;
                var e = PickWeighted(rng, difficulty);
                string rule = Test(e, plan, wave, totalWaves, previous, previousHeavy);
                if (rule == null) { plan.primary = e.id; break; }
                int n; plan.rejected.TryGetValue(rule, out n); plan.rejected[rule] = n + 1;
                plan.primary = WaveEvent.ClearNight;                           // fallback bila semua percobaan gagal
            }
            plan.assistSupply = difficulty < p.supplyAssistBelow && plan.primary != WaveEvent.SupplyDrop;
            return plan;
        }

        static string Test(EventInfo e, WaveEventPlan plan, int wave, int totalWaves, WaveEvent? previous, bool previousHeavy)
        {
            if (wave < e.minWave) return "MinWave";
            if (e.cost > plan.budget) return "Budget";
            if (previous.HasValue && e.id == previous.Value && e.id != WaveEvent.ClearNight) return "NoRepeat";
            if (previousHeavy && e.Heavy) return "NoHeavyStreak";
            if (wave == totalWaves && e.id == WaveEvent.Horde) return "FinalNoHorde";
            return null;
        }

        static EventInfo PickWeighted(System.Random rng, float d)
        {
            float sum = 0f;
            foreach (var t in table) sum += Mathf.Max(0f, Mathf.Lerp(t.weight.x, t.weight.y, d));
            double r = rng.NextDouble() * sum;
            foreach (var t in table)
            {
                r -= Mathf.Max(0f, Mathf.Lerp(t.weight.x, t.weight.y, d));
                if (r <= 0) return t;
            }
            return table[table.Length - 1];
        }
    }
}
