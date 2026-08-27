using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Orquestador del roguelike: ciudad -> niveles del bosque infinitos con
    /// paradas de descanso cada 3 niveles. El tesoro del nivel 10 (y sus ecos
    /// cada 10 niveles) es un hito con recompensa: la expedicion continua.
    /// El jugador y sus aliados persisten entre mapas dentro de la misma
    /// escena; morir termina la run (R recarga).
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        public Text levelText; // indicador de nivel en el HUD

        public int CurrentLevel { get; private set; } // 0 = ciudad
        public bool InRestStop { get; private set; }
        public MapBuildData CurrentMap { get; private set; }

        Transform content;
        LevelExit exit;
        int enemiesAlive;
        bool firstRestUsed;
        int freeRecruitsGranted; // huecos de aliado ya regalados (niveles 6/12/18)
        string labelKey = "zone.town"; // rotulo actual (para refrescar al cambiar idioma)
        int labelArg;

        void Awake()
        {
            Instance = this;
        }

        void OnEnable()
        {
            Loc.LanguageChanged += RefreshLabel;
        }

        void OnDisable()
        {
            Loc.LanguageChanged -= RefreshLabel;
        }

        void Start()
        {
            AllyAI.ResetOrders(); // las ordenes son estaticas: limpiar tras recargar
            LoadTown();
        }

        // ----------------------------------------------------------------

        public void LoadTown()
        {
            CurrentLevel = 0;
            InRestStop = false;
            LoadMap(ForestMaps.Town());
            exit?.Activate();
            SetLevelLabel("zone.town");
        }

        void LoadLevel(int level)
        {
            CurrentLevel = level;
            InRestStop = false;
            var data = ForestMaps.Level(level);
            LoadMap(data);

            SpawnEnemies(data, Difficulty.EnemyCountFor(level));
            SetLevelLabel("zone.level", level);

            if (enemiesAlive <= 0) exit?.Activate(); // por si no hubo sitio
        }

        void LoadRestStop()
        {
            InRestStop = true;
            LoadMap(ForestMaps.RestStop(4000 + CurrentLevel));
            exit?.Activate();
            SetLevelLabel("zone.camp");

            // Reclutamiento de aliados: un hueco nuevo en los niveles 6/12/18
            // (gratis la primera vez por hueco); los sustitutos de caidos se
            // compran. Un reclutador por cada hueco vacio, clases sin repetir.
            int unlocked = Difficulty.AllySlotsFor(CurrentLevel);
            int deficit = unlocked - AllyAI.Active.Count;
            var spots = new[]
            {
                new Vector2(13f, 4.9f), new Vector2(9.6f, 4.9f), new Vector2(16.4f, 4.9f),
            };
            var reservedClasses = new System.Collections.Generic.List<int>();
            int freesPlanned = 0;
            for (int i = 0; i < deficit && i < spots.Length; i++)
            {
                int classIdx = PickAllyClass(reservedClasses);
                if (classIdx < 0) break;
                reservedClasses.Add(classIdx);
                int price = freeRecruitsGranted + freesPlanned < unlocked
                    ? 0 : Difficulty.AllyReplacementPrice;
                if (price == 0) freesPlanned++;
                AllyRecruiter.Create(spots[i], content, classIdx, price);
            }
        }

        /// Clase para el proximo recluta: distinta de la del jugador, de los
        /// aliados vivos y de las ya reservadas. -1 si no queda ninguna libre.
        int PickAllyClass(System.Collections.Generic.List<int> alsoTaken)
        {
            var taken = new System.Collections.Generic.List<int>(alsoTaken);
            if (ClassSelectScreen.Instance != null)
                taken.Add(ClassSelectScreen.Instance.ChosenClassIndex);
            foreach (var ally in AllyAI.Active)
                if (ally != null) taken.Add(ally.classIndex);

            var candidates = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 5; i++) // 5 clases: guerrero/lancero/arquero/monje/mago
                if (!taken.Contains(i)) candidates.Add(i);
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
        }

        public void OnAllyRecruited(bool wasFree)
        {
            if (wasFree) freeRecruitsGranted++;
        }

        /// El jugador cruza la salida del mapa actual.
        public void Advance()
        {
            if (CurrentLevel == 0) { LoadLevel(1); return; }
            if (InRestStop) { LoadLevel(CurrentLevel + 1); return; }
            if (CurrentLevel % 3 == 0) LoadRestStop();
            else LoadLevel(CurrentLevel + 1);
        }

#if UNITY_EDITOR
        /// Saltos directos para las capturas de verificacion (solo editor).
        public void DebugLoadRest(int level) { CurrentLevel = level; LoadRestStop(); }
        public void DebugLoadLevel(int level) { LoadLevel(level); }
#endif

        /// Hito del tesoro (nivel 10 y cada 10 niveles): recompensa y a seguir.
        public void TreasureFound()
        {
            GameManager.Player?.GetComponent<Inventory>()?.AddCoins(Difficulty.TreasureReward);
            Flash(string.Format(Loc.T("msg.treasure"), Difficulty.TreasureReward));
        }

        // ----------------------------------------------------------------

        void LoadMap(MapBuildData data)
        {
            if (content != null) Destroy(content.gameObject);
            foreach (var stray in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                Destroy(stray.gameObject);
            foreach (var stray in FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None))
                Destroy(stray.gameObject);
            foreach (var stray in FindObjectsByType<ArrowStrike>(FindObjectsSortMode.None))
                Destroy(stray.gameObject);
            foreach (var stray in FindObjectsByType<MagicCircleBlast>(FindObjectsSortMode.None))
                Destroy(stray.gameObject);

            content = MapPainter.CreateContentRoot();
            CurrentMap = data;
            MapPainter.Paint(data, content);
            enemiesAlive = 0;
            exit = null;

            // Camara: limites del mapa nuevo y encuadre inmediato.
            var cam = Camera.main != null ? Camera.main.GetComponent<SmoothCameraFollow>() : null;
            if (cam != null)
            {
                cam.boundsMin = Vector2.zero;
                cam.boundsMax = new Vector2(data.W, data.H);
            }

            var player = GameManager.Player;
            if (player != null)
            {
                player.transform.position = data.playerSpawn;
                cam?.SnapToTarget();

                // Los aliados viajan contigo: recolocarlos junto al spawn.
                AllyAI.ResetOrders();
                int slot = 0;
                foreach (var ally in AllyAI.Active)
                {
                    if (ally == null) continue;
                    float angle = 200f + slot * 55f;
                    ally.transform.position = data.playerSpawn + new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 1.4f;
                    slot++;
                }
            }
            else
            {
                // Aun no se eligio clase: la seleccion usara este spawn.
                if (ClassSelectScreen.Instance != null)
                    ClassSelectScreen.Instance.spawnPosition = data.playerSpawn;
                if (cam != null)
                    cam.transform.position = new Vector3(data.playerSpawn.x, data.playerSpawn.y, -10f);
            }

            if (!string.IsNullOrEmpty(data.exitLabel))
                exit = LevelExit.Create(data.exitPos, data.exitLabel, content);

            bool firstVendorPending = !firstRestUsed;
            foreach (var special in data.specials)
            {
                switch (special.kind)
                {
                    case SpecialKind.Campfire:
                        Campfire.Create(special.pos, content);
                        break;
                    case SpecialKind.Vendor:
                        // La primera parada asegura una pocion basica en venta.
                        RestVendor.Create(special.pos, content, forceBasicPotion: firstVendorPending);
                        firstVendorPending = false;
                        firstRestUsed = true;
                        break;
                    case SpecialKind.Treasure:
                        TreasureTrigger.Create(special.pos, content);
                        break;
                    case SpecialKind.Miner:
                        TownNpc.Create(special.pos, content, TownNpc.Mode.Miner);
                        break;
                    case SpecialKind.Chopper:
                        TownNpc.Create(special.pos, content, TownNpc.Mode.Chopper);
                        break;
                    case SpecialKind.Walker:
                        TownNpc.Create(special.pos, content, TownNpc.Mode.Walker);
                        break;
                    case SpecialKind.Sheep:
                        if (MapLibrary.Instance.sheepPrefab != null)
                            Instantiate(MapLibrary.Instance.sheepPrefab, special.pos,
                                Quaternion.identity, content);
                        break;
                }
            }
        }

        void SpawnEnemies(MapBuildData data, int count)
        {
            var lib = MapLibrary.Instance;
            var spawns = data.enemySpawns;
            for (int i = spawns.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (spawns[i], spawns[j]) = (spawns[j], spawns[i]);
            }

            int placed = 0;
            for (int i = 0; i < spawns.Count && placed < count; i++)
            {
                var prefab = lib.enemyPrefabs[Random.Range(0, lib.enemyPrefabs.Length)];
                if (prefab == null) continue;
                var enemy = Instantiate(prefab, spawns[i], Quaternion.identity, content);
                enemiesAlive++;
                placed++;

                // Estadisticas del enemigo por nivel: los 3 primeros niveles son
                // mas amables (3 en todo); despues, base 5 mas los puntos extra
                // de la espiral infinita (nivel 16+, +1 cada 2 niveles).
                int statValue = CurrentLevel <= 3 ? 3 : 5;
                var (bonusStr, bonusDef, bonusSpd) = Difficulty.StatBonusFor(CurrentLevel);
                var attrs = enemy.AddComponent<CharacterAttributes>();
                attrs.strength = statValue + bonusStr;
                attrs.defense = statValue + bonusDef;
                attrs.speed = statValue + bonusSpd;
                enemy.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

                // Nivel de inteligencia de la IA segun la profundidad del bosque.
                enemy.GetComponent<EnemyAI>()?.ApplyTier(Difficulty.AiTierFor(CurrentLevel));

                var enemyStats = enemy.GetComponent<CharacterStats>();
                var enemyGo = enemy;
                enemyStats.Died += () =>
                {
                    GameManager.HandleEnemyCorpse(enemyGo);
                    OnEnemyDied();
                };
            }
            if (placed < count)
                Debug.LogWarning($"[GameFlow] Solo se colocaron {placed}/{count} enemigos.");
        }

        void OnEnemyDied()
        {
            enemiesAlive--;
            if (enemiesAlive <= 0)
            {
                exit?.Activate();
                AllyAI.ResetOrders(); // sin enemigos, los aliados vuelven a seguirte
                // No pisar el mensaje de muerte/victoria con el aviso de nivel.
                if (!GameManager.IsGameOver)
                    StartCoroutine(FlashMessage(Loc.T("msg.clean")));
            }
        }

        /// Mensaje breve en pantalla (no pisa muerte/victoria).
        public void Flash(string text)
        {
            if (!GameManager.IsGameOver) StartCoroutine(FlashMessage(text));
        }

        IEnumerator FlashMessage(string text)
        {
            GameManager.ShowMessage(text);
            yield return new WaitForSeconds(1.6f);
            GameManager.ClearMessageIf(text);
        }

        void SetLevelLabel(string key, int arg = 0)
        {
            labelKey = key;
            labelArg = arg;
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (levelText == null || string.IsNullOrEmpty(labelKey)) return;
            string text = Loc.T(labelKey);
            if (text.Contains("{0}")) text = string.Format(text, labelArg);
            levelText.text = text;
        }
    }
}
