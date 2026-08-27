using UnityEngine;

namespace TinyRpg
{
    /// Curvas de dificultad de la expedicion infinita.
    ///  - IA por niveles: 1-6 tonta (0), 7-12 media (1), 13+ inteligente (2).
    ///  - Enemigos: +1 cada 3 niveles hasta 5 en el nivel 13-15; despues
    ///    +1 cada 10 niveles (25, 35, ...).
    ///  - Estadisticas: base 3 en niveles 1-3, base 5 despues; a partir del
    ///    nivel 16, +1 punto a una estadistica aleatoria cada 2 niveles.
    ///  - Elixires: 2 monedas antes del nivel 6; despues +1 cada 3 niveles.
    public static class Difficulty
    {
        public const int TreasureReward = 15; // monedas por tesoro (nivel 10, 20...)
        public const int AllyReplacementPrice = 5;

        public static int AiTierFor(int level)
        {
            if (level <= 6) return 0;
            if (level <= 12) return 1;
            return 2;
        }

        public static int EnemyCountFor(int level)
        {
            if (level <= 15) return Mathf.Min(5, 1 + (level - 1) / 3);
            return 5 + (level - 15) / 10; // 6 en el 25, 7 en el 35...
        }

        /// Puntos extra repartidos al azar entre fuerza/defensa/velocidad a
        /// partir del nivel 16 (+1 cada 2 niveles). Cada tramo sortea su punto
        /// con su propia semilla: el reparto es determinista y ACUMULATIVO
        /// (el del nivel N siempre contiene al del N-2, ninguna estadistica
        /// retrocede al profundizar).
        public static (int strength, int defense, int speed) StatBonusFor(int level)
        {
            int s = 0, d = 0, v = 0;
            for (int step = 16; step <= level; step += 2)
            {
                var rng = new System.Random(step * 7919);
                switch (rng.Next(3))
                {
                    case 0: s++; break;
                    case 1: d++; break;
                    default: v++; break;
                }
            }
            return (s, d, v);
        }

        public static int ElixirPriceFor(int level)
        {
            if (level < 6) return 2;
            return 2 + (level - 3) / 3; // 3 en el 6-8, 4 en el 9-11...
        }

        /// Huecos de aliado desbloqueados segun el nivel alcanzado (6/12/18).
        public static int AllySlotsFor(int level)
        {
            if (level >= 18) return 3;
            if (level >= 12) return 2;
            if (level >= 6) return 1;
            return 0;
        }
    }
}
