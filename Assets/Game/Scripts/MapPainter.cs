using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyRpg
{
    /// Pinta un MapBuildData sobre los tilemaps de la escena y crea la
    /// decoracion. Todo el contenido volatil cuelga de un contenedor que se
    /// destruye al cambiar de mapa.
    public static class MapPainter
    {
        public const int WaterMargin = 12;

        /// Contenedor del contenido del mapa actual (decoracion, NPCs, salidas...).
        public static Transform CreateContentRoot()
        {
            var go = new GameObject("MapContent");
            return go.transform;
        }

        public static void Paint(MapBuildData data, Transform contentRoot)
        {
            var lib = MapLibrary.Instance;
            int w = data.W, h = data.H;

            AutoTiles.CleanupMask(data.land);

            lib.waterLayer.ClearAllTiles();
            lib.foamLayer.ClearAllTiles();
            lib.groundLayer.ClearAllTiles();
            lib.detailLayer.ClearAllTiles();
            lib.collisionLayer.ClearAllTiles();

            // Fondo de agua con margen.
            var waterBounds = new BoundsInt(-WaterMargin, -WaterMargin, 0,
                w + WaterMargin * 2, h + WaterMargin * 2, 1);
            var waterTiles = new TileBase[waterBounds.size.x * waterBounds.size.y];
            for (int i = 0; i < waterTiles.Length; i++) waterTiles[i] = lib.waterBgTile;
            lib.waterLayer.SetTilesBlock(waterBounds, waterTiles);

            var baseTiles = lib.GetColor(data.baseColor);

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var cell = new Vector3Int(x, y, 0);

                    if (data.land[x, y])
                    {
                        lib.groundLayer.SetTile(cell,
                            baseTiles[AutoTiles.FlatByMask[AutoTiles.MaskOf(data.land, x, y)]]);

                        // Espuma si toca agua en 8 direcciones.
                        bool touchesWater = false;
                        for (int dx = -1; dx <= 1 && !touchesWater; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (!AutoTiles.Get(data.land, x + dx, y + dy)) { touchesWater = true; break; }
                        if (touchesWater) lib.foamLayer.SetTile(cell, lib.foamTile);
                    }

                    // Colision: agua cercana a tierra, celdas bloqueadas y el
                    // anillo exterior del mapa (nunca se sale del recinto).
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    if (border || data.blocked[x, y] || !data.land[x, y])
                        lib.collisionLayer.SetTile(cell, lib.collisionTile);
                }

            // Parches de bioma con su propio autotile.
            foreach (int color in new[] { 1, 3, 4, 5 })
            {
                var mask = new bool[w, h];
                bool any = false;
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (data.patch[x, y] == color && data.land[x, y]) { mask[x, y] = true; any = true; }
                if (!any) continue;
                AutoTiles.CleanupMask(mask);
                var tiles = lib.GetColor(color);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (mask[x, y])
                            lib.detailLayer.SetTile(new Vector3Int(x, y, 0),
                                tiles[AutoTiles.FlatByMask[AutoTiles.MaskOf(mask, x, y)]]);
            }

            foreach (var d in data.decor)
                CreateDecor(d, contentRoot);
        }

        static void CreateDecor(DecorSpec spec, Transform parent)
        {
            var lib = MapLibrary.Instance;
            Sprite sprite = null;
            float feetInset = 0.15f;
            float colliderRadius = 0f;

            switch (spec.kind)
            {
                case DecorKind.Tree:
                    sprite = Pick(lib.treeSprites, spec.variant);
                    feetInset = 0.45f; colliderRadius = spec.blocking ? 0.35f : 0f;
                    break;
                case DecorKind.Bush:
                    sprite = Pick(lib.bushSprites, spec.variant); feetInset = 0.12f;
                    break;
                case DecorKind.Rock:
                    sprite = Pick(lib.rockSprites, spec.variant);
                    feetInset = 0.1f; colliderRadius = spec.blocking ? 0.28f : 0f;
                    break;
                case DecorKind.Stump:
                    sprite = Pick(lib.stumpSprites, spec.variant);
                    feetInset = 0.12f; colliderRadius = spec.blocking ? 0.22f : 0f;
                    break;
                case DecorKind.Gold:
                    sprite = Pick(lib.goldSprites, spec.variant);
                    feetInset = 0.1f; colliderRadius = spec.blocking ? 0.3f : 0f;
                    break;
                case DecorKind.House: sprite = lib.houseSprite; feetInset = 0.15f; break;
                case DecorKind.House2: sprite = lib.house2Sprite; feetInset = 0.15f; break;
                case DecorKind.Tower: sprite = lib.towerSprite; feetInset = 0.15f; break;
                case DecorKind.WoodTable: sprite = lib.woodTableSprite; feetInset = 0.1f; break;
            }
            if (sprite == null) return;

            var root = new GameObject(spec.kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.position = spec.pos;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);
            spriteGo.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y - feetInset, 0f);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            var sorter = root.AddComponent<YSorter>();
            sorter.renderers = new[] { sr };
            sorter.isStatic = true;

            bool isBuilding = spec.kind == DecorKind.House || spec.kind == DecorKind.House2
                || spec.kind == DecorKind.Tower;
            if (isBuilding)
            {
                var box = root.AddComponent<BoxCollider2D>();
                box.size = new Vector2(Mathf.Max(1f, sprite.bounds.size.x * 0.6f), 1.2f);
                box.offset = new Vector2(0f, 0.6f);
            }
            else if (colliderRadius > 0f)
            {
                var col = root.AddComponent<CircleCollider2D>();
                col.radius = colliderRadius;
                col.offset = new Vector2(0f, colliderRadius * 0.5f);
            }
        }

        static Sprite Pick(Sprite[] options, int variant)
        {
            if (options == null || options.Length == 0) return null;
            return options[Mathf.Abs(variant) % options.Length];
        }
    }
}
