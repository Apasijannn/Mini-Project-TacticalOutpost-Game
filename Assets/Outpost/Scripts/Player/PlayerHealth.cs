using System;
using UnityEngine;

// Script player first person untuk Tactical Outpost.
// Namespace Outpost.Player supaya tidak bentrok dengan script tim lain.
namespace Outpost.Player
{
    /// <summary>Implementasikan di musuh/objek yang bisa terkena tembakan player.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
    }

    /// <summary>Darah player. Musuh cukup memanggil TakeDamage(...) (atau SendMessage "TakeDamage" tidak didukung).</summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        public float Health { get; private set; }
        public bool IsDead { get { return Health <= 0f; } }

        public event Action<float, float> OnHealthChanged;   // (sekarang, maks)
        public event Action<Vector3> OnDamaged;               // arah datangnya serangan (world), bila diketahui
        public event Action OnDied;

        public static PlayerHealth Instance { get; private set; }

        void Awake() { Instance = this; Health = maxHealth; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (OnHealthChanged != null) OnHealthChanged(Health, maxHealth);
            if (OnDamaged != null) OnDamaged(hitDirection);
            if (Health <= 0f && OnDied != null) OnDied();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            if (OnHealthChanged != null) OnHealthChanged(Health, maxHealth);
        }

        public void ResetHealth()
        {
            Health = maxHealth;
            if (OnHealthChanged != null) OnHealthChanged(Health, maxHealth);
        }
    }
}
