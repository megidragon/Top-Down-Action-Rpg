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

        // ----------------------------------------------------------------
        //  Cerebros de combate por nivel
        // ----------------------------------------------------------------
        //
        // El reparto sale de la liga del coliseo (435 duelos). La dificultad
        // REAL para un jugador no es cuanto gana una IA, sino lo que le cuesta
        // al rival matarla: derrotas Counter 17 < Feinter 42 < Flanker 46 <
        // Spacer 48 < Ambusher 56 < Rusher 75. Y cada una castiga un error
        // distinto: el Counter castiga atacar, el Spacer fallar, el Feinter el
        // parry en falso, el Flanker quedarse quieto.

        /// Cerebros "elite": los que castigan al jugador por su accion basica.
        /// Se limita cuantos pueden salir a la vez en un mismo nivel.
        public static bool IsElite(CombatBrain brain) =>
            brain == CombatBrain.Counter || brain == CombatBrain.Feinter;

        public static int MaxElitesFor(int level) => level >= 20 ? 2 : (level >= 13 ? 1 : 0);

        /// Pesos por tramo. Devuelve pares (cerebro, peso).
        static (CombatBrain brain, int weight)[] BrainBag(int level)
        {
            if (level <= 3)
                return new[] { (CombatBrain.Rusher, 100) };

            if (level <= 6)
                return new[] { (CombatBrain.Rusher, 70), (CombatBrain.Ambusher, 30) };

            if (level <= 9)
                return new[]
                {
                    (CombatBrain.Rusher, 40), (CombatBrain.Ambusher, 25),
                    (CombatBrain.Flanker, 20), (CombatBrain.Spacer, 15),
                };

            if (level <= 12)
                return new[]
                {
                    (CombatBrain.Rusher, 25), (CombatBrain.Ambusher, 20),
                    (CombatBrain.Flanker, 20), (CombatBrain.Spacer, 20),
                    (CombatBrain.Feinter, 15),
                };

            if (level <= 15)
                return new[]
                {
                    (CombatBrain.Rusher, 18), (CombatBrain.Ambusher, 17),
                    (CombatBrain.Flanker, 20), (CombatBrain.Spacer, 20),
                    (CombatBrain.Feinter, 10), (CombatBrain.Counter, 15),
                };

            // 16+: mandan los que castigan, pero los seis siguen apareciendo.
            return new[]
            {
                (CombatBrain.Counter, 25), (CombatBrain.Feinter, 20),
                (CombatBrain.Spacer, 20), (CombatBrain.Flanker, 15),
                (CombatBrain.Ambusher, 12), (CombatBrain.Rusher, 8),
            };
        }

        /// Probabilidad de que un enemigo use un campeon neuroevolucionado (si
        /// su clase tiene uno entrenado). Desde el nivel 4, y creciendo con la
        /// profundidad: los cerebros escritos a mano nunca desaparecen del
        /// todo, que la variedad de rivales es parte del juego.
        public static float NeuralChanceFor(int level)
        {
            if (level < 4) return 0f;
            if (level < 8) return 0.25f;
            if (level < 14) return 0.35f;
            return 0.45f;
        }

        /// Elige un cerebro para un enemigo del nivel dado. 'elitesLeft' es el
        /// cupo de elites que queda en este nivel; si se agota, se reintenta
        /// con la parte no-elite de la bolsa.
        public static CombatBrain PickBrain(int level, ref int elitesLeft, int classIndex)
        {
            var bag = BrainBag(level);
            bool allowElite = elitesLeft > 0;

            int total = 0;
            foreach (var (brain, weight) in bag)
                if (Allowed(brain, classIndex, level, allowElite)) total += weight;

            if (total <= 0) return CombatBrain.Rusher;

            int roll = Random.Range(0, total);
            foreach (var (brain, weight) in bag)
            {
                if (!Allowed(brain, classIndex, level, allowElite)) continue;
                roll -= weight;
                if (roll < 0)
                {
                    if (IsElite(brain)) elitesLeft--;
                    return brain;
                }
            }
            return CombatBrain.Rusher;
        }

        /// Restricciones que salieron de la liga:
        ///  - El monje con cerebros defensivos no mata a nadie (Counter 1V de
        ///    29, Flanker 0V): su empuje lo desengancha de su propio alcance.
        ///    Hasta arreglar eso, solo cerebros agresivos.
        ///  - Mago Counter fue la mejor combinacion del torneo (19V, 3D): se
        ///    reserva para lo hondo del bosque.
        static bool Allowed(CombatBrain brain, int classIndex, int level, bool allowElite)
        {
            if (IsElite(brain) && !allowElite) return false;

            const int Monk = 3, Mage = 4;
            if (classIndex == Monk)
                return brain == CombatBrain.Rusher || brain == CombatBrain.Ambusher;
            if (classIndex == Mage && brain == CombatBrain.Counter && level < 18)
                return false;
            return true;
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
