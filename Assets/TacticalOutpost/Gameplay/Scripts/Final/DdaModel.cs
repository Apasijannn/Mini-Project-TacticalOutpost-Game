using UnityEngine;

namespace Outpost.Final
{
    /// <summary>Data performa player dalam satu jendela evaluasi (satu wave).</summary>
    public struct PerformanceWindow
    {
        public int shots, hits, kills, allyKills;
        public float damageTaken, generatorLoss, seconds, playerHealthFraction;
        public float Accuracy { get { return shots > 0 ? (float)hits / shots : 0f; } }
        public float Minutes { get { return Mathf.Max(seconds, 1f) / 60f; } }
    }

    /// <summary>Nilai parameter gameplay hasil DDA untuk satu difficulty.</summary>
    public struct DdaOutput
    {
        public float difficulty, countMultiplier, spawnInterval, enemyHealth, enemySpeed, dropChance;
        public float supportWeight, repairWeight, interceptWeight;
        public float blackoutFlickerScale, blackoutOffScale, blackoutThresholdShift;
    }

    /// <summary>
    /// Model DDA murni (tanpa Unity scene) supaya bisa diuji di eksperimen.
    /// performance (0..1) → skill (EMA) → difficulty (dibatasi maxStep) → output parameter (lerp easy..hard).
    /// </summary>
    public static class DdaModel
    {
        public struct Components { public float accuracy, damage, generator, killRate, score; }

        public static Components Score(FinalProfile p, PerformanceWindow w)
        {
            var c = new Components();
            c.accuracy = w.shots < p.minShotsForAccuracy ? 0.5f : Mathf.InverseLerp(p.accuracyLow, p.accuracyHigh, w.Accuracy);
            c.damage = 1f - Mathf.Clamp01(w.damageTaken / w.Minutes / p.damagePerMinuteHigh);
            c.generator = 1f - Mathf.Clamp01(w.generatorLoss / w.Minutes / p.generatorLossPerMinuteHigh);
            c.killRate = Mathf.Clamp01(w.kills / w.Minutes / p.killsPerMinuteHigh);
            float total = p.accuracyWeight + p.damageTakenWeight + p.generatorLossWeight + p.killRateWeight;
            c.score = total <= 0f ? 0.5f : (c.accuracy * p.accuracyWeight + c.damage * p.damageTakenWeight
                + c.generator * p.generatorLossWeight + c.killRate * p.killRateWeight) / total;
            return c;
        }

        /// <summary>Skill = EMA performa; difficulty mengejar skill tapi maksimal maxStep per evaluasi.</summary>
        public static void Step(FinalProfile p, float performance, ref float skill, ref float difficulty)
        {
            skill = Mathf.Lerp(skill, performance, p.smoothing);
            difficulty = Mathf.Clamp01(difficulty + Mathf.Clamp(skill - difficulty, -p.maxStep, p.maxStep));
        }

        public static DdaOutput Output(FinalProfile p, float d)
        {
            float shift = (d - 0.5f) * 2f * p.ddaAllyShift;       // -shift (easy) .. +shift (hard)
            return new DdaOutput
            {
                difficulty = d,
                countMultiplier = Mathf.Lerp(p.zombieCountMultiplier.x, p.zombieCountMultiplier.y, d),
                spawnInterval = Mathf.Lerp(p.spawnInterval.x, p.spawnInterval.y, d),
                enemyHealth = Mathf.Lerp(p.enemyHealthMultiplier.x, p.enemyHealthMultiplier.y, d),
                enemySpeed = Mathf.Lerp(p.enemySpeedMultiplier.x, p.enemySpeedMultiplier.y, d),
                dropChance = Mathf.Lerp(p.dropChance.x, p.dropChance.y, d),
                supportWeight = Mathf.Max(0f, p.supportWeight - shift),
                repairWeight = Mathf.Max(0f, p.repairWeight - shift),
                interceptWeight = Mathf.Max(0f, p.interceptWeight + shift),
                blackoutFlickerScale = Mathf.Lerp(p.blackoutFlickerMultiplier.x, p.blackoutFlickerMultiplier.y, d),
                blackoutOffScale = Mathf.Lerp(p.blackoutOffDurationMultiplier.x, p.blackoutOffDurationMultiplier.y, d),
                blackoutThresholdShift = Mathf.Lerp(p.blackoutThresholdShift.x, p.blackoutThresholdShift.y, d),
            };
        }

        public static float VariantWeight(FinalProfile p, string id, float d, float fallback)
        {
            if (p.variantWeights != null)
                foreach (var v in p.variantWeights) if (v.id == id) return Mathf.Lerp(v.weight.x, v.weight.y, d);
            return fallback;
        }

        public static string Label(float d)
        {
            return d < 0.2f ? "RECRUIT" : d < 0.4f ? "EASY" : d < 0.6f ? "NORMAL" : d < 0.8f ? "HARD" : "VETERAN";
        }
    }
}
