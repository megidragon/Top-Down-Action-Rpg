using System;
using UnityEngine;

namespace TinyRpg
{
    /// Salud y energia de un personaje (jugador o NPC).
    /// La energia se regenera de 0 a 100 en 4 segundos (25/s).
    public class CharacterStats : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float maxEnergy = 50f;             // energia reducida a la mitad
        public float energyRegenPerSecond = 12.5f; // y regeneracion a media velocidad
        public int team; // 0 = jugador, 1 = enemigos

        /// Solo el mago y el monje gastan mana (su habilidad de Espacio).
        public bool usesMana;

        public float Health { get; private set; }
        public float Energy { get; private set; }
        public float Mana { get; private set; }
        public bool IsDead { get; private set; }

        /// El deposito de mana crece a la par que la energia maxima, asi que
        /// los elixires de energia tambien alargan la barra azul.
        public float MaxMana => usesMana ? maxEnergy : 0f;

        public event Action<float, float> HealthChanged; // (actual, max)
        public event Action<float, float> EnergyChanged; // (actual, max)
        public event Action<float, float> ManaChanged;   // (actual, max)
        public event Action<Vector2> Damaged;            // direccion del golpe recibido
        public event Action Died;

        void Awake()
        {
            Health = maxHealth;
            Energy = maxEnergy;
            Mana = MaxMana;
        }

        void Start()
        {
            HealthChanged?.Invoke(Health, maxHealth);
            EnergyChanged?.Invoke(Energy, maxEnergy);
            ManaChanged?.Invoke(Mana, MaxMana);
        }

        /// El mana NO se regenera con el tiempo: solo se recupera descansando
        /// en la fogata. Es un recurso de expedicion, no de combate.
        public bool TrySpendMana(float amount)
        {
            if (IsDead || !usesMana || Mana < amount) return false;
            Mana -= amount;
            ManaChanged?.Invoke(Mana, MaxMana);
            return true;
        }

        public void RestoreMana()
        {
            if (!usesMana) return;
            Mana = MaxMana;
            ManaChanged?.Invoke(Mana, MaxMana);
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

        /// Tope al que puede llegar la barra de energia con elixires.
        public const float MaxEnergyCap = 200f;

        /// Elixir de energia: sube el maximo y regala esos puntos en el acto,
        /// para que beberlo se note en vez de encoger la barra visualmente.
        /// Devuelve false si ya estaba al tope.
        public bool AddMaxEnergy(float points)
        {
            if (IsDead || points <= 0f) return false;
            if (maxEnergy >= MaxEnergyCap) return false;

            float before = maxEnergy;
            maxEnergy = Mathf.Min(MaxEnergyCap, maxEnergy + points);
            float gained = maxEnergy - before;
            Energy = Mathf.Min(maxEnergy, Energy + gained);
            EnergyChanged?.Invoke(Energy, maxEnergy);

            // El deposito de mana crece con la energia maxima y se regalan
            // tambien esos puntos (si no, la barra azul se veria mas vacia).
            if (usesMana)
            {
                Mana = Mathf.Min(MaxMana, Mana + gained);
                ManaChanged?.Invoke(Mana, MaxMana);
            }
            return true;
        }

        /// Rellena la barra de energia de golpe (escena de pruebas).
        public void RefillEnergy()
        {
            if (IsDead) return;
            Energy = maxEnergy;
            EnergyChanged?.Invoke(Energy, maxEnergy);
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (IsDead) return;
            // La defensa reduce el dano recibido (2% por punto).
            var attrs = GetComponent<CharacterAttributes>();
            if (attrs != null) amount *= attrs.DamageTakenMultiplier;
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
