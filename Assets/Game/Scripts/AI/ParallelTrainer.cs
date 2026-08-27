using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg.AI
{
    /// Motor de entrenamiento por lotes: corre MUCHOS duelos a la vez.
    ///
    /// La idea que hace practico todo esto: un duelo consume poquisima CPU,
    /// lo que cuesta es el tiempo de reloj. Subir Time.timeScale deja de
    /// rendir pasado ~10x porque la fisica acumula pasos fijos. En cambio,
    /// pelear 40 duelos SIMULTANEOS en los mismos frames multiplica el
    /// rendimiento casi linealmente. Combinando ambos se pasa de ~3 duelos por
    /// segundo a varios cientos.
    ///
    /// Las arenas se separan lo suficiente para que ningun ataque de area
    /// alcance a la de al lado (el mayor alcance del juego es la lluvia del
    /// arquero, 11 unidades).
    public class ParallelTrainer : MonoBehaviour
    {
        public const float ArenaSpacing = 40f;
        public static readonly Vector2 TrainingOrigin = new Vector2(2000f, 2000f);

        [Header("Lote")]
        /// Desplazamiento de ESTE entrenador sobre la rejilla base, para que
        /// varias poblaciones (una por clase) convivan sin pisarse las arenas.
        public Vector2 blockOffset = Vector2.zero;

        public int arenaCount = 40;
        public float duelTimeout = 18f;   // segundos de JUEGO por duelo
        public float timeScale = 8f;

        /// Cuantas arenas conservan sus graficos. Entrenar a ciegas es lo
        /// rapido, pero hace falta poder MIRAR que esta aprendiendo la red.
        public int visibleArenas = 8;

        [Header("Terreno")]
        /// Coliseo circular cerrado. Entrenar en campo abierto infinito ensena
        /// tacticas que no transfieren (retroceder sin fin, por ejemplo) y que
        /// fracasan en cuanto hay una pared detras.
        public float arenaRadius = 10f;
        public int obstaclesPerArena = 2;

        [Header("Ritmo")]
        /// Puntos de dano EXTRA por golpe mientras se entrena. Acorta los
        /// duelos, que es lo que limita cuantas generaciones caben en una hora.
        public float bonusDamage = 15f;

        [Header("Justicia")]
        /// Retardo entre percibir y actuar de los DOS contendientes. Debe ser el
        /// mismo con el que jugara el enemigo final (tier 2 = 0.18 s): si se
        /// entrena con reflejos instantaneos, la red aprende respuestas que
        /// dependen de informacion que en partida le llegara tarde.
        public float reactionDelay = 0.18f;

        [Header("Fuentes (opcional)")]
        /// Prefabs propios para no depender de la escena que nos aloje. Si se
        /// dejan vacios se usan ClassSelectScreen y MapLibrary (caso del Lab);
        /// la escena de entrenamiento los asigna directamente.
        public GameObject[] bluePrefabs;
        public GameObject[] redPrefabs;
        public Sprite[] obstacleSprites;

        /// Vida de referencia por clase (la del prefab de jugador), para que la
        /// clase valga lo mismo en los dos bandos.
        static readonly float[] ClassHealth = { 150f, 112f, 75f, 125f, 90f };

        static readonly string[] ClassNames = { "Guerrero", "Lancero", "Arquero", "Monje", "Mago" };

        // Rotulo por arena de la tanda en curso ("vs Rusher · Monje"), para que
        // la escena de entrenamiento pueda ensenarlo sobre cada circulo.
        string[] waveLabels = new string[0];

        public string ArenaLabel(int arena)
            => arena >= 0 && arena < waveLabels.Length && waveLabels[arena] != null
               ? waveLabels[arena] : "-";

        public struct Matchup
        {
            public NeuralNet net;         // aspirante (siempre lado azul)
            public int netClass;
            public CombatBrain rivalBrain;
            public int rivalClass;
        }

        public struct Result
        {
            public float damageDealt;
            public float damageTaken;
            public bool won;
            public bool lost;
            public bool timedOut;   // empate por agotar el tiempo, sin muertos
            public float duration;
            public int actions;
        }

        class Slot
        {
            public GameObject blue, red;
            public DuelistAI blueAi, redAi;
            public CharacterStats blueStats, redStats;
            public float elapsed;
            public bool finished;
            public Result result;
        }

        readonly List<Slot> slots = new List<Slot>();
        readonly List<GameObject> terrain = new List<GameObject>();

        /// Muro circular + un par de estorbos en el centro. El muro es un solo
        /// EdgeCollider2D cerrado: mas barato que veinte cajas y bloquea igual
        /// por dentro. Sin Rigidbody2D, Unity lo trata como estatico, que es lo
        /// que esperan tanto el esquive de la IA como el bloqueo de ataques.
        GameObject BuildArenaTerrain(Vector2 center, bool visible, int seed)
        {
            var root = new GameObject("Arena");
            root.transform.SetParent(transform, false);
            root.transform.position = center;

            const int segments = 28;
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * arenaRadius;
            }
            var edge = root.AddComponent<EdgeCollider2D>();
            edge.points = points;

            var rng = new System.Random(seed * 7919 + 13);
            for (int i = 0; i < obstaclesPerArena; i++)
            {
                var obs = new GameObject("Obstaculo");
                obs.transform.SetParent(root.transform, false);

                // Repartidos alrededor del centro, sin taparlo del todo: deben
                // estorbar la linea de tiro, no partir la arena en dos.
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Lerp(1.8f, arenaRadius * 0.55f, (float)rng.NextDouble());
                obs.transform.localPosition =
                    new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, 0f);

                float size = Mathf.Lerp(0.55f, 1.05f, (float)rng.NextDouble());
                obs.AddComponent<CircleCollider2D>().radius = size;

                if (!visible) continue;
                var sprites = obstacleSprites;
                if (sprites == null || sprites.Length == 0)
                {
                    var lib = MapLibrary.Instance;
                    sprites = lib != null ? lib.rockSprites : null;
                }
                if (sprites == null || sprites.Length == 0) continue;
                var sr = obs.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[rng.Next(sprites.Length)];
                sr.sortingOrder = YSorter.OrderForY(center.y + obs.transform.localPosition.y);
                obs.transform.localScale = Vector3.one * (size * 1.6f);
            }

            if (visible) DrawArenaOutline(root, points);
            return root;
        }

        /// Solo en las arenas visibles: dibuja el muro para que se entienda
        /// donde estan los limites al mirar el duelo.
        static void DrawArenaOutline(GameObject root, Vector2[] points)
        {
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = points.Length - 1;
            line.widthMultiplier = 0.16f;
            line.material = AttackVfx.SharedMaterial;
            line.startColor = line.endColor = new Color(0.85f, 0.95f, 1f, 0.35f);
            line.sortingOrder = 1;
            for (int i = 0; i < points.Length - 1; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
        }

        public bool Running { get; private set; }
        public int DuelsCompleted { get; private set; }

        /// Duelos que acabaron por agotar el tiempo sin ningun muerto. Si sube
        /// mucho, el entrenamiento tiene poca senal: los duelistas no se
        /// alcanzan y la mayoria de partidas no distinguen a bueno de malo.
        public int TimedOut { get; private set; }
        public float LastBatchSeconds { get; private set; }

        /// Corre todos los emparejamientos en tandas de 'arenaCount'.
        /// Devuelve un resultado por emparejamiento, en el mismo orden.
        public IEnumerator RunBatch(IList<Matchup> matchups, List<Result> results)
        {
            Running = true;
            results.Clear();
            for (int i = 0; i < matchups.Count; i++) results.Add(default);

            float startReal = Time.realtimeSinceStartup;
            TrainingMode.Begin(timeScale);

            int index = 0;
            while (index < matchups.Count)
            {
                int take = Mathf.Min(arenaCount, matchups.Count - index);
                yield return RunWave(matchups, results, index, take);
                index += take;
            }

            TrainingMode.End();
            LastBatchSeconds = Time.realtimeSinceStartup - startReal;
            DuelsCompleted += matchups.Count;
            Running = false;
        }

        /// Una tanda: se crean 'count' duelos, avanzan en los mismos frames y
        /// se recogen cuando todos terminan o agotan el tiempo.
        IEnumerator RunWave(IList<Matchup> matchups, List<Result> results, int offset, int count)
        {
            ClearSlots();
            if (waveLabels.Length < count) waveLabels = new string[count];

            for (int i = 0; i < count; i++)
            {
                var m = matchups[offset + i];
                Vector2 center = Center(i);
                bool visible = i < visibleArenas;
                waveLabels[i] = $"{m.rivalBrain} · {ClassNames[Mathf.Clamp(m.rivalClass, 0, 4)]}";
                // Cada arena estrena obstaculos: si el terreno fuera siempre el
                // mismo, la red memorizaria ESE mapa en vez de aprender a
                // pelear con estorbos.
                terrain.Add(BuildArenaTerrain(center, visible, offset + i));
                var slot = BuildDuel(m, center, visible);
                slots.Add(slot);
            }

            // Un frame para que corran los Awake/Start de todos los duelistas.
            yield return null;
            foreach (var s in slots)
            {
                if (s.blueAi == null || s.redAi == null) continue;
                s.blueAi.SetFoe(s.red.transform);
                s.redAi.SetFoe(s.blue.transform);
                s.blueAi.ResetDuelStats();
                s.redAi.ResetDuelStats();
            }

            int pending = slots.Count;
            while (pending > 0)
            {
                float dt = Time.deltaTime;
                pending = 0;
                foreach (var s in slots)
                {
                    if (s.finished) continue;
                    s.elapsed += dt;

                    bool blueDead = s.blueStats == null || s.blueStats.IsDead;
                    bool redDead = s.redStats == null || s.redStats.IsDead;
                    // Fin por muerte o por agotar el tiempo. Sin el corte por
                    // tiempo, dos que no se alcanzan bloquearian la arena para
                    // siempre y la tanda entera con ella.
                    bool outOfTime = s.elapsed >= duelTimeout;
                    if (blueDead || redDead || outOfTime)
                    {
                        s.finished = true;
                        if (outOfTime && !blueDead && !redDead) TimedOut++;
                        s.result = new Result
                        {
                            damageDealt = s.blueAi != null ? s.blueAi.DamageDealt : 0f,
                            damageTaken = s.blueAi != null ? s.blueAi.DamageTaken : 0f,
                            won = redDead && !blueDead,
                            lost = blueDead && !redDead,
                            timedOut = outOfTime && !blueDead && !redDead,
                            duration = s.elapsed,
                            actions = s.blueAi != null ? s.blueAi.ActionsTaken : 0,
                        };
                        continue;
                    }
                    pending++;
                }
                if (pending > 0) yield return null;
            }

            for (int i = 0; i < slots.Count; i++)
                results[offset + i] = slots[i].result;

            ClearSlots();
        }

        public static Vector2 ArenaCenter(int index)
        {
            // Rejilla cuadrada para no alejarse demasiado del origen.
            int side = 8;
            int x = index % side, y = index / side;
            return TrainingOrigin + new Vector2(x * ArenaSpacing, y * ArenaSpacing);
        }

        /// Centro de la arena 'index' de ESTE entrenador (rejilla + su bloque).
        public Vector2 Center(int index) => ArenaCenter(index) + blockOffset;

        Slot BuildDuel(Matchup m, Vector2 center, bool visible)
        {
            var slot = new Slot();
            // Se colocan enfrentados y holgados dentro del circulo.
            float half = Mathf.Min(5f, arenaRadius * 0.55f);
            slot.blue = SpawnFighter(true, m.netClass, center + new Vector2(-half, 0f), visible);
            slot.red = SpawnFighter(false, m.rivalClass, center + new Vector2(half, 0f), visible);
            if (slot.blue == null || slot.red == null) { slot.finished = true; return slot; }

            slot.blueAi = slot.blue.AddComponent<DuelistAI>();
            slot.blueAi.brain = CombatBrain.Neural;
            slot.blueAi.net = m.net;
            slot.blueAi.reactionDelay = reactionDelay;

            slot.redAi = slot.red.AddComponent<DuelistAI>();
            slot.redAi.brain = m.rivalBrain;
            slot.redAi.reactionDelay = reactionDelay;

            slot.blueStats = slot.blue.GetComponent<CharacterStats>();
            slot.redStats = slot.red.GetComponent<CharacterStats>();
            return slot;
        }

        /// Sube el dano SOLO durante el entrenamiento, para que los duelos se
        /// resuelvan antes y quepan mas generaciones por hora. Se suma a todos
        /// los ataques por igual: multiplicar habria alterado el equilibrio
        /// entre clases (el circulo del mago pega 34 y la flecha 12), y la red
        /// aprenderia un juego que no es el que se juega luego.
        void BoostDamage(CharacterCombat c)
        {
            if (bonusDamage <= 0f) return;
            c.sweepDamage += bonusDamage;
            c.stabDamage += bonusDamage;
            switch (c)
            {
                case ArcherCombat a:
                    a.artilleryDamage += bonusDamage;
                    a.tripleShotDamage += bonusDamage;
                    break;
                case MageCombat m:
                    m.boltDamage += bonusDamage;
                    m.circleDamage += bonusDamage;
                    break;
                case MonkCombat k:
                    k.chargeDamage += bonusDamage;
                    break;
            }
        }

        GameObject SpawnFighter(bool blueSide, int classIndex, Vector2 pos, bool visible)
        {
            GameObject prefab = null;
            var own = blueSide ? bluePrefabs : redPrefabs;
            if (own != null && classIndex < own.Length) prefab = own[classIndex];

            if (prefab == null && blueSide)
            {
                var s = ClassSelectScreen.Instance;
                if (s != null)
                    prefab = classIndex == 4 ? s.magePrefab
                           : classIndex == 3 ? s.monkPrefab
                           : classIndex == 2 ? s.archerPrefab
                           : classIndex == 1 ? s.lancerPrefab : s.warriorPrefab;
            }
            else if (prefab == null)
            {
                var lib = MapLibrary.Instance;
                prefab = lib != null && lib.enemyPrefabs != null
                    && classIndex < lib.enemyPrefabs.Length ? lib.enemyPrefabs[classIndex] : null;
            }
            if (prefab == null) return null;

            PlayerController.SpawningAlly = true;
            var go = Instantiate(prefab, pos, Quaternion.identity, transform);
            PlayerController.SpawningAlly = false;

            var pc = go.GetComponent<PlayerController>();
            if (pc != null) Destroy(pc);
            var enemyAi = go.GetComponent<EnemyAI>();
            if (enemyAi != null) Destroy(enemyAi);

            var combat = go.GetComponent<CharacterCombat>();
            if (combat != null) { combat.isPlayer = false; BoostDamage(combat); }

            var stats = go.GetComponent<CharacterStats>();
            stats.team = blueSide ? 0 : 1;
            stats.maxHealth = ClassHealth[Mathf.Clamp(classIndex, 0, ClassHealth.Length - 1)];
            stats.Heal(999f);

            var attrs = go.GetComponent<CharacterAttributes>();
            if (attrs == null) attrs = go.AddComponent<CharacterAttributes>();
            attrs.strength = attrs.defense = attrs.speed = 5;
            go.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

            if (!visible) StripVisuals(go);
            return go;
        }

        /// Apaga TODO lo que solo sirve para verse. Es donde se gana la mayor
        /// parte del tiempo: con 80 duelistas, animar y ordenar sprites cada
        /// frame cuesta mas que simular el combate entero.
        static void StripVisuals(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var y in go.GetComponentsInChildren<YSorter>(true)) y.enabled = false;
            foreach (var b in go.GetComponentsInChildren<WorldStatusBars>(true))
                b.gameObject.SetActive(false);
            // UnitAnimator se deja vivo a proposito: es barato (voltea el
            // sprite y lleva un temporizador) y ademas lanza corrutinas de
            // destello que se romperian si el componente estuviera apagado.
        }

        void ClearSlots()
        {
            foreach (var s in slots)
            {
                if (s.blue != null) Destroy(s.blue);
                if (s.red != null) Destroy(s.red);
            }
            slots.Clear();

            foreach (var t in terrain) if (t != null) Destroy(t);
            terrain.Clear();
        }

        void OnDestroy()
        {
            TrainingMode.End();
        }
    }
}
