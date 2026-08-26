using UnityEngine;

namespace TinyRpg
{
    /// Barras de salud y energia flotantes sobre la cabeza de cada personaje.
    /// El relleno se escala desde el borde izquierdo mediante un transform ancla.
    public class WorldStatusBars : MonoBehaviour
    {
        public CharacterStats stats;
        public Transform healthFillAnchor;
        public Transform energyFillAnchor;

        void Awake()
        {
            if (stats == null) stats = GetComponentInParent<CharacterStats>();
        }

        void OnEnable()
        {
            if (stats == null) return;
            stats.HealthChanged += OnHealthChanged;
            stats.EnergyChanged += OnEnergyChanged;
            stats.Died += OnDied;
        }

        void OnDisable()
        {
            if (stats == null) return;
            stats.HealthChanged -= OnHealthChanged;
            stats.EnergyChanged -= OnEnergyChanged;
            stats.Died -= OnDied;
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
