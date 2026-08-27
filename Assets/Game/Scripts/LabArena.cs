using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Escena de pruebas (Lab): un coliseo cerrado por muros invisibles, sin
    /// expedicion ni niveles. Sirve para probar clases, IA y habilidades en un
    /// espacio controlado.
    ///
    /// El suelo de arena marca EXACTAMENTE donde esta el muro: la colision es
    /// invisible (CollisionTile sin sprite) y sigue el borde de la elipse.
    ///
    /// Teclas del lab (ademas de los controles normales):
    ///   F1-F5  invocar enemigo (guerrero/lancero/arquero/monje/mago) en el cursor
    ///   F6     limpiar todos los enemigos
    ///   F7     curar al grupo a tope y rellenar energia
    ///   F8     invocar un aliado de una clase libre
    public class LabArena : MonoBehaviour
    {
        public static LabArena Instance { get; private set; }

        [Header("HUD")]
        public Text titleText;
        public Text hintText;

        [Header("Coliseo")]
        public int width = 34;
        public int height = 26;
        public float arenaRadiusX = 13f;
        public float arenaRadiusY = 9.5f;

        public MapBuildData Arena { get; private set; }
        public Vector2 Center => center;

        Transform content;
        Vector2 center;

        void Awake()
        {
            Instance = this;
            // En el lab no hay pantalla de titulo: directo a elegir clase.
            TitleScreen.SkipTitleOnce = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() { Loc.LanguageChanged += RefreshTexts; }
        void OnDisable() { Loc.LanguageChanged -= RefreshTexts; }

        void Start()
        {
            BuildArena();
            RefreshTexts();
        }

        /// Torneo de cerebros de combate (F9): 6 duelos espejo + 3 cruzados.
        public ColiseumTournament StartTournament(float speed = 1f, int reps = 1)
        {
            var tournament = GetComponent<ColiseumTournament>();
            if (tournament == null) tournament = gameObject.AddComponent<ColiseumTournament>();
            if (tournament.Running) return tournament;
            tournament.speedMultiplier = speed;
            tournament.repetitions = Mathf.Max(1, reps);
            ClearEnemies();
            tournament.StartTournament();
            return tournament;
        }

        /// Liga de las 30 combinaciones (5 clases x 6 algoritmos), todas
        /// contra todas.
        public ColiseumTournament StartLeague(float speed = 1f, float timeout = 22f)
        {
            var tournament = GetComponent<ColiseumTournament>();
            if (tournament == null) tournament = gameObject.AddComponent<ColiseumTournament>();
            if (tournament.Running) return tournament;
            tournament.speedMultiplier = speed;
            tournament.fightTimeout = timeout;
            ClearEnemies();
            tournament.StartLeague();
            return tournament;
        }

        // ----------------------------------------------------------------
        //  Construccion del coliseo
        // ----------------------------------------------------------------

        void BuildArena()
        {
            center = new Vector2(width / 2f, height / 2f);

            var data = new MapBuildData(width, height, 20260828);
            data.baseColor = 2;          // hierba alrededor
            data.FillLand();
            data.exitLabel = "";         // el lab no tiene salida
            data.playerSpawn = center;

            // Elipse nitida (sin ruido): dentro = suelo de arena, fuera = muro
            // invisible. Asi el borde visible del suelo ES el muro.
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    float nx = (x + 0.5f - center.x) / arenaRadiusX;
                    float ny = (y + 0.5f - center.y) / arenaRadiusY;
                    if (nx * nx + ny * ny <= 1f) data.patch[x, y] = 4; // tierra batida
                    else data.blocked[x, y] = true;                    // muro invisible
                }

            Arena = data;
            content = MapPainter.CreateContentRoot();
            MapPainter.Paint(data, content);

            var cam = Camera.main != null ? Camera.main.GetComponent<SmoothCameraFollow>() : null;
            if (cam != null)
            {
                cam.boundsMin = Vector2.zero;
                cam.boundsMax = new Vector2(width, height);
            }

            var player = GameManager.Player;
            if (player != null)
            {
                player.transform.position = center;
                cam?.SnapToTarget();
            }
            else
            {
                if (ClassSelectScreen.Instance != null)
                    ClassSelectScreen.Instance.spawnPosition = center;
                if (cam != null)
                    cam.transform.position = new Vector3(center.x, center.y, -10f);
            }
        }

        void RefreshTexts()
        {
            if (titleText != null) titleText.text = Loc.T("lab.title");
            if (hintText != null) hintText.text = Loc.T("lab.keys");
        }

        // ----------------------------------------------------------------
        //  Teclas del lab
        // ----------------------------------------------------------------

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.timeScale <= 0f) return;

            if (keyboard.f1Key.wasPressedThisFrame) SpawnEnemy(0);
            if (keyboard.f2Key.wasPressedThisFrame) SpawnEnemy(1);
            if (keyboard.f3Key.wasPressedThisFrame) SpawnEnemy(2);
            if (keyboard.f4Key.wasPressedThisFrame) SpawnEnemy(3);
            if (keyboard.f5Key.wasPressedThisFrame) SpawnEnemy(4);
            if (keyboard.f6Key.wasPressedThisFrame) ClearEnemies();
            if (keyboard.f7Key.wasPressedThisFrame) HealParty();
            if (keyboard.f8Key.wasPressedThisFrame) SpawnAlly();
            if (keyboard.f9Key.wasPressedThisFrame) StartTournament();
            if (keyboard.f10Key.wasPressedThisFrame) StartLeague();
        }

        /// Punto bajo el cursor, siempre dentro del coliseo.
        Vector2 CursorInsideArena()
        {
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam == null || mouse == null) return center;

            Vector2 p = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 d = p - center;
            float nx = d.x / (arenaRadiusX - 1f), ny = d.y / (arenaRadiusY - 1f);
            float k = Mathf.Sqrt(nx * nx + ny * ny);
            if (k > 1f) d /= k;   // reproyectar al borde util de la elipse
            return center + d;
        }

        public void SpawnEnemy(int classIndex) => SpawnEnemyAt(classIndex, CursorInsideArena());

        public void SpawnEnemyAt(int classIndex, Vector2 pos)
        {
            var lib = MapLibrary.Instance;
            if (lib == null || lib.enemyPrefabs == null
                || classIndex < 0 || classIndex >= lib.enemyPrefabs.Length) return;
            var prefab = lib.enemyPrefabs[classIndex];
            if (prefab == null) return;

            var enemy = Instantiate(prefab, pos, Quaternion.identity, content);

            var attrs = enemy.AddComponent<CharacterAttributes>();
            attrs.strength = attrs.defense = attrs.speed = 5;
            enemy.GetComponent<CharacterMotor>()?.RefreshAttributesCache();
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.ApplyTier(2); // reflejos al maximo para probar
                // Con cerebro instalado usan TODO su kit (el mago, su rayo de
                // hielo); sin el se quedaban en la logica basica por clase.
                var brains = (CombatBrain[])System.Enum.GetValues(typeof(CombatBrain));
                ai.ApplyBrain(brains[Random.Range(0, brains.Length)]);
            }

            var stats = enemy.GetComponent<CharacterStats>();
            var enemyGo = enemy;
            if (stats != null) stats.Died += () => GameManager.HandleEnemyCorpse(enemyGo);
        }

        public void ClearEnemies()
        {
            var alive = EnemyAI.Active.ToArray(); // copia: destruir muta la lista
            foreach (var enemy in alive)
                if (enemy != null) Destroy(enemy.gameObject);
        }

        public void HealParty()
        {
            var player = GameManager.Player;
            var stats = player != null ? player.GetComponent<CharacterStats>() : null;
            if (stats != null && !stats.IsDead)
            {
                stats.Heal(stats.maxHealth);
                stats.RefillEnergy();
                stats.RestoreMana();
                player.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.55f, 1f, 0.6f, 1f));
            }
            foreach (var ally in AllyAI.Active)
            {
                if (ally == null || ally.Stats == null || ally.Stats.IsDead) continue;
                ally.Stats.Heal(ally.Stats.maxHealth);
                ally.Stats.RefillEnergy();
                ally.Stats.RestoreMana();
                ally.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.55f, 1f, 0.6f, 1f));
            }
        }

        /// Aliado de una clase libre (distinta de la del jugador y de la de los
        /// aliados vivos), como en las zonas de descanso.
        public void SpawnAlly()
        {
            var taken = new System.Collections.Generic.List<int>();
            if (ClassSelectScreen.Instance != null)
                taken.Add(ClassSelectScreen.Instance.ChosenClassIndex);
            foreach (var ally in AllyAI.Active)
                if (ally != null) taken.Add(ally.classIndex);

            var free = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 5; i++)
                if (!taken.Contains(i)) free.Add(i);
            if (free.Count == 0) return;

            AllyAI.Spawn(free[Random.Range(0, free.Count)], CursorInsideArena());
        }
    }
}
