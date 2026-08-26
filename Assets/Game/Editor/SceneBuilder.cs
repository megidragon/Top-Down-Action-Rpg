using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace TinyRpg.EditorTools
{
    /// Construye el juego completo: animadores, prefabs, mapa (tilemaps + colision),
    /// decoracion, unidades, camara, HUD y guarda la escena Game.unity.
    /// Ejecutable desde el menu o por linea de comandos (batchmode -executeMethod).
    public static class SceneBuilder
    {
        const string TS = "Assets/Tiny Swords/";
        const string TilesDir = TS + "Terrain/Tileset/Tilemap Settings/Sliced Tiles/";
        const string TileSettingsDir = TS + "Terrain/Tileset/Tilemap Settings/";
        const string OutDir = "Assets/Game";
        const string ScenePath = OutDir + "/Scenes/Game.unity";

        // --- Indices del autotile de 16 piezas (mascara de vecinos N=1,S=2,E=4,W=8) ---
        static readonly int[] FlatByMask = BuildMaskTable(
            single: 27, capW: 24, capE: 26, horiz: 25, capS: 3, capN: 19, vert: 11,
            cornerNW: 0, cornerNE: 2, cornerSW: 16, cornerSE: 18,
            edgeN: 1, edgeS: 17, edgeW: 8, edgeE: 10, center: 9);

        static readonly int[] ElevByMask = BuildMaskTable(
            single: 31, capW: 28, capE: 30, horiz: 29, capS: 7, capN: 23, vert: 15,
            cornerNW: 4, cornerNE: 6, cornerSW: 20, cornerSE: 22,
            edgeN: 5, edgeS: 21, edgeW: 12, edgeE: 14, center: 13);

        static int[] BuildMaskTable(int single, int capW, int capE, int horiz,
            int capS, int capN, int vert, int cornerNW, int cornerNE, int cornerSW, int cornerSE,
            int edgeN, int edgeS, int edgeW, int edgeE, int center)
        {
            // bits de vecinos presentes: N=1, S=2, E=4, W=8
            var t = new int[16];
            t[0] = single;
            t[4] = capW; t[8] = capE; t[12] = horiz;
            t[2] = capS; t[1] = capN; t[3] = vert;
            t[6] = cornerNW; t[10] = cornerNE; t[5] = cornerSW; t[9] = cornerSE;
            t[14] = edgeN; t[13] = edgeS; t[7] = edgeW; t[11] = edgeE;
            t[15] = center;
            return t;
        }

        static System.Random rng;
        static RuntimeAnimatorController pawnController;
        static Sprite coinIconSprite;
        static Sprite potionIconSprite;

        // Celda deseada de la aldea del jugador. MapValidation valida la
        // conectividad del mapa desde este mismo punto.
        public static readonly Vector2Int PlayerSpawnHint = new Vector2Int(46, 20);

        [MenuItem("TinyRpg/Construir escena del juego")]
        public static void BuildAll()
        {
            try
            {
                rng = new System.Random(20260826);
                EnsureFolders();

                var controllers = BuildWarriorControllers();
                var sheepController = BuildSheepController();
                pawnController = BuildPawnController();
                coinIconSprite = LoadIcon("Assets/Tiny Fantasy Icons/Coins/Coins_Medium_Gold.png");
                potionIconSprite = LoadIcon("Assets/Tiny Fantasy Icons/Potions/Potion_Medium_Red.png");
                var map = MapGenerator.Generate();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                BuildTilemaps(map);

                var playerPrefab = BuildCharacterPrefab("Player", controllers["Blue"], "Blue", true);
                var enemyPrefabs = new Dictionary<string, GameObject>
                {
                    ["Red"] = BuildCharacterPrefab("Enemy_Red", controllers["Red"], "Red", false),
                    ["Purple"] = BuildCharacterPrefab("Enemy_Purple", controllers["Purple"], "Purple", false),
                    ["Yellow"] = BuildCharacterPrefab("Enemy_Yellow", controllers["Yellow"], "Yellow", false),
                };
                var sheepPrefab = BuildSheepPrefab(sheepController);

                var world = PopulateWorld(map, playerPrefab, enemyPrefabs, sheepPrefab);

                BuildCameraAndLight(world.playerInstance);
                BuildHudAndManagers();

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

        static Dictionary<string, RuntimeAnimatorController> BuildWarriorControllers()
        {
            var result = new Dictionary<string, RuntimeAnimatorController>();
            foreach (var color in new[] { "Blue", "Red", "Purple", "Yellow" })
            {
                string path = $"{OutDir}/Anim/Warrior_{color}.controller";
                AssetDatabase.DeleteAsset(path);
                var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                var sm = controller.layers[0].stateMachine;

                var idle = AddState(sm, "Idle", FindClip($"Warrior_Idle_{color}"), loop: true);
                AddState(sm, "Run", FindClip($"Warrior_Run_{color}"), loop: true);
                AddState(sm, "Attack1", FindClip($"Warrior_Attack1_{color}"), loop: false);
                AddState(sm, "Attack2", FindClip($"Warrior_Attack2_{color}"), loop: false);
                AddState(sm, "Guard", FindClip($"Warrior_Guard_{color}"), loop: true);
                sm.defaultState = idle;
                result[color] = controller;
            }
            return result;
        }

        static RuntimeAnimatorController BuildPawnController()
        {
            string path = $"{OutDir}/Anim/Pawn_Vendor.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            var idle = AddState(sm, "Idle", FindClip("Pawn_Idle_Blue"), loop: true);
            sm.defaultState = idle;
            return controller;
        }

        /// Carga un icono de Tiny Fantasy Icons asegurando importacion como Sprite
        /// con 256 ppu (los PNG son de 256x256 -> 1 unidad de mundo).
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

        static RuntimeAnimatorController BuildSheepController()
        {
            string path = $"{OutDir}/Anim/Sheep.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            var idle = AddState(sm, "Idle", FindClip("Sheep_Idle"), loop: true);
            AddState(sm, "Move", FindClip("Sheep_Run"), loop: true);
            AddState(sm, "Grass", FindClip("Sheep_Grass"), loop: true);
            sm.defaultState = idle;
            return controller;
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
        //  CARGA DE SPRITES
        // =================================================================

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

        /// Sprite blanco generado (los rellenos del pack son rojos y no se pueden
        /// tintar a otros colores por multiplicacion).
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
        //  TILEMAPS
        // =================================================================

        static void BuildTilemaps(MapData map)
        {
            int W = MapData.W, H = MapData.H;

            var gridGo = new GameObject("World");
            gridGo.AddComponent<Grid>();

            var waterBgTile = LoadTile(TileSettingsDir + "Water Background color.asset");
            var foamTile = LoadTile(TileSettingsDir + "Water Tile animated.asset");
            var shadowTile = LoadTile(TileSettingsDir + "Shadow.asset");
            var c1 = LoadColorTiles(1);
            var c2 = LoadColorTiles(2);
            var c3 = LoadColorTiles(3);
            var c4 = LoadColorTiles(4);
            var c5 = LoadColorTiles(5);

            // ---- Fondo de agua (con margen alrededor del mapa) ----
            const int margin = 14;
            var waterMap = NewLayer(gridGo, "WaterBG", -100);
            var waterBounds = new BoundsInt(-margin, -margin, 0, W + margin * 2, H + margin * 2, 1);
            var waterTiles = new TileBase[waterBounds.size.x * waterBounds.size.y];
            for (int i = 0; i < waterTiles.Length; i++) waterTiles[i] = waterBgTile;
            waterMap.SetTilesBlock(waterBounds, waterTiles);

            var mapBounds = new BoundsInt(0, 0, 0, W, H, 1);

            // ---- Espuma ----
            var foamMap = NewLayer(gridGo, "Foam", -96);
            PaintFromMask(foamMap, mapBounds, (x, y) => map.foam[x, y] ? foamTile : null);

            // ---- Suelo llano base (color 1) ----
            var groundMap = NewLayer(gridGo, "Ground", -92);
            PaintFromMask(groundMap, mapBounds, (x, y) =>
                map.land[x, y] ? c1[FlatByMask[MaskOf(map.land, x, y)]] : null);

            // ---- Parches de bioma (colores 3, 4, 5) ----
            var detailMap = NewLayer(gridGo, "GroundDetail", -88);
            var patchSets = new Dictionary<int, TileBase[]> { [3] = c3, [4] = c4, [5] = c5 };
            foreach (var kv in patchSets)
            {
                int colorIdx = kv.Key;
                var mask = new bool[W, H];
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        mask[x, y] = map.detailPatch[x, y] == colorIdx;
                PaintFromMask(detailMap, mapBounds, (x, y) =>
                    mask[x, y] ? kv.Value[FlatByMask[MaskOf(mask, x, y)]] : null, clearFirst: false);
            }

            // ---- Nivel elevado 1 (color 2) + sombra ----
            var shadow1 = NewLayer(gridGo, "Shadow1", -84);
            PaintFromMask(shadow1, mapBounds, (x, y) =>
                map.InBounds(x, y + 1) && map.elev1[x, y + 1] ? shadowTile : null);

            var elev1Map = NewLayer(gridGo, "Elevated1", -80);
            PaintElevated(elev1Map, mapBounds, map, map.elev1, map.cliff1, map.stairTiles1, c2,
                cliffOverWaterAllowed: true);

            // ---- Nivel elevado 2 (color 3) + sombra ----
            var shadow2 = NewLayer(gridGo, "Shadow2", -76);
            PaintFromMask(shadow2, mapBounds, (x, y) =>
                map.InBounds(x, y + 1) && map.elev2[x, y + 1] ? shadowTile : null);

            var elev2Map = NewLayer(gridGo, "Elevated2", -72);
            PaintElevated(elev2Map, mapBounds, map, map.elev2, map.cliff2, map.stairTiles2, c3,
                cliffOverWaterAllowed: false);

            // ---- Colision ----
            BuildCollision(gridGo, map, waterBgTile);
        }

        static Tilemap NewLayer(GameObject grid, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        static int MaskOf(bool[,] mask, int x, int y)
        {
            int m = 0;
            if (GetMask(mask, x, y + 1)) m |= 1; // N
            if (GetMask(mask, x, y - 1)) m |= 2; // S
            if (GetMask(mask, x + 1, y)) m |= 4; // E
            if (GetMask(mask, x - 1, y)) m |= 8; // W
            return m;
        }

        static bool GetMask(bool[,] mask, int x, int y)
        {
            return x >= 0 && x < MapData.W && y >= 0 && y < MapData.H && mask[x, y];
        }

        static void PaintFromMask(Tilemap tilemap, BoundsInt bounds, Func<int, int, TileBase> pick,
            bool clearFirst = true)
        {
            var tiles = new TileBase[bounds.size.x * bounds.size.y];
            bool any = false;
            for (int y = 0; y < bounds.size.y; y++)
                for (int x = 0; x < bounds.size.x; x++)
                {
                    var t = pick(bounds.x + x, bounds.y + y);
                    if (t != null) { tiles[y * bounds.size.x + x] = t; any = true; }
                }
            if (!any) return;
            if (clearFirst)
            {
                tilemap.SetTilesBlock(bounds, tiles);
            }
            else
            {
                // Pintar sin borrar lo ya existente en la capa.
                for (int y = 0; y < bounds.size.y; y++)
                    for (int x = 0; x < bounds.size.x; x++)
                    {
                        var t = tiles[y * bounds.size.x + x];
                        if (t != null) tilemap.SetTile(new Vector3Int(bounds.x + x, bounds.y + y, 0), t);
                    }
            }
        }

        static void PaintElevated(Tilemap tilemap, BoundsInt bounds, MapData map,
            bool[,] elev, bool[,] cliff, Dictionary<Vector2Int, int> stairs, TileBase[] tiles,
            bool cliffOverWaterAllowed)
        {
            PaintFromMask(tilemap, bounds, (x, y) =>
            {
                var cell = new Vector2Int(x, y);
                if (stairs.TryGetValue(cell, out int stairIdx)) return tiles[stairIdx];

                if (elev[x, y]) return tiles[ElevByMask[MaskOf(elev, x, y)]];

                if (cliff[x, y])
                {
                    bool left = GetMask(cliff, x - 1, y);
                    bool right = GetMask(cliff, x + 1, y);
                    int piece = left && right ? 35 : (!left && right ? 34 : (left ? 36 : 37));
                    bool overWater = cliffOverWaterAllowed && !map.land[x, y];
                    if (overWater) piece += 6; // fila de acantilado que toca el agua
                    return tiles[piece];
                }
                return null;
            });
        }

        static void BuildCollision(GameObject grid, MapData map, TileBase _)
        {
            // Tile invisible de colision (caja de celda completa).
            string tilePath = OutDir + "/Tiles/CollisionTile.asset";
            AssetDatabase.DeleteAsset(tilePath);
            var collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.name = "CollisionTile";
            collisionTile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(collisionTile, tilePath);

            // Solo bloqueamos celdas no transitables cercanas a la zona jugable.
            int W = MapData.W, H = MapData.H;
            var near = new bool[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!map.walkable[x, y]) continue;
                    for (int dx = -3; dx <= 3; dx++)
                        for (int dy = -3; dy <= 3; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < W && ny >= 0 && ny < H) near[nx, ny] = true;
                        }
                }

            var go = new GameObject("Collision");
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            var composite = go.AddComponent<CompositeCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var bounds = new BoundsInt(0, 0, 0, W, H, 1);
            PaintFromMask(tilemap, bounds, (x, y) =>
                (!map.walkable[x, y] && near[x, y]) ? (TileBase)collisionTile : null);
        }

        // =================================================================
        //  PREFABS DE PERSONAJES
        // =================================================================

        static GameObject BuildCharacterPrefab(string name, RuntimeAnimatorController controller,
            string color, bool isPlayer)
        {
            string unitDir = $"{TS}Units/{color} Units/Warrior/";
            var idleSprite = LoadFirstSprite(unitDir + "Warrior_Idle.png");

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
                stats.maxHealth = isPlayer ? 150f : 100f;

                root.AddComponent<CharacterMotor>();
                var combat = root.AddComponent<CharacterCombat>();
                combat.isPlayer = isPlayer;

                // Sprite animado (los pies del personaje quedan en el origen del root).
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                spriteGo.transform.localPosition = new Vector3(0f, 0.62f, 0f);
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

            // Ranura medida en SmallBar_Base.png: X 55-136, Y 27-35 (de 192x64).
            // La franja de SmallBar_Fill es de 3 px a ancho completo: escala 3 en Y
            // para llenar la ranura de 9 px.
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

            // Ancla en el borde izquierdo de la ranura; su escala X es la fraccion.
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

        /// NPC vendedor: Blue Pawn con burbuja de oferta ([pocion] 1 [moneda] + hint E).
        static void BuildVendor(Vector2 position, Transform parent)
        {
            var idleSprite = LoadFirstSprite(TS + "Pawn and Resources/Pawn/Blue Pawn/Pawn_Idle.png");

            var root = new GameObject("Vendor_BluePawn");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.28f;
            col.offset = new Vector2(0f, 0.3f);

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);
            spriteGo.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = idleSprite;
            var animator = spriteGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = pawnController;

            var sorter = root.AddComponent<YSorter>();
            sorter.renderers = new[] { sr };
            sorter.isStatic = true;

            // --- Burbuja de oferta ---
            int order = 31000;
            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(bubble.transform, false);
            bg.transform.localScale = new Vector3(13.5f, 5.8f, 1f); // sprite blanco de 8px -> ~1.7 x 0.72 u
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = GetWhiteSprite();
            bgSr.color = new Color(0.16f, 0.12f, 0.09f, 0.88f);
            bgSr.sortingOrder = order;

            var potionGo = new GameObject("PotionIcon");
            potionGo.transform.SetParent(bubble.transform, false);
            potionGo.transform.localPosition = new Vector3(-0.5f, 0.02f, 0f);
            potionGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            var potionSr = potionGo.AddComponent<SpriteRenderer>();
            potionSr.sprite = potionIconSprite;
            potionSr.sortingOrder = order + 2;

            var priceText = MakeWorldText(bubble.transform, "1", new Vector3(0.08f, 0f, 0f),
                0.058f, order + 2, Color.white);

            var coinGo = new GameObject("CoinIcon");
            coinGo.transform.SetParent(bubble.transform, false);
            coinGo.transform.localPosition = new Vector3(0.48f, 0.02f, 0f);
            coinGo.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            var coinSr = coinGo.AddComponent<SpriteRenderer>();
            coinSr.sprite = coinIconSprite;
            coinSr.sortingOrder = order + 2;

            MakeWorldText(bubble.transform, "[E] comprar", new Vector3(0f, -0.55f, 0f),
                0.042f, order + 2, new Color(1f, 0.95f, 0.75f, 1f));

            var vendor = root.AddComponent<VendorNpc>();
            vendor.itemSold = ItemType.HealthPotion;
            vendor.priceInCoins = 1;
            vendor.bubble = bubble;
            vendor.coinIconRenderer = coinSr;
            vendor.spriteRenderer = sr;
        }

        static TextMesh MakeWorldText(Transform parent, string content, Vector3 localPos,
            float characterSize, int sortingOrder, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = content;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = sortingOrder;
            return tm;
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

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/Prefabs/Sheep.prefab");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =================================================================
        //  POBLAR EL MUNDO
        // =================================================================

        class WorldRefs
        {
            public GameObject playerInstance;
        }

        static WorldRefs PopulateWorld(MapData map, GameObject playerPrefab,
            Dictionary<string, GameObject> enemyPrefabs, GameObject sheepPrefab)
        {
            var refs = new WorldRefs();
            var decorParent = new GameObject("Decor").transform;
            var unitsParent = new GameObject("Units").transform;

            var occupied = new HashSet<Vector2Int>();
            foreach (var cell in map.stairWalkable)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        occupied.Add(new Vector2Int(cell.x + dx, cell.y + dy));

            // ---------- Aldea del jugador ----------
            var spawnCell = map.FindWalkableNear(PlayerSpawnHint.x, PlayerSpawnHint.y);
            Vector2 spawn = CellCenter(spawnCell);
            MarkArea(occupied, spawnCell, 3);

            // Aserto de regresion: ninguna celda transitable puede quedar aislada.
            MapGenerator.ValidateConnectivity(map, spawnCell);

            // Solo se puede spawnear unidades en celdas alcanzables desde el jugador,
            // para que la victoria (matar a todos) sea siempre posible.
            var reachable = map.ComputeReachable(spawnCell);

            PlaceBuilding(map, occupied, decorParent, TS + "Buildings/Blue Buildings/Castle.png",
                spawnCell + new Vector2Int(0, 4), 3, 2);
            PlaceBuilding(map, occupied, decorParent, TS + "Buildings/Blue Buildings/House1.png",
                spawnCell + new Vector2Int(-6, 2), 2, 1);
            PlaceBuilding(map, occupied, decorParent, TS + "Buildings/Blue Buildings/House2.png",
                spawnCell + new Vector2Int(6, 3), 2, 1);

            refs.playerInstance = Spawn(playerPrefab, spawn, unitsParent);

            // Vendedor neutral (Blue Pawn) frente al castillo.
            BuildVendor(CellCenter(spawnCell + new Vector2Int(1, 2)), unitsParent);
            MarkArea(occupied, spawnCell + new Vector2Int(1, 2), 1);

            foreach (var offset in new[] { new Vector2Int(-3, -2), new Vector2Int(2, -3),
                new Vector2Int(5, 0), new Vector2Int(-2, 2) })
            {
                if (map.TryFindReachableNear(reachable, spawnCell.x + offset.x, spawnCell.y + offset.y, 4,
                    out var cell))
                    Spawn(sheepPrefab, CellCenter(cell), unitsParent);
            }

            // ---------- Campamentos enemigos ----------
            PlaceCamp(map, reachable, occupied, decorParent, unitsParent, enemyPrefabs,
                towerPath: TS + "Buildings/Red Buildings/Tower.png", towerCell: new Vector2Int(52, 48),
                color: "Red", spots: new[] { new Vector2Int(48, 45), new Vector2Int(56, 46), new Vector2Int(51, 44), new Vector2Int(55, 49) });

            PlaceCamp(map, reachable, occupied, decorParent, unitsParent, enemyPrefabs,
                towerPath: TS + "Buildings/Purple Buildings/Tower.png", towerCell: new Vector2Int(22, 32),
                color: "Purple", spots: new[] { new Vector2Int(19, 29), new Vector2Int(25, 30), new Vector2Int(22, 28) });

            PlaceCamp(map, reachable, occupied, decorParent, unitsParent, enemyPrefabs,
                towerPath: TS + "Buildings/Yellow Buildings/Tower.png", towerCell: new Vector2Int(86, 55),
                color: "Yellow", spots: new[] { new Vector2Int(83, 52), new Vector2Int(88, 53), new Vector2Int(86, 51) });

            PlaceCamp(map, reachable, occupied, decorParent, unitsParent, enemyPrefabs,
                towerPath: TS + "Buildings/Red Buildings/Barracks.png", towerCell: new Vector2Int(66, 16),
                color: "Red", spots: new[] { new Vector2Int(63, 13), new Vector2Int(69, 14) });

            // Merodeadores sueltos entre la aldea y los campamentos.
            foreach (var cell in new[] { new Vector2Int(34, 28), new Vector2Int(58, 30) })
            {
                if (!map.TryFindReachableNear(reachable, cell.x, cell.y, 12, out var c))
                {
                    Debug.LogWarning($"[SceneBuilder] Merodeador en {cell} sin celda alcanzable; omitido.");
                    continue;
                }
                Spawn(enemyPrefabs["Red"], CellCenter(c), unitsParent);
                MarkArea(occupied, c, 1);
            }

            // ---------- Decoracion dispersa ----------
            ScatterDecor(map, occupied, decorParent, spawnCell);
            PlaceWaterExtras(map, decorParent);
            PlaceClouds(decorParent);

            return refs;
        }

        static void PlaceCamp(MapData map, bool[,] reachable, HashSet<Vector2Int> occupied,
            Transform decorParent, Transform unitsParent, Dictionary<string, GameObject> enemyPrefabs,
            string towerPath, Vector2Int towerCell, string color, Vector2Int[] spots)
        {
            var tc = map.FindWalkableNear(towerCell.x, towerCell.y);
            PlaceBuilding(map, occupied, decorParent, towerPath, tc, 1, 1);
            foreach (var s in spots)
            {
                if (!map.TryFindReachableNear(reachable, s.x, s.y, 10, out var cell))
                {
                    Debug.LogWarning($"[SceneBuilder] Enemigo {color} en {s} sin celda alcanzable; omitido.");
                    continue;
                }
                if (occupied.Contains(cell))
                    map.TryFindReachableNear(reachable, s.x + 1, s.y + 1, 10, out cell);
                Spawn(enemyPrefabs[color], CellCenter(cell), unitsParent);
                occupied.Add(cell);
            }
        }

        static void ScatterDecor(MapData map, HashSet<Vector2Int> occupied, Transform parent,
            Vector2Int playerSpawn)
        {
            string[] trees = { "Tree1.png", "Tree2.png", "Tree3.png", "Tree4.png" };
            string[] stumps = { "Stump 1.png", "Stump 2.png", "Stump 3.png", "Stump 4.png" };
            string[] bushes = { "Bush 1.png", "Bush 2.png", "Bush 3.png", "Bush 4.png" };
            string[] rocks = { "Rock1.png", "Rock2.png", "Rock3.png", "Rock4.png" };

            // Arboles: repartidos con ruido de densidad, lejos de la aldea.
            ScatterKind(map, occupied, parent, 52, minSpawnDist: 8, playerSpawn,
                i => TS + "Pawn and Resources/Wood/Trees/" + trees[i % trees.Length],
                feetInset: 0.45f, colliderRadius: 0.35f);

            ScatterKind(map, occupied, parent, 9, minSpawnDist: 6, playerSpawn,
                i => TS + "Pawn and Resources/Wood/Trees/" + stumps[i % stumps.Length],
                feetInset: 0.12f, colliderRadius: 0.22f);

            ScatterKind(map, occupied, parent, 22, minSpawnDist: 4, playerSpawn,
                i => TS + "Terrain/Decorations/Bushes/" + bushes[i % bushes.Length],
                feetInset: 0.12f, colliderRadius: 0f);

            ScatterKind(map, occupied, parent, 12, minSpawnDist: 5, playerSpawn,
                i => TS + "Terrain/Decorations/Rocks/" + rocks[i % rocks.Length],
                feetInset: 0.1f, colliderRadius: 0.25f);

            // Vetas de oro agrupadas en el humedal del este.
            for (int i = 0; i < 8; i++)
            {
                int x = 74 + rng.Next(-6, 9);
                int y = 33 + rng.Next(-5, 6);
                var cell = map.FindWalkableNear(x, y, 5);
                if (occupied.Contains(cell)) continue;
                occupied.Add(cell);
                string path = TS + $"Pawn and Resources/Gold/Gold Stones/Gold Stone {1 + (i % 6)}.png";
                CreateDecorSprite(path, CellCenter(cell), parent, 0.1f, 0.28f);
            }
        }

        static void ScatterKind(MapData map, HashSet<Vector2Int> occupied, Transform parent,
            int count, int minSpawnDist, Vector2Int playerSpawn,
            Func<int, string> pathFor, float feetInset, float colliderRadius)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                int x = rng.Next(2, MapData.W - 2);
                int y = rng.Next(2, MapData.H - 2);
                var cell = new Vector2Int(x, y);
                if (!map.Walkable(x, y) || occupied.Contains(cell)) continue;
                if (Vector2Int.Distance(cell, playerSpawn) < minSpawnDist) continue;
                // Ruido de densidad para que se agrupen de forma organica.
                if (Mathf.PerlinNoise(x * 0.15f + 7.3f, y * 0.15f + 2.9f) < 0.42f) continue;

                occupied.Add(cell);
                Vector2 pos = CellCenter(cell) + new Vector2(
                    (float)(rng.NextDouble() - 0.5) * 0.5f, (float)(rng.NextDouble() - 0.5) * 0.4f);
                CreateDecorSprite(pathFor(placed), pos, parent, feetInset, colliderRadius);
                placed++;
            }
        }

        static void PlaceWaterExtras(MapData map, Transform parent)
        {
            string[] waterRocks = { "Water Rocks_01.png", "Water Rocks_02.png", "Water Rocks_03.png", "Water Rocks_04.png" };
            int placed = 0;
            for (int attempt = 0; attempt < 300 && placed < 10; attempt++)
            {
                int x = rng.Next(3, MapData.W - 3);
                int y = rng.Next(3, MapData.H - 3);
                bool nearLand = false;
                for (int dx = -2; dx <= 2 && !nearLand; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        if (map.Land(x + dx, y + dy)) { nearLand = true; break; }
                if (map.land[x, y] || nearLand) continue;

                var sprite = LoadFirstSprite(TS + "Terrain/Decorations/Rocks in the Water/" + waterRocks[placed % waterRocks.Length]);
                var go = new GameObject("WaterRock");
                go.transform.SetParent(parent, false);
                go.transform.position = CellCenter(new Vector2Int(x, y));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = -94;
                placed++;

                if (placed == 5)
                {
                    var duck = new GameObject("RubberDuck");
                    duck.transform.SetParent(parent, false);
                    duck.transform.position = CellCenter(new Vector2Int(x, y)) + new Vector2(1.2f, 0.6f);
                    var duckSr = duck.AddComponent<SpriteRenderer>();
                    duckSr.sprite = LoadFirstSprite(TS + "Terrain/Decorations/Rubber Duck/Rubber duck.png");
                    duckSr.sortingOrder = -93;
                }
            }
        }

        static void PlaceClouds(Transform parent)
        {
            for (int i = 0; i < 6; i++)
            {
                string path = TS + $"Terrain/Decorations/Clouds/Clouds_0{1 + (i % 8)}.png";
                var go = new GameObject("Cloud" + i);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(rng.Next(0, MapData.W), 8 + i * 9 + rng.Next(0, 5), 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LoadFirstSprite(path);
                sr.color = new Color(1f, 1f, 1f, 0.8f);
                // Ojo: sortingOrder se almacena como Int16; valores altos se desbordan.
                sr.sortingOrder = 32000;
                var drift = go.AddComponent<CloudDrift>();
                drift.speed = 0.25f + (float)rng.NextDouble() * 0.5f;
            }
        }

        static void PlaceBuilding(MapData map, HashSet<Vector2Int> occupied, Transform parent,
            string spritePath, Vector2Int cell, int halfWidth, int clearRadiusY)
        {
            var feet = map.FindWalkableNear(cell.x, cell.y);
            var go = CreateDecorSprite(spritePath, CellCenter(feet), parent, 0.15f, 0f);
            if (go == null) return;

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                float w = sr.sprite.bounds.size.x;
                box.size = new Vector2(Mathf.Max(1f, w * 0.6f), 1.3f);
                box.offset = new Vector2(0f, 0.65f);
            }
            for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
                for (int dy = -clearRadiusY; dy <= clearRadiusY + 2; dy++)
                    occupied.Add(new Vector2Int(feet.x + dx, feet.y + dy));
        }

        static GameObject CreateDecorSprite(string spritePath, Vector2 feetPos, Transform parent,
            float feetInset, float colliderRadius)
        {
            var sprite = LoadFirstSprite(spritePath);
            if (sprite == null) return null;

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(spritePath));
            root.transform.SetParent(parent, false);
            root.transform.position = feetPos;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);
            spriteGo.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y - feetInset, 0f);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            var sorter = root.AddComponent<YSorter>();
            sorter.renderers = new[] { sr };
            sorter.isStatic = true;

            if (colliderRadius > 0f)
            {
                var col = root.AddComponent<CircleCollider2D>();
                col.radius = colliderRadius;
                col.offset = new Vector2(0f, colliderRadius * 0.5f);
            }
            return root;
        }

        static GameObject Spawn(GameObject prefab, Vector2 position, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        static Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        static void MarkArea(HashSet<Vector2Int> occupied, Vector2Int center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    occupied.Add(new Vector2Int(center.x + dx, center.y + dy));
        }

        // =================================================================
        //  CAMARA, LUZ, HUD Y GESTORES
        // =================================================================

        static void BuildCameraAndLight(GameObject player)
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
            follow.target = player != null ? player.transform : null;
            follow.boundsMin = new Vector2(0f, 0f);
            follow.boundsMax = new Vector2(MapData.W, MapData.H);
            if (player != null)
                camGo.transform.position = new Vector3(player.transform.position.x,
                    player.transform.position.y, -10f);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            // Iconos de objetos para el inventario, dropeos y vendedor.
            var itemsGo = new GameObject("ItemLibrary");
            var itemLib = itemsGo.AddComponent<ItemLibrary>();
            itemLib.coinIcon = coinIconSprite;
            itemLib.potionIcon = potionIconSprite;

            // Material para los VFX de los ataques.
            var vfxGo = new GameObject("VfxLibrary");
            var lib = vfxGo.AddComponent<VfxLibrary>();
            lib.vfxMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (lib.vfxMaterial == null)
                lib.vfxMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        }

        static void BuildHudAndManagers()
        {
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
            // Geometria medida sobre BigBar_Base.png (192x64): marco en X 40-151.
            // El fill del pack (BigBar_Fill) trae su franja roja en Y 20-43 con padding
            // transparente, asi que con rect a altura completa la franja cae centrada.
            hud.healthFill = BuildHudBar(canvasGo.transform, "Health", baseSprite, fillSprite,
                new Vector2(30f, 150f), new Vector2(384f, 128f), Color.white,
                new Vector2(0.25f, 0f), new Vector2(0.75f, 1f));
            hud.energyFill = BuildHudBar(canvasGo.transform, "Energy", baseSprite, GetWhiteSprite(),
                new Vector2(30f, 60f), new Vector2(307f, 102f), new Color(1f, 0.82f, 0.25f, 1f),
                new Vector2(0.25f, 0.3125f), new Vector2(0.75f, 0.6875f));

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Texto de controles.
            var controls = MakeText(canvasGo.transform, "Controls", font, 22,
                "WASD mover  |  Shift dash  |  Click Izq. barrido  |  Click Der. estocada  |  Espacio parry  |  1-4 objetos  |  E comprar");
            var controlsRt = controls.rectTransform;
            controlsRt.anchorMin = new Vector2(0.5f, 1f);
            controlsRt.anchorMax = new Vector2(0.5f, 1f);
            controlsRt.pivot = new Vector2(0.5f, 1f);
            controlsRt.anchoredPosition = new Vector2(0f, -16f);
            controlsRt.sizeDelta = new Vector2(1400f, 40f);
            controls.alignment = TextAnchor.UpperCenter;
            controls.color = new Color(1f, 1f, 1f, 0.85f);

            // Mensaje central (muerte / victoria).
            var message = MakeText(canvasGo.transform, "Message", font, 52, "");
            var messageRt = message.rectTransform;
            messageRt.anchorMin = new Vector2(0.5f, 0.5f);
            messageRt.anchorMax = new Vector2(0.5f, 0.5f);
            messageRt.pivot = new Vector2(0.5f, 0.5f);
            messageRt.anchoredPosition = Vector2.zero;
            messageRt.sizeDelta = new Vector2(1200f, 400f);
            message.alignment = TextAnchor.MiddleCenter;
            message.color = new Color(1f, 0.95f, 0.8f, 1f);

            BuildInventoryHud(canvasGo.transform, font);

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            manager.messageText = message;
        }

        /// Barra de inventario: 4 slots en la parte inferior central (teclas 1-4).
        static void BuildInventoryHud(Transform canvas, Font font)
        {
            var hudGo = new GameObject("InventoryHud");
            hudGo.transform.SetParent(canvas, false);
            // El contenedor necesita un RectTransform estirado a todo el canvas;
            // sin el, las anclas de los slots se resuelven contra el centro de la
            // pantalla y la barra aparece en medio en vez de abajo.
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

    /// Punto de entrada para la verificacion visual automatizada:
    /// abre la escena, enfoca la Game View y entra en modo Play.
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

    /// Expone la ruta de la escena sin hacer publica toda la clase del builder.
    public static class SceneBuilder2
    {
        public const string ScenePathPublic = "Assets/Game/Scenes/Game.unity";
    }
}
