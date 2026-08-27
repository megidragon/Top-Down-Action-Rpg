using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace TinyRpg.EditorTools
{
    /// Construye la escena base del roguelike: sistemas, prefabs, MapLibrary con
    /// todas las referencias de assets, camara, HUD y seleccion de clase. Los
    /// mapas (ciudad, niveles del bosque, campamentos) los pinta GameFlow en
    /// runtime; aqui NO se hornea ningun mapa.
    public static class SceneBuilder
    {
        const string TS = "Assets/Tiny Swords/";
        const string TilesDir = TS + "Terrain/Tileset/Tilemap Settings/Sliced Tiles/";
        const string TileSettingsDir = TS + "Terrain/Tileset/Tilemap Settings/";
        const string OutDir = "Assets/Game";
        const string ScenePath = OutDir + "/Scenes/Game.unity";

        static System.Random rng;
        static Sprite coinIconSprite;   // moneda dropeada (Tiny Fantasy)
        static Sprite potionIconSprite; // pocion del inventario (Tiny Fantasy)

        // Conservado por compatibilidad con MapValidation (valida el generador
        // de isla antiguo, ya no usado por el juego).
        public static readonly Vector2Int PlayerSpawnHint = new Vector2Int(46, 20);

        [MenuItem("TinyRpg/Construir escena del juego")]
        public static void BuildAll()
        {
            try
            {
                rng = new System.Random(20260827);
                EnsureFolders();

                // ---- Animadores ----
                var warriorBlue = BuildWarriorController("Blue");
                var warriorRed = BuildWarriorController("Red");
                var lancerBlue = BuildLancerController("Blue");
                var lancerRed = BuildLancerController("Red");
                var archerBlue = BuildArcherController("Blue");
                var archerRed = BuildArcherController("Red");
                var monkBlue = BuildMonkController("Blue");
                var monkRed = BuildMonkController("Red");
                var sheepController = BuildSheepController();
                var pawnController = BuildPawnNpcController();

                // ---- Iconos ----
                coinIconSprite = LoadIcon("Assets/Tiny Fantasy Icons/Coins/Coins_Medium_Gold.png");
                potionIconSprite = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Red.png");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // ---- Prefabs ----
                var playerWarrior = BuildCharacterPrefab("Player_Warrior", warriorBlue, "Blue",
                    true, WarriorPlayerTuning());
                var playerLancer = BuildCharacterPrefab("Player_Lancer", lancerBlue, "Blue",
                    true, LancerPlayerTuning());
                var playerArcher = BuildCharacterPrefab("Player_Archer", archerBlue, "Blue",
                    true, ArcherPlayerTuning());
                var playerMonk = BuildCharacterPrefab("Player_Monk", monkBlue, "Blue",
                    true, MonkPlayerTuning());

                var enemyWarrior = BuildCharacterPrefab("Enemy_Warrior", warriorRed, "Red", false);
                var enemyLancer = BuildCharacterPrefab("Enemy_Lancer", lancerRed, "Red",
                    false, EnemyLancerTuning());
                var enemyArcher = BuildCharacterPrefab("Enemy_Archer", archerRed, "Red",
                    false, EnemyArcherTuning());
                var enemyMonk = BuildCharacterPrefab("Enemy_Monk", monkRed, "Red",
                    false, EnemyMonkTuning());
                var sheepPrefab = BuildSheepPrefab(sheepController);
                var pawnPrefab = BuildPawnNpcPrefab(pawnController);

                // ---- Escena ----
                var layers = BuildEmptyTilemaps();
                var cameraFollow = BuildCameraAndLight();
                BuildMapLibrary(layers,
                    new[] { enemyWarrior, enemyLancer, enemyArcher, enemyMonk },
                    sheepPrefab, pawnPrefab);
                BuildHudAndManagers(playerWarrior, playerLancer, playerArcher, playerMonk,
                    cameraFollow);

                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SceneBuilder] Escena construida correctamente en " + ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneBuilder] FALLO: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { OutDir, OutDir + "/Anim", OutDir + "/Prefabs", OutDir + "/Scenes", OutDir + "/Tiles" })
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'),
                        System.IO.Path.GetFileName(dir));
        }

        // =================================================================
        //  ANIMADORES
        // =================================================================

        static RuntimeAnimatorController BuildWarriorController(string color)
        {
            var (controller, sm) = NewController($"Warrior_{color}_Player");
            var idle = AddState(sm, "Idle", FindClip($"Warrior_Idle_{color}"), loop: true);
            AddState(sm, "Run", FindClip($"Warrior_Run_{color}"), loop: true);
            AddState(sm, "Attack1", FindClip($"Warrior_Attack1_{color}"), loop: false);
            AddState(sm, "Attack2", FindClip($"Warrior_Attack2_{color}"), loop: false);
            AddState(sm, "Guard", FindClip($"Warrior_Guard_{color}"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        static RuntimeAnimatorController BuildLancerController(string color)
        {
            var (controller, sm) = NewController($"Lancer_{color}_Player");
            // Ojo: los .anim del Lancer llevan un espacio tras "Lancer_".
            var idle = AddState(sm, "Idle", FindClip($"Lancer_Idle_{color}"), loop: true);
            AddState(sm, "Run", FindClip($"Lancer_ Run_{color}"), loop: true);
            AddState(sm, "Attack1", FindClip($"Lancer_ Right_Attack_{color}"), loop: false);
            AddState(sm, "Attack2", FindClip($"Lancer_ Right_Attack_{color}"), loop: false);
            AddState(sm, "Guard", FindClip($"Lancer_ Right_Defence_{color}"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        static RuntimeAnimatorController BuildArcherController(string color)
        {
            var (controller, sm) = NewController($"Archer_{color}_Player");
            var idle = AddState(sm, "Idle", FindClip($"Archer_Idle_{color}"), loop: true);
            AddState(sm, "Run", FindClip($"Archer_Run_{color}"), loop: true);
            AddState(sm, "Attack1", FindClip($"Archer_Shoot_{color}"), loop: false);
            AddState(sm, "Attack2", FindClip($"Archer_Shoot_{color}"), loop: false);
            AddState(sm, "Guard", FindClip($"Archer_Idle_{color}"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        static RuntimeAnimatorController BuildMonkController(string color)
        {
            var (controller, sm) = NewController($"Monk_{color}_Player");
            var idle = AddState(sm, "Idle", FindClip($"Monk_Idle_{color}"), loop: true);
            AddState(sm, "Run", FindClip($"Monk_Run_{color}"), loop: true);
            AddState(sm, "Attack1", FindClip($"Monk_Idle_{color}"), loop: false);
            AddState(sm, "Attack2", FindClip($"Monk_Idle_{color}"), loop: false);
            AddState(sm, "Guard", FindClip($"Monk_Idle_{color}"), loop: true);
            AddState(sm, "Heal", FindClip($"Monk_Heal_{color}"), loop: false);
            sm.defaultState = idle;
            return controller;
        }

        static RuntimeAnimatorController BuildSheepController()
        {
            var (controller, sm) = NewController("Sheep");
            var idle = AddState(sm, "Idle", FindClip("Sheep_Idle"), loop: true);
            AddState(sm, "Move", FindClip("Sheep_Run"), loop: true);
            AddState(sm, "Grass", FindClip("Sheep_Grass"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        static RuntimeAnimatorController BuildPawnNpcController()
        {
            var (controller, sm) = NewController("Pawn_Vendor");
            var idle = AddState(sm, "Idle", FindClip("Pawn_Idle_Blue"), loop: true);
            AddState(sm, "Run", FindClip("Pawn_Run_Blue"), loop: true);
            // Los .anim del pawn llevan espacio en el nombre de la herramienta.
            AddState(sm, "Mine", FindClip("Pawn_Interact Pickaxe_Blue"), loop: true);
            AddState(sm, "Chop", FindClip("Pawn_Interact Axe_Blue"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        static (AnimatorController, AnimatorStateMachine) NewController(string name)
        {
            string path = $"{OutDir}/Anim/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            return (controller, controller.layers[0].stateMachine);
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, bool loop)
        {
            if (clip != null && loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (!settings.loopTime)
                {
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    EditorUtility.SetDirty(clip);
                }
            }
            var state = sm.AddState(name);
            state.motion = clip;
            return state;
        }

        static AnimationClip FindClip(string exactName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:AnimationClip {exactName}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == exactName)
                    return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
            Debug.LogWarning("[SceneBuilder] Clip no encontrado: " + exactName);
            return null;
        }

        // =================================================================
        //  CARGA DE ASSETS
        // =================================================================

        static Sprite GetWhiteSprite()
        {
            string path = OutDir + "/Tiles/White.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite LoadIcon(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[SceneBuilder] Icono no encontrado: " + path);
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 256f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 256f;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite LoadFirstSprite(string path)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                Debug.LogWarning("[SceneBuilder] Sin sprites en " + path);
                return null;
            }
            var zero = sprites.FirstOrDefault(s => s.name.EndsWith("_0"));
            if (zero != null) return zero;
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites[0];
        }

        static TileBase LoadTile(string path)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile == null) Debug.LogWarning("[SceneBuilder] Tile no encontrado: " + path);
            return tile;
        }

        static TileBase[] LoadColorTiles(int colorIndex)
        {
            var tiles = new TileBase[44];
            for (int i = 0; i < 44; i++)
                tiles[i] = LoadTile($"{TilesDir}Tilemap_color{colorIndex}_{i}.asset");
            return tiles;
        }

        // =================================================================
        //  AJUSTES POR CLASE
        // =================================================================

        class UnitTuning
        {
            public string idlePngPath;
            public bool ranged;
            public bool monk;
            public float maxHealth = 150f;
            public float spriteYOffset = 0.62f;
            public float sweepRange = 1.8f;
            public float sweepKnockback = 5f;
            public float sweepDamage = 20f;
            public float stabRange = 3.0f;
            public float stabDamage = 30f;
            public float attackDuration = 0.4f;
            public float hitDelay = 0.18f;
            public float attackRecovery = 0.1f;
        }

        static UnitTuning WarriorPlayerTuning() => new UnitTuning();

        static UnitTuning LancerPlayerTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Blue Units/Lancer/Lancer_Idle.png",
            maxHealth = 112f,
            spriteYOffset = 0.68f,
            sweepRange = 2.5f,
            sweepDamage = 25f,
            stabRange = 3.8f,
            stabDamage = 36f,
            attackDuration = 0.55f,
            hitDelay = 0.24f,
            attackRecovery = 0.325f,
        };

        static UnitTuning ArcherPlayerTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Blue Units/Archer/Archer_Idle.png",
            ranged = true,
            maxHealth = 75f,
            attackDuration = 0.55f,
            attackRecovery = 0.325f,
        };

        static UnitTuning MonkPlayerTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Blue Units/Monk/Idle.png",
            monk = true,
            maxHealth = 125f,
            sweepRange = 1.4f,
            sweepDamage = 20f,
            sweepKnockback = 12f,
            attackDuration = 0.35f,
            hitDelay = 0.14f,
            attackRecovery = 0.1f,
        };

        // Enemigos: mismas identidades con menos vida que el jugador.
        static UnitTuning EnemyLancerTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Red Units/Lancer/Lancer_Idle.png",
            maxHealth = 75f,
            spriteYOffset = 0.68f,
            sweepRange = 2.5f,
            sweepDamage = 25f,
            stabRange = 3.8f,
            stabDamage = 36f,
            attackDuration = 0.55f,
            hitDelay = 0.24f,
            attackRecovery = 0.325f,
        };

        static UnitTuning EnemyArcherTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Red Units/Archer/Archer_Idle.png",
            ranged = true,
            maxHealth = 55f,
            attackDuration = 0.55f,
            attackRecovery = 0.325f,
        };

        static UnitTuning EnemyMonkTuning() => new UnitTuning
        {
            idlePngPath = TS + "Units/Red Units/Monk/Idle.png",
            monk = true,
            maxHealth = 85f,
            sweepRange = 1.4f,
            sweepDamage = 20f,
            sweepKnockback = 12f,
            attackDuration = 0.35f,
            hitDelay = 0.14f,
            attackRecovery = 0.1f,
        };

        // =================================================================
        //  PREFABS
        // =================================================================

        static GameObject BuildCharacterPrefab(string name, RuntimeAnimatorController controller,
            string color, bool isPlayer, UnitTuning tuning = null)
        {
            string idlePath = tuning?.idlePngPath ?? $"{TS}Units/{color} Units/Warrior/Warrior_Idle.png";
            var idleSprite = LoadFirstSprite(idlePath);

            var root = new GameObject(name);
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.3f;
                col.offset = new Vector2(0f, 0.32f);

                var stats = root.AddComponent<CharacterStats>();
                stats.team = isPlayer ? 0 : 1;
                stats.maxHealth = tuning != null ? tuning.maxHealth : (isPlayer ? 150f : 100f);

                root.AddComponent<CharacterMotor>();
                CharacterCombat combat = tuning != null && tuning.ranged
                    ? root.AddComponent<ArcherCombat>()
                    : tuning != null && tuning.monk
                        ? root.AddComponent<MonkCombat>()
                        : root.AddComponent<CharacterCombat>();
                combat.isPlayer = isPlayer;
                if (tuning != null)
                {
                    combat.sweepRange = tuning.sweepRange;
                    combat.sweepKnockback = tuning.sweepKnockback;
                    combat.sweepDamage = tuning.sweepDamage;
                    combat.stabRange = tuning.stabRange;
                    combat.stabDamage = tuning.stabDamage;
                    combat.attackDuration = tuning.attackDuration;
                    combat.hitDelay = tuning.hitDelay;
                    combat.attackRecovery = tuning.attackRecovery;
                }

                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                spriteGo.transform.localPosition = new Vector3(0f, tuning?.spriteYOffset ?? 0.62f, 0f);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = idleSprite;
                var animator = spriteGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                var unitAnim = root.AddComponent<UnitAnimator>();
                unitAnim.animator = animator;
                unitAnim.spriteRenderer = sr;

                var sorter = root.AddComponent<YSorter>();
                sorter.renderers = new[] { sr };

                BuildWorldBars(root);

                if (isPlayer)
                {
                    root.AddComponent<CharacterAttributes>();
                    root.AddComponent<Inventory>();
                    root.AddComponent<PlayerController>();
                }
                else
                {
                    root.AddComponent<EnemyAI>();
                }

                string prefabPath = $"{OutDir}/Prefabs/{name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void BuildWorldBars(GameObject root)
        {
            var baseSprite = LoadFirstSprite(TS + "UI Elements/Bars/SmallBar_Base.png");
            var fillSprite = LoadFirstSprite(TS + "UI Elements/Bars/SmallBar_Fill.png");

            var barsGo = new GameObject("Bars");
            barsGo.transform.SetParent(root.transform, false);
            var bars = barsGo.AddComponent<WorldStatusBars>();

            bars.healthFillAnchor = BuildOneBar(barsGo.transform, "Health", baseSprite, fillSprite,
                new Vector3(0f, 1.52f, 0f), new Vector3(0.8f, 0.8f, 1f), Color.white, 30000,
                new Vector2(1.28125f, 3f));
            bars.energyFillAnchor = BuildOneBar(barsGo.transform, "Energy", baseSprite, GetWhiteSprite(),
                new Vector3(0f, 1.33f, 0f), new Vector3(0.62f, 0.5f, 1f), new Color(1f, 0.82f, 0.25f, 1f), 30002,
                new Vector2(10.25f, 1.125f));
        }

        static Transform BuildOneBar(Transform parent, string name, Sprite baseSprite, Sprite fillSprite,
            Vector3 position, Vector3 scale, Color fillTint, int sortingOrder, Vector2 fillScale)
        {
            var baseGo = new GameObject(name + "Base");
            baseGo.transform.SetParent(parent, false);
            baseGo.transform.localPosition = position;
            baseGo.transform.localScale = scale;
            var baseSr = baseGo.AddComponent<SpriteRenderer>();
            baseSr.sprite = baseSprite;
            baseSr.sortingOrder = sortingOrder;

            var anchor = new GameObject(name + "FillAnchor");
            anchor.transform.SetParent(baseGo.transform, false);
            anchor.transform.localPosition = new Vector3(-0.6406f, 0f, 0f);

            var fillGo = new GameObject(name + "Fill");
            fillGo.transform.SetParent(anchor.transform, false);
            fillGo.transform.localPosition = new Vector3(0.6406f, 0f, 0f);
            fillGo.transform.localScale = new Vector3(fillScale.x, fillScale.y, 1f);
            var fillSr = fillGo.AddComponent<SpriteRenderer>();
            fillSr.sprite = fillSprite;
            fillSr.color = fillTint;
            fillSr.sortingOrder = sortingOrder + 1;

            return anchor.transform;
        }

        static GameObject BuildSheepPrefab(RuntimeAnimatorController controller)
        {
            var sprite = LoadFirstSprite(TS + "Pawn and Resources/Meat/Sheep/Sheep_Idle.png");
            var root = new GameObject("Sheep");
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.25f;
                col.offset = new Vector2(0f, 0.25f);
                root.AddComponent<SheepAmbient>();

                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                float yOff = sprite != null ? sprite.bounds.extents.y - 0.35f : 0.5f;
                spriteGo.transform.localPosition = new Vector3(0f, yOff, 0f);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                var animator = spriteGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                var sorter = root.AddComponent<YSorter>();
                sorter.renderers = new[] { sr };

                return PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/Prefabs/Sheep.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static GameObject BuildPawnNpcPrefab(RuntimeAnimatorController controller)
        {
            var sprite = LoadFirstSprite(TS + "Pawn and Resources/Pawn/Blue Pawn/Pawn_Idle.png");
            var root = new GameObject("PawnNpc");
            try
            {
                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.26f;
                col.offset = new Vector2(0f, 0.3f);
                root.AddComponent<TownNpc>();

                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                spriteGo.transform.localPosition = new Vector3(0f, 0.62f, 0f);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                var animator = spriteGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                var sorter = root.AddComponent<YSorter>();
                sorter.renderers = new[] { sr };

                return PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/Prefabs/PawnNpc.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =================================================================
        //  ESCENA: TILEMAPS VACIOS, LIBRERIA, CAMARA
        // =================================================================

        static Tilemap[] BuildEmptyTilemaps()
        {
            var gridGo = new GameObject("World");
            gridGo.AddComponent<Grid>();

            Tilemap NewLayer(string name, int order, bool withRenderer = true)
            {
                var go = new GameObject(name);
                go.transform.SetParent(gridGo.transform, false);
                var tilemap = go.AddComponent<Tilemap>();
                if (withRenderer)
                {
                    var renderer = go.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = order;
                }
                return tilemap;
            }

            var water = NewLayer("WaterBG", -100);
            var foam = NewLayer("Foam", -96);
            var ground = NewLayer("Ground", -92);
            var detail = NewLayer("GroundDetail", -88);

            var collision = NewLayer("Collision", 0, withRenderer: false);
            var rb = collision.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var tileCollider = collision.gameObject.AddComponent<TilemapCollider2D>();
            var composite = collision.gameObject.AddComponent<CompositeCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            return new[] { water, foam, ground, detail, collision };
        }

        static void BuildMapLibrary(Tilemap[] layers, GameObject[] enemyPrefabs,
            GameObject sheepPrefab, GameObject pawnPrefab)
        {
            // Tile invisible de colision.
            string tilePath = OutDir + "/Tiles/CollisionTile.asset";
            AssetDatabase.DeleteAsset(tilePath);
            var collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.name = "CollisionTile";
            collisionTile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(collisionTile, tilePath);

            var go = new GameObject("MapLibrary");
            var lib = go.AddComponent<MapLibrary>();

            lib.waterLayer = layers[0];
            lib.foamLayer = layers[1];
            lib.groundLayer = layers[2];
            lib.detailLayer = layers[3];
            lib.collisionLayer = layers[4];

            lib.color1 = LoadColorTiles(1);
            lib.color2 = LoadColorTiles(2);
            lib.color3 = LoadColorTiles(3);
            lib.color4 = LoadColorTiles(4);
            lib.color5 = LoadColorTiles(5);
            lib.waterBgTile = LoadTile(TileSettingsDir + "Water Background color.asset");
            lib.foamTile = LoadTile(TileSettingsDir + "Water Tile animated.asset");
            lib.collisionTile = collisionTile;

            lib.treeSprites = new[]
            {
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Tree1.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Tree2.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Tree3.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Tree4.png"),
            };
            lib.bushSprites = new[]
            {
                LoadFirstSprite(TS + "Terrain/Decorations/Bushes/Bush 1.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Bushes/Bush 2.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Bushes/Bush 3.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Bushes/Bush 4.png"),
            };
            lib.rockSprites = new[]
            {
                LoadFirstSprite(TS + "Terrain/Decorations/Rocks/Rock1.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Rocks/Rock2.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Rocks/Rock3.png"),
                LoadFirstSprite(TS + "Terrain/Decorations/Rocks/Rock4.png"),
            };
            lib.stumpSprites = new[]
            {
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Stump 1.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Stump 2.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Stump 3.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Wood/Trees/Stump 4.png"),
            };
            lib.goldSprites = new[]
            {
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 1.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 2.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 3.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 4.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 5.png"),
                LoadFirstSprite(TS + "Pawn and Resources/Gold/Gold Stones/Gold Stone 6.png"),
            };
            lib.houseSprite = LoadFirstSprite(TS + "Buildings/Blue Buildings/House1.png");
            lib.house2Sprite = LoadFirstSprite(TS + "Buildings/Blue Buildings/House2.png");
            lib.towerSprite = LoadFirstSprite(TS + "Buildings/Blue Buildings/Tower.png");
            lib.woodTableSprite = LoadFirstSprite(TS + "UI Elements/Wood Table/WoodTable.png");
            lib.fireSprite = LoadFirstSprite(TS + "Particle FX/Fire_01.png");
            lib.fireController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                TS + "Particle FX/Fire 1 Animation/Fire 1.controller");

            lib.enemyPrefabs = enemyPrefabs;
            lib.sheepPrefab = sheepPrefab;
            lib.pawnNpcPrefab = pawnPrefab;

            lib.coinHudIcon = LoadFirstSprite(TS + "UI Elements/Icons/Icon_03.png");
            lib.potionSmallIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Small_Red.png");
            lib.potionMediumIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Red.png");
            lib.potionLargeIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Large_Red.png");
            lib.elixirStrengthIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Orange.png");
            lib.elixirDefenseIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Blue.png");
            lib.elixirSpeedIcon = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Green.png");
        }

        static SmoothCameraFollow BuildCameraAndLight()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.28f, 0.6f, 0.6f, 1f);
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<SmoothCameraFollow>();
            follow.target = null;
            follow.boundsMin = Vector2.zero;
            follow.boundsMax = new Vector2(36f, 24f);
            camGo.transform.position = new Vector3(18f, 12f, -10f);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            var itemsGo = new GameObject("ItemLibrary");
            var itemLib = itemsGo.AddComponent<ItemLibrary>();
            // La moneda dropeada usa el MISMO icono que el contador del HUD (Icon_03).
            itemLib.coinIcon = LoadFirstSprite(TS + "UI Elements/Icons/Icon_03.png");
            itemLib.potionIcon = potionIconSprite;

            var vfxGo = new GameObject("VfxLibrary");
            var lib = vfxGo.AddComponent<VfxLibrary>();
            lib.vfxMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (lib.vfxMaterial == null)
                lib.vfxMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            lib.arrowSprite = LoadFirstSprite(TS + "Units/Extra/Arrow/Arrow.png");

            return follow;
        }

        // =================================================================
        //  HUD Y GESTORES
        // =================================================================

        static void BuildHudAndManagers(GameObject playerWarrior, GameObject playerLancer,
            GameObject playerArcher, GameObject playerMonk, SmoothCameraFollow cameraFollow)
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var baseSprite = LoadFirstSprite(TS + "UI Elements/Bars/BigBar_Base.png");
            var fillSprite = LoadFirstSprite(TS + "UI Elements/Bars/BigBar_Fill.png");

            var hud = canvasGo.AddComponent<PlayerHUD>();
            hud.healthFill = BuildHudBar(canvasGo.transform, "Health", baseSprite, fillSprite,
                new Vector2(30f, 150f), new Vector2(384f, 128f), Color.white,
                new Vector2(0.25f, 0f), new Vector2(0.75f, 1f));
            hud.energyFill = BuildHudBar(canvasGo.transform, "Energy", baseSprite, GetWhiteSprite(),
                new Vector2(30f, 60f), new Vector2(307f, 102f), new Color(1f, 0.82f, 0.25f, 1f),
                new Vector2(0.25f, 0.3125f), new Vector2(0.75f, 0.6875f));

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var controls = MakeText(canvasGo.transform, "Controls", font, 22,
                "WASD mover  |  Shift dash  |  Click Izq. atacar  |  Click Der. especial  |  Espacio parry o curar  |  1-4 objetos  |  E interactuar");
            var controlsRt = controls.rectTransform;
            controlsRt.anchorMin = new Vector2(0.5f, 1f);
            controlsRt.anchorMax = new Vector2(0.5f, 1f);
            controlsRt.pivot = new Vector2(0.5f, 1f);
            controlsRt.anchoredPosition = new Vector2(0f, -16f);
            controlsRt.sizeDelta = new Vector2(1500f, 40f);
            controls.alignment = TextAnchor.UpperCenter;
            controls.color = new Color(1f, 1f, 1f, 0.85f);

            // Indicador de nivel (arriba a la derecha).
            var levelText = MakeText(canvasGo.transform, "LevelLabel", font, 30, "Ciudad");
            var lrt = levelText.rectTransform;
            lrt.anchorMin = new Vector2(1f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(1f, 1f);
            lrt.anchoredPosition = new Vector2(-24f, -18f);
            lrt.sizeDelta = new Vector2(400f, 42f);
            levelText.alignment = TextAnchor.UpperRight;
            levelText.color = new Color(1f, 0.93f, 0.7f, 1f);

            var message = MakeText(canvasGo.transform, "Message", font, 52, "");
            var messageRt = message.rectTransform;
            messageRt.anchorMin = new Vector2(0.5f, 0.5f);
            messageRt.anchorMax = new Vector2(0.5f, 0.5f);
            messageRt.pivot = new Vector2(0.5f, 0.5f);
            messageRt.anchoredPosition = Vector2.zero;
            messageRt.sizeDelta = new Vector2(1400f, 400f);
            message.alignment = TextAnchor.MiddleCenter;
            message.color = new Color(1f, 0.95f, 0.8f, 1f);

            BuildInventoryHud(canvasGo.transform, font);

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            manager.messageText = message;

            var flowGo = new GameObject("GameFlow");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.levelText = levelText;

            BuildClassSelect(canvasGo.transform, font, playerWarrior, playerLancer, playerArcher,
                playerMonk, cameraFollow);
        }

        static Image BuildHudBar(Transform parent, string name, Sprite baseSprite, Sprite fillSprite,
            Vector2 position, Vector2 size, Color fillTint, Vector2 fillAnchorMin, Vector2 fillAnchorMax)
        {
            var baseGo = new GameObject(name + "Base");
            baseGo.transform.SetParent(parent, false);
            var baseImg = baseGo.AddComponent<Image>();
            baseImg.sprite = baseSprite;
            var rt = baseImg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var fillGo = new GameObject(name + "Fill");
            fillGo.transform.SetParent(baseGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = fillSprite;
            fillImg.color = fillTint;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            var frt = fillImg.rectTransform;
            frt.anchorMin = fillAnchorMin;
            frt.anchorMax = fillAnchorMax;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            return fillImg;
        }

        /// Barra de inventario abajo-centro + icono de personaje (stats al hacer
        /// hover) a la izquierda + contador de monedas a la derecha.
        static void BuildInventoryHud(Transform canvas, Font font)
        {
            var hudGo = new GameObject("InventoryHud");
            hudGo.transform.SetParent(canvas, false);
            var hudRt = hudGo.AddComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;
            var hud = hudGo.AddComponent<InventoryHud>();
            hud.slotWidgets = new InventoryHud.SlotWidgets[Inventory.SlotCount];

            var white = GetWhiteSprite();
            const float slotSize = 88f;
            const float spacing = 12f;

            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                float x = (i - (Inventory.SlotCount - 1) * 0.5f) * (slotSize + spacing);

                var slotGo = new GameObject("Slot" + (i + 1));
                slotGo.transform.SetParent(hudGo.transform, false);
                var bg = slotGo.AddComponent<Image>();
                bg.sprite = white;
                bg.color = new Color(0.11f, 0.09f, 0.07f, 0.82f);
                var rt = bg.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(x, 26f);
                rt.sizeDelta = new Vector2(slotSize, slotSize);

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(slotGo.transform, false);
                var icon = iconGo.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.enabled = false;
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(64f, 64f);

                var keyText = MakeText(slotGo.transform, "Key", font, 20, (i + 1).ToString());
                keyText.color = new Color(1f, 0.9f, 0.6f, 0.95f);
                var krt = keyText.rectTransform;
                krt.anchorMin = new Vector2(0f, 1f);
                krt.anchorMax = new Vector2(0f, 1f);
                krt.pivot = new Vector2(0f, 1f);
                krt.anchoredPosition = new Vector2(7f, -4f);
                krt.sizeDelta = new Vector2(30f, 26f);
                keyText.alignment = TextAnchor.UpperLeft;

                var countText = MakeText(slotGo.transform, "Count", font, 24, "");
                var crt = countText.rectTransform;
                crt.anchorMin = new Vector2(1f, 0f);
                crt.anchorMax = new Vector2(1f, 0f);
                crt.pivot = new Vector2(1f, 0f);
                crt.anchoredPosition = new Vector2(-7f, 4f);
                crt.sizeDelta = new Vector2(50f, 28f);
                countText.alignment = TextAnchor.LowerRight;

                hud.slotWidgets[i] = new InventoryHud.SlotWidgets { icon = icon, countText = countText };
            }

            float barHalf = (Inventory.SlotCount * (slotSize + spacing)) * 0.5f;

            // ---- Contador de monedas (derecha de la barra) ----
            var coinGo = new GameObject("CoinHud");
            coinGo.transform.SetParent(hudGo.transform, false);
            var coinIcon = coinGo.AddComponent<Image>();
            coinIcon.sprite = LoadFirstSprite(TS + "UI Elements/Icons/Icon_03.png");
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
            var coinRt = coinIcon.rectTransform;
            coinRt.anchorMin = new Vector2(0.5f, 0f);
            coinRt.anchorMax = new Vector2(0.5f, 0f);
            coinRt.pivot = new Vector2(0f, 0f);
            coinRt.anchoredPosition = new Vector2(barHalf + 18f, 40f);
            coinRt.sizeDelta = new Vector2(56f, 56f);

            var coinCount = MakeText(coinGo.transform, "Count", font, 30, "0");
            var ccrt = coinCount.rectTransform;
            ccrt.anchorMin = new Vector2(1f, 0.5f);
            ccrt.anchorMax = new Vector2(1f, 0.5f);
            ccrt.pivot = new Vector2(0f, 0.5f);
            ccrt.anchoredPosition = new Vector2(8f, 0f);
            ccrt.sizeDelta = new Vector2(90f, 40f);
            coinCount.alignment = TextAnchor.MiddleLeft;
            coinCount.color = new Color(1f, 0.9f, 0.5f, 1f);

            var coinHud = coinGo.AddComponent<CoinHud>();
            coinHud.countText = coinCount;

            // ---- Icono de personaje (izquierda de la barra) con panel de stats ----
            var avatarGo = new GameObject("CharacterIcon");
            avatarGo.transform.SetParent(hudGo.transform, false);
            var avatarBg = avatarGo.AddComponent<Image>();
            avatarBg.sprite = white;
            avatarBg.color = new Color(0.11f, 0.09f, 0.07f, 0.82f);
            var art = avatarBg.rectTransform;
            art.anchorMin = new Vector2(0.5f, 0f);
            art.anchorMax = new Vector2(0.5f, 0f);
            art.pivot = new Vector2(1f, 0f);
            art.anchoredPosition = new Vector2(-barHalf - 18f, 26f);
            art.sizeDelta = new Vector2(slotSize, slotSize);

            var avatarImgGo = new GameObject("Avatar");
            avatarImgGo.transform.SetParent(avatarGo.transform, false);
            var avatarImg = avatarImgGo.AddComponent<Image>();
            avatarImg.sprite = LoadFirstSprite(TS + "UI Elements/Human Avatars/Avatars_01.png");
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;
            var airt = avatarImg.rectTransform;
            airt.anchorMin = Vector2.zero;
            airt.anchorMax = Vector2.one;
            airt.offsetMin = new Vector2(8f, 8f);
            airt.offsetMax = new Vector2(-8f, -8f);

            // Panel de estadisticas (banner) que aparece con el hover.
            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(avatarGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite = LoadFirstSprite(TS + "UI Elements/Banners/Banner.png");
            panelImg.raycastTarget = false;
            var prt = panelImg.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(60f, 14f);
            prt.sizeDelta = new Vector2(430f, 510f);

            var statsText = MakeText(panelGo.transform, "Stats", font, 24, "");
            statsText.raycastTarget = false;
            var srt = statsText.rectTransform;
            // Area util del pergamino Banner.png (los rollos ocupan el resto):
            // 16-82% horizontal, 22-78% vertical del lienzo.
            srt.anchorMin = new Vector2(0.17f, 0.23f);
            srt.anchorMax = new Vector2(0.81f, 0.77f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            statsText.alignment = TextAnchor.MiddleLeft;
            statsText.color = new Color(0.25f, 0.16f, 0.08f, 1f);

            var statsPanel = avatarGo.AddComponent<PlayerStatsPanel>();
            statsPanel.panel = panelGo;
            statsPanel.statsText = statsText;
        }

        static void BuildClassSelect(Transform canvas, Font font, GameObject playerWarrior,
            GameObject playerLancer, GameObject playerArcher, GameObject playerMonk,
            SmoothCameraFollow cameraFollow)
        {
            var panelGo = new GameObject("ClassSelectPanel");
            panelGo.transform.SetParent(canvas, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite = GetWhiteSprite();
            panelImg.color = new Color(0.05f, 0.05f, 0.07f, 0.86f);
            var prt = panelImg.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            var title = MakeText(panelGo.transform, "Title", font, 54, "ELIGE TU CLASE");
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -110f);
            trt.sizeDelta = new Vector2(900f, 80f);
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.93f, 0.75f, 1f);

            var bootstrapGo = new GameObject("GameBootstrap");
            var screen = bootstrapGo.AddComponent<ClassSelectScreen>();
            screen.panel = panelGo;
            screen.warriorPrefab = playerWarrior;
            screen.lancerPrefab = playerLancer;
            screen.archerPrefab = playerArcher;
            screen.monkPrefab = playerMonk;
            screen.spawnPosition = new Vector2(18f, 7f); // GameFlow lo actualiza al pintar la ciudad
            screen.cameraFollow = cameraFollow;

            MakeClassCard(panelGo.transform, font, -450f, "Guerrero", "Tecla 1",
                TS + "Units/Blue Units/Warrior/Warrior_Idle.png", screen, 0);
            MakeClassCard(panelGo.transform, font, -150f, "Lancero", "Tecla 2",
                TS + "Units/Blue Units/Lancer/Lancer_Idle.png", screen, 1);
            MakeClassCard(panelGo.transform, font, 150f, "Arquero", "Tecla 3",
                TS + "Units/Blue Units/Archer/Archer_Idle.png", screen, 2);
            MakeClassCard(panelGo.transform, font, 450f, "Monje", "Tecla 4",
                TS + "Units/Blue Units/Monk/Idle.png", screen, 3);
        }

        static void MakeClassCard(Transform parent, Font font, float x, string title,
            string subtitle, string portraitPath, ClassSelectScreen screen, int classIndex)
        {
            bool locked = classIndex < 0;

            var cardGo = new GameObject("Card_" + title);
            cardGo.transform.SetParent(parent, false);
            var bg = cardGo.AddComponent<Image>();
            bg.sprite = GetWhiteSprite();
            bg.color = locked ? new Color(0.1f, 0.09f, 0.08f, 0.95f)
                              : new Color(0.24f, 0.19f, 0.12f, 0.97f);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -30f);
            rt.sizeDelta = new Vector2(260f, 360f);

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(cardGo.transform, false);
            var portrait = portraitGo.AddComponent<Image>();
            portrait.sprite = LoadFirstSprite(portraitPath);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = locked ? new Color(0.04f, 0.04f, 0.05f, 1f) : Color.white;
            var prt2 = portrait.rectTransform;
            prt2.anchorMin = new Vector2(0.5f, 0.5f);
            prt2.anchorMax = new Vector2(0.5f, 0.5f);
            prt2.anchoredPosition = new Vector2(0f, 50f);
            float pixelsPerWorldUnit = 200f / 3f;
            if (portrait.sprite != null)
            {
                float unitsWide = portrait.sprite.rect.width / portrait.sprite.pixelsPerUnit;
                float unitsTall = portrait.sprite.rect.height / portrait.sprite.pixelsPerUnit;
                prt2.sizeDelta = new Vector2(unitsWide * pixelsPerWorldUnit,
                    unitsTall * pixelsPerWorldUnit);
            }
            else
            {
                prt2.sizeDelta = new Vector2(200f, 200f);
            }

            var nameText = MakeText(cardGo.transform, "Name", font, 32, title);
            var nrt = nameText.rectTransform;
            nrt.anchorMin = new Vector2(0.5f, 0f);
            nrt.anchorMax = new Vector2(0.5f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.anchoredPosition = new Vector2(0f, 66f);
            nrt.sizeDelta = new Vector2(240f, 44f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = locked ? new Color(0.5f, 0.47f, 0.42f, 1f) : Color.white;

            var subText = MakeText(cardGo.transform, "Subtitle", font, 22, subtitle);
            var srt = subText.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 24f);
            srt.sizeDelta = new Vector2(240f, 34f);
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = locked ? new Color(0.45f, 0.4f, 0.36f, 1f)
                                   : new Color(1f, 0.85f, 0.45f, 1f);

            if (!locked)
            {
                var button = cardGo.AddComponent<Button>();
                button.targetGraphic = bg;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                UnityEventTools.AddIntPersistentListener(button.onClick, screen.Choose, classIndex);
            }
        }

        static Text MakeText(Transform parent, string name, Font font, int size, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = content;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }

    /// Punto de entrada para la verificacion visual automatizada.
    public static class GameBoot
    {
        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene(SceneBuilder2.ScenePathPublic);
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
                EditorWindow.GetWindow(gameViewType).Focus();
            EditorApplication.EnterPlaymode();
        }
    }

    public static class SceneBuilder2
    {
        public const string ScenePathPublic = "Assets/Game/Scenes/Game.unity";
    }
}
