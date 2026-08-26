using System;
using UnityEngine;

namespace TinyRpg
{
    /// Salud y energia de un personaje (jugador o NPC).
    /// La energia se regenera de 0 a 100 en 4 segundos (25/s).
    public class CharacterStats : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float maxEnergy = 100f;
        public float energyRegenPerSecond = 25f;
        public int team; // 0 = jugador, 1 = enemigos

        public float Health { get; private set; }
        public float Energy { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<float, float> HealthChanged; // (actual, max)
        public event Action<float, float> EnergyChanged; // (actual, max)
        public event Action<Vector2> Damaged;            // direccion del golpe recibido
        public event Action Died;

        void Awake()
        {
            Health = maxHealth;
            Energy = maxEnergy;
        }

        void Start()
        {
            HealthChanged?.Invoke(Health, maxHealth);
            EnergyChanged?.Invoke(Energy, maxEnergy);
        }

        void Update()
        {
            if (IsDead) return;
            if (Energy < maxEnergy)
            {
                Energy = Mathf.Min(maxEnergy, Energy + energyRegenPerSecond * Time.deltaTime);
                EnergyChanged?.Invoke(Energy, maxEnergy);
            }
        }

        public bool TrySpendEnergy(float amount)
        {
            if (IsDead || Energy < amount) return false;
            Energy -= amount;
            EnergyChanged?.Invoke(Energy, maxEnergy);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            HealthChanged?.Invoke(Health, maxHealth);
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (IsDead) return;
            Health = Mathf.Max(0f, Health - amount);
            HealthChanged?.Invoke(Health, maxHealth);
            Damaged?.Invoke(hitDirection);
            if (Health <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }
    }
}
