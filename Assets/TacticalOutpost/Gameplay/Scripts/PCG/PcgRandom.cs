using UnityEngine;

namespace Outpost.PCG
{
    /// <summary>
    /// Pseudo-random generator ber-seed milik PCG sendiri (bukan UnityEngine.Random global).
    /// Alasannya: UnityEngine.Random juga dipakai spread senjata, audio, dan AI. Kalau PCG memakai stream global,
    /// setiap tembakan player akan menggeser hasil generation sehingga seed yang sama tidak lagi reproducible.
    /// Setiap subsistem (wave, cover, supply, dressing) mendapat stream turunan dari master seed.
    /// </summary>
    public sealed class PcgRandom
    {
        readonly System.Random rng;
        public int Seed { get; private set; }

        public PcgRandom(int seed) { Seed = seed; rng = new System.Random(seed); }

        /// <summary>Seed turunan yang stabil: master seed + nama subsistem + index (mis. nomor wave).</summary>
        public static int Derive(int masterSeed, string stream, int index = 0)
        {
            unchecked
            {
                int h = (int)2166136261;
                h = (h ^ masterSeed) * 16777619;
                foreach (char c in stream) h = (h ^ c) * 16777619;
                h = (h ^ index) * 16777619;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }

        public static PcgRandom For(int masterSeed, string stream, int index = 0) { return new PcgRandom(Derive(masterSeed, stream, index)); }

        /// <summary>Float pada [0, 1).</summary>
        public float Value { get { return (float)rng.NextDouble(); } }

        /// <summary>Sama seperti Random.Range(float, float).</summary>
        public float Range(float min, float max) { return min + (max - min) * Value; }

        /// <summary>Sama seperti Random.Range(int, int): max eksklusif.</summary>
        public int Range(int min, int max) { return max <= min ? min : rng.Next(min, max); }

        public Vector2 InsideUnitCircle()
        {
            float a = Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int k = rng.Next(0, i + 1);
                T t = list[i]; list[i] = list[k]; list[k] = t;
            }
        }

        /// <summary>Weighted random: index dipilih sebanding bobotnya. Bobot ≤ 0 tidak pernah terpilih. -1 bila semua bobot 0.</summary>
        public int WeightedIndex(System.Collections.Generic.IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0f) total += weights[i];
            if (total <= 0f) return -1;
            float r = Value * total, cumulative = 0f;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                cumulative += weights[i]; last = i;
                if (r < cumulative) return i;
            }
            return last;
        }
    }
}
