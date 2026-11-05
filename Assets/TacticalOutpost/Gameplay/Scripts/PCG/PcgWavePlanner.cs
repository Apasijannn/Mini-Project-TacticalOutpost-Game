using System.Collections.Generic;
using System.Linq;

namespace Outpost.PCG
{
    /// <summary>Komposisi satu wave hasil PCG.</summary>
    public sealed class PcgWavePlan
    {
        public int wave, budget, spent;
        public readonly List<string> queue = new List<string>();          // urutan spawn zombie biasa
        public readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        public int draws, rejectedOverBudget;

        public string Summary()
        {
            return string.Join("  ", counts.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)) +
                   "  (" + queue.Count + " zombie, cost " + spent + "/" + budget + ")";
        }
    }

    /// <summary>
    /// Enemy selection mode PCG: weighted random dari PcgProfile.enemyTable + difficulty budget per wave.
    /// Generator memilih varian (weighted) berulang kali; varian yang cost-nya melebihi sisa budget tidak boleh dipilih.
    /// Selesai ketika tidak ada varian yang muat. Memakai stream PcgRandom "enemy" → seed sama = komposisi sama.
    /// </summary>
    public static class PcgWavePlanner
    {
        public static PcgWavePlan Plan(PcgProfile p, int seed, int wave, ICollection<string> availableIds)
        {
            var plan = new PcgWavePlan { wave = wave };
            if (p.waveBudget == null || p.waveBudget.Length == 0 || p.enemyTable == null) return plan;
            plan.budget = p.waveBudget[System.Math.Min(wave - 1, p.waveBudget.Length - 1)];
            var rng = PcgRandom.For(seed, "enemy", wave);
            int remaining = plan.budget;
            var lastCost = new List<int>();
            for (int guard = 0; guard < 500; guard++)
            {
                var eligible = p.enemyTable.Where(e => e != null && e.weight > 0f && wave >= e.minWave &&
                                                       (availableIds == null || availableIds.Contains(e.id))).ToList();
                var fits = eligible.Where(e => e.cost <= remaining).ToList();
                plan.rejectedOverBudget += eligible.Count - fits.Count;
                if (fits.Count == 0) break;
                var pick = fits[System.Math.Max(0, rng.WeightedIndex(fits.Select(e => e.weight).ToList()))];
                plan.draws++;
                plan.queue.Add(pick.id);
                lastCost.Add(pick.cost);
                remaining -= pick.cost;
            }
            // WaveDirector men-spawn perSide x 2 zombie → jumlah dibuat genap (zombie terakhir dikembalikan ke budget).
            if (plan.queue.Count % 2 == 1)
            {
                remaining += lastCost[lastCost.Count - 1];
                plan.queue.RemoveAt(plan.queue.Count - 1);
            }
            plan.spent = plan.budget - remaining;
            foreach (var id in plan.queue) { plan.counts.TryGetValue(id, out int n); plan.counts[id] = n + 1; }
            return plan;
        }
    }
}
