using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// HUD en pantalla del jugador: barras de salud, energia y —solo para el
    /// mago y el monje— mana.
    public class PlayerHUD : MonoBehaviour
    {
        public Image healthFill;
        public Image energyFill;
        public Image manaFill;
        public GameObject manaBar; // marco completo: se oculta si la clase no usa mana

        CharacterStats stats;

        void Start()
        {
            TryBind();
        }

        void Update()
        {
            if (stats == null) TryBind();
        }

        void TryBind()
        {
            if (GameManager.Player == null) return;
            stats = GameManager.Player.GetComponent<CharacterStats>();
            stats.HealthChanged += OnHealthChanged;
            stats.EnergyChanged += OnEnergyChanged;
            stats.ManaChanged += OnManaChanged;
            OnHealthChanged(stats.Health, stats.maxHealth);
            OnEnergyChanged(stats.Energy, stats.maxEnergy);

            // La barra de mana solo existe para quien la usa.
            if (manaBar != null) manaBar.SetActive(stats.usesMana);
            if (stats.usesMana) OnManaChanged(stats.Mana, stats.MaxMana);
        }

        void OnDestroy()
        {
            if (stats == null) return;
            stats.HealthChanged -= OnHealthChanged;
            stats.EnergyChanged -= OnEnergyChanged;
            stats.ManaChanged -= OnManaChanged;
        }

        void OnManaChanged(float current, float max)
        {
            if (manaFill != null) manaFill.fillAmount = max > 0f ? current / max : 0f;
        }

        void OnHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max > 0f ? current / max : 0f;
        }

        void OnEnergyChanged(float current, float max)
        {
            if (energyFill != null) energyFill.fillAmount = max > 0f ? current / max : 0f;
        }
    }
}
