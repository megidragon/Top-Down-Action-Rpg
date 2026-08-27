using System;
using UnityEngine;

namespace TinyRpg
{
    /// Estadisticas del personaje para la run roguelike:
    ///  - Fuerza: 5 puntos = dano actual de la clase (multiplicador fuerza/5).
    ///  - Defensa: cada punto reduce el dano recibido un 2% (5 puntos = 10%).
    ///  - Velocidad: 5 puntos = velocidad base; cada punto extra +2% de movimiento.
    /// Los elixires de los mercaderes suben un punto permanente (durante la run).
    public class CharacterAttributes : MonoBehaviour
    {
        public int strength = 5;
        public int defense = 5;
        public int speed = 5;

        public event Action Changed;

        public float DamageMultiplier => strength / 5f;
        public float DamageTakenMultiplier => Mathf.Clamp(1f - defense * 0.02f, 0.1f, 1f);
        public float SpeedMultiplier => 1f + (speed - 5) * 0.02f;

        public void AddStrength(int points) { strength += points; Changed?.Invoke(); }
        public void AddDefense(int points) { defense += points; Changed?.Invoke(); }
        public void AddSpeed(int points) { speed += points; Changed?.Invoke(); }

        public void ResetToBase()
        {
            strength = 5;
            defense = 5;
            speed = 5;
            Changed?.Invoke();
        }

        /// Multiplicador de dano del atacante (1 si no tiene atributos).
        public static float DamageOf(Component attacker)
        {
            if (attacker == null) return 1f;
            var attrs = attacker.GetComponent<CharacterAttributes>();
            return attrs != null ? attrs.DamageMultiplier : 1f;
        }
    }
}
