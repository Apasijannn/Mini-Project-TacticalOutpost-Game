using System;
using System.Collections.Generic;
using UnityEngine;
using Outpost.Player;

namespace Outpost.Enemy
{
    /// <summary>Darah musuh. Menerima tembakan player (IDamageable), mendeteksi headshot dari jarak ke tulang kepala.</summary>
    [DisallowMultipleComponent]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static readonly List<EnemyHealth> Alive = new List<EnemyHealth>();
        public static event Action<EnemyHealth> AnySpawned;
        public static event Action<EnemyHealth> AnyDied;

        public EnemyConfig config;
        public Transform headBone;

        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Fraction { get { return MaxHealth > 0f ? Health / MaxHealth : 0f; } }
        public bool IsDead { get; private set; }
        /// <summary>Waktu (Time.time) tembakan kepala terakhir mengenai musuh mana pun. Dipakai HUD untuk umpan balik headshot.</summary>
        public static float LastHeadshotTime = -10f;
        public Vector3 LastHitDirection { get; private set; }
        public bool LastHitWasHeadshot { get; private set; }

        public event Action<float, Vector3> OnDamaged;   // (damage, arah peluru)
        public event Action<EnemyHealth> OnDied;

        void Awake()
        {
            MaxHealth = config != null ? config.maxHealth : 100f;
            Health = MaxHealth;
            LastHitDirection = transform.forward;
        }

        void OnEnable()
        {
            if (!IsDead && !Alive.Contains(this)) { Alive.Add(this); if (AnySpawned != null) AnySpawned(this); }
        }

        void OnDisable() { Alive.Remove(this); }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (IsDead || amount <= 0f) return;
            bool head = IsHeadHit(hitPoint);
            LastHitWasHeadshot = head;
            if (head && config != null) amount *= config.headshotMultiplier;
            if (head) LastHeadshotTime = Time.time;
            LastHitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : transform.forward;
            Health = Mathf.Max(0f, Health - amount);
            if (OnDamaged != null) OnDamaged(amount, LastHitDirection);
            if (Health <= 0f) Die();
        }

        /// <summary>Area kepala = bola di sekitar kepala (tulang kepala digeser ke atas, mencakup leher atas hingga puncak kepala).</summary>
        public bool IsHeadHit(Vector3 hitPoint)
        {
            if (headBone == null) return false;
            float sc = (config != null ? config.visualScale : 1f) * transform.localScale.y;   // ikut skala boss dari WaveDirector
            Vector3 c = headBone.position + Vector3.up * (0.10f * sc);
            float r = 0.27f * sc;
            return (hitPoint - c).sqrMagnitude <= r * r;
        }

        public void Kill()
        {
            if (IsDead) return;
            Health = 0f;
            Die();
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Alive.Remove(this);
            if (OnDied != null) OnDied(this);
            if (AnyDied != null) AnyDied(this);
        }
    }
}
