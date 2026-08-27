using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Orquestador del roguelike: ciudad -> niveles del bosque -> paradas de
    /// descanso cada 3 niveles -> tesoro en el nivel 10. El jugador persiste
    /// entre mapas dentro de la misma escena; morir termina la run (R recarga).
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

            // Dificultad: 1 enemigo, +1 cada 3 niveles.
            int enemyCount = 1 + (level - 1) / 3;
            SpawnEnemies(data, enemyCount);
            SetLevelLabel("zone.level", level);

            if (enemiesAlive <= 0) exit?.Activate(); // por si no hubo sitio
        }

        void LoadRestStop()
        {
            InRestStop = true;
            LoadMap(ForestMaps.RestStop(4000 + CurrentLevel));
            exit?.Activate();
            SetLevelLabel("zone.camp");
        }

        /// El jugador cruza la salida del mapa actual.
        public void Advance()
        {
            if (CurrentLevel == 0) { LoadLevel(1); return; }
            if (InRestStop) { LoadLevel(CurrentLevel + 1); return; }
            if (CurrentLevel >= 10) return; // el 10 termina con el tesoro
            if (CurrentLevel % 3 == 0) LoadRestStop();
            else LoadLevel(CurrentLevel + 1);
        }

        public void Victory()
        {
            GameManager.TriggerEnd(Loc.T("msg.victory"));
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
                // mas amables (3 en todo); despues, el valor base 5.
                int statValue = CurrentLevel <= 3 ? 3 : 5;
                var attrs = enemy.AddComponent<CharacterAttributes>();
                attrs.strength = statValue;
                attrs.defense = statValue;
                attrs.speed = statValue;
                enemy.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

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
                // No pisar el mensaje de muerte/victoria con el aviso de nivel.
                if (!GameManager.IsGameOver)
                    StartCoroutine(FlashMessage(Loc.T("msg.clean")));
            }
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
