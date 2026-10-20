using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Enemy
{
    [Serializable]
    public class EnemyVariant
    {
        public string id;
        public GameObject prefab;
        public EnemyKind kind = EnemyKind.Normal;
        public int minWave = 1;
        public int maxWave = 3;
        public float weight = 1f;
    }

    /// <summary>Daftar semua varian musuh + aturan wave berapa saja varian itu boleh muncul. Dipakai WaveDirector.</summary>
    [CreateAssetMenu(menuName = "Outpost/Enemy Catalog", fileName = "EnemyCatalog")]
    public class EnemyCatalog : ScriptableObject
    {
        public List<EnemyVariant> variants = new List<EnemyVariant>();

        public EnemyVariant Pick(EnemyKind kind, int wave)
        {
            float total = 0f;
            foreach (var v in variants) if (Match(v, kind, wave)) total += Mathf.Max(0f, v.weight);
            if (total <= 0f) return null;
            float r = UnityEngine.Random.value * total;
            foreach (var v in variants)
            {
                if (!Match(v, kind, wave)) continue;
                r -= Mathf.Max(0f, v.weight);
                if (r <= 0f) return v;
            }
            return null;
        }

        static bool Match(EnemyVariant v, EnemyKind kind, int wave)
        {
            return v != null && v.prefab != null && v.kind == kind && wave >= v.minWave && wave <= v.maxWave;
        }

        public GameObject PickPrefab(EnemyKind kind, int wave)
        {
            var v = Pick(kind, wave);
            return v != null ? v.prefab : null;
        }
    }
}
