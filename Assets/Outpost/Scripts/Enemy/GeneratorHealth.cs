using System;
using System.Collections.Generic;
using UnityEngine;
using Outpost.Env;

namespace Outpost.Enemy
{
    /// <summary>
    /// Darah generator (objective yang dilindungi player). Sengaja BUKAN IDamageable supaya tembakan player tidak merusaknya.
    /// Musuh memanggil Damage(). Logika perbaikan/menang-kalah akan dibangun di tahap alur wave.
    /// </summary>
    public class GeneratorHealth : MonoBehaviour
    {
        public static readonly List<GeneratorHealth> All = new List<GeneratorHealth>();

        public float maxHealth = 400f;
        public float Health { get; private set; }
        public bool IsAlive { get { return Health > 0f; } }
        public float Fraction { get { return maxHealth > 0f ? Health / maxHealth : 0f; } }
        public string Id { get { return slot != null ? slot.id : name; } }
        public int attackers;                       // jumlah musuh yang sedang menyerang generator ini (diisi EnemyBrain)
        public Transform[] AttackPoints { get { return slot != null && slot.attackPoints != null ? slot.attackPoints : new Transform[0]; } }
        public Collider Body { get; private set; }

        public event Action<GeneratorHealth, float> OnDamaged;   // (generator, sisa HP)
        public event Action<GeneratorHealth> OnDestroyed;

        EnvGeneratorSlot slot;

        void Awake()
        {
            Health = maxHealth;
            slot = GetComponent<EnvGeneratorSlot>();
            Body = GetComponent<Collider>();
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Damage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (OnDamaged != null) OnDamaged(this, Health);
            if (Health <= 0f && OnDestroyed != null) OnDestroyed(this);
        }

        public void Repair(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            if (OnDamaged != null) OnDamaged(this, Health);
        }

        public void ResetHealth() { Health = maxHealth; }
    }
}
