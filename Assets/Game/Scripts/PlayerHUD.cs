using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// HUD en pantalla del jugador: barra grande de salud y de energia.
    public class PlayerHUD : MonoBehaviour
    {
        public Image healthFill;
        public Image energyFill;

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
            OnHealthChanged(stats.Health, stats.maxHealth);
            OnEnergyChanged(stats.Energy, stats.maxEnergy);
        }

        void OnDestroy()
        {
            if (stats == null) return;
            stats.HealthChanged -= OnHealthChanged;
            stats.EnergyChanged -= OnEnergyChanged;
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
