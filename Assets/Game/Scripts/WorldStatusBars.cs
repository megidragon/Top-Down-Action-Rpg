using UnityEngine;

namespace TinyRpg
{
    /// Barras flotantes sobre la cabeza de cada personaje: salud, energia y
    /// —solo en quien gasta mana (mago y monje)— una tercera azul debajo.
    /// El relleno se escala desde el borde izquierdo mediante un transform ancla.
    public class WorldStatusBars : MonoBehaviour
    {
        public CharacterStats stats;
        public Transform healthFillAnchor;
        public Transform energyFillAnchor;
        public Transform manaFillAnchor;
        public GameObject manaBar; // marco entero: se oculta si no usa mana

        void Awake()
        {
            if (stats == null) stats = GetComponentInParent<CharacterStats>();
            // La barra azul solo existe para quien tiene mana.
            if (manaBar != null) manaBar.SetActive(stats != null && stats.usesMana);
        }

        void OnEnable()
        {
            if (stats == null) return;
            stats.HealthChanged += OnHealthChanged;
            stats.EnergyChanged += OnEnergyChanged;
            stats.ManaChanged += OnManaChanged;
            stats.Died += OnDied;
        }

        void OnDisable()
        {
            if (stats == null) return;
            stats.HealthChanged -= OnHealthChanged;
            stats.EnergyChanged -= OnEnergyChanged;
            stats.ManaChanged -= OnManaChanged;
            stats.Died -= OnDied;
        }

        void OnManaChanged(float current, float max)
        {
            SetFill(manaFillAnchor, max > 0f ? current / max : 0f);
        }

        void OnHealthChanged(float current, float max)
        {
            SetFill(healthFillAnchor, max > 0f ? current / max : 0f);
        }

        void OnEnergyChanged(float current, float max)
        {
            SetFill(energyFillAnchor, max > 0f ? current / max : 0f);
        }

        static void SetFill(Transform anchor, float fraction)
        {
            if (anchor == null) return;
            var s = anchor.localScale;
            s.x = Mathf.Clamp01(fraction);
            anchor.localScale = s;
        }

        void OnDied()
        {
            gameObject.SetActive(false);
        }
    }
}
