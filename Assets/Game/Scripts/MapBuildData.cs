using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg
{
    public enum DecorKind { Tree, Bush, Rock, Stump, Gold, House, House2, Tower, WoodTable }

    public enum SpecialKind
    {
        Miner,      // pawn picando una roca de oro
        Chopper,    // pawn talando un arbol
        Walker,     // pawn paseando
        Sheep,
        Campfire,   // fogata curativa
        Vendor,     // mercader de la parada de descanso
        Treasure,   // el tesoro del bosque (victoria)
    }

    public struct DecorSpec
    {
        public Vector2 pos;
        public DecorKind kind;
        public int variant;
        public bool blocking;
    }

    public struct SpecialSpec
    {
        public Vector2 pos;
        public SpecialKind kind;
    }

    /// Datos de un mapa construible en runtime. Las mascaras usan celdas de 1
    /// unidad; (0,0) es la esquina inferior-izquierda.
    public class MapBuildData
    {
        public int W;
        public int H;
        public int baseColor = 2;    // hoja del suelo base
        public bool[,] land;         // false = agua
        public int[,] patch;         // 0 = sin parche; si no, color de la hoja
        public bool[,] blocked;      // colision extra (muro de arboles, etc.)

        public Vector2 playerSpawn;
        public Vector2 exitPos;
        public string exitLabel = "Salida";

        public List<DecorSpec> decor = new List<DecorSpec>();
        public List<SpecialSpec> specials = new List<SpecialSpec>();
        public List<Vector2> enemySpawns = new List<Vector2>();

        public System.Random rng;

        public MapBuildData(int w, int h, int seed)
        {
            W = w; H = h;
            land = new bool[w, h];
            patch = new int[w, h];
            blocked = new bool[w, h];
            rng = new System.Random(seed);
        }

        // ------------------- helpers de mascara -------------------

        public void FillLand()
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    land[x, y] = true;
        }

        public void Ellipse(bool[,] mask, float cx, float cy, float rx, float ry, bool value,
            float wobble = 0f)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float nx = (x - cx) / rx, ny = (y - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (wobble > 0f)
                        d += (Mathf.PerlinNoise(x * 0.23f + cx, y * 0.23f + cy) - 0.5f) * wobble;
                    if (d <= 1f) mask[x, y] = value;
                }
        }

        public void Strip(bool[,] mask, Vector2 a, Vector2 b, float halfWidth, bool value)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    Vector2 p = new Vector2(x, y);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                    if (Vector2.Distance(p, a + ab * t) <= halfWidth) mask[x, y] = value;
                }
        }

        public void PatchEllipse(float cx, float cy, float rx, float ry, int color)
        {
            var mask = new bool[W, H];
            Ellipse(mask, cx, cy, rx, ry, true, 0.3f);
            AutoTiles.CleanupMask(mask);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (mask[x, y] && land[x, y]) patch[x, y] = color;
        }

        // ------------------- helpers de decoracion -------------------

        public void AddDecor(DecorKind kind, float x, float y, bool blocking = true, int variant = -1)
        {
            decor.Add(new DecorSpec
            {
                pos = new Vector2(x, y),
                kind = kind,
                variant = variant >= 0 ? variant : rng.Next(0, 4),
                blocking = blocking,
            });
        }

        public void AddSpecial(SpecialKind kind, float x, float y)
        {
            specials.Add(new SpecialSpec { pos = new Vector2(x, y), kind = kind });
        }

        /// Muralla de arboles alrededor del mapa (el bosque cerrado), con un hueco
        /// de 'gapWidth' celdas centrado en 'gapCenter' (posicion de la salida).
        public void TreeWall(int thickness, Vector2 gapCenter, float gapWidth)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                    if (edge >= thickness) continue;
                    if (Vector2.Distance(new Vector2(x, y), gapCenter) <= gapWidth) continue;

                    blocked[x, y] = true;
                    // Arboles con densidad decreciente hacia el interior.
                    float density = edge == 0 ? 0.55f : (edge == 1 ? 0.4f : 0.25f);
                    if (rng.NextDouble() < density && land[x, y])
                        AddDecor(DecorKind.Tree,
                            x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.6f,
                            y + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.6f,
                            blocking: false); // la colision la pone 'blocked'
                }
        }

        /// Dispersa decoracion en celdas de tierra libres dentro de un rectangulo.
        public void Scatter(DecorKind kind, int count, float x0, float y0, float x1, float y1,
            bool blocking = true, float keepClearOf = 3.5f)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 25 && placed < count; attempt++)
            {
                float x = Mathf.Lerp(x0, x1, (float)rng.NextDouble());
                float y = Mathf.Lerp(y0, y1, (float)rng.NextDouble());
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                if (cx < 0 || cx >= W || cy < 0 || cy >= H) continue;
                if (!land[cx, cy] || blocked[cx, cy]) continue;
                if (Vector2.Distance(new Vector2(x, y), playerSpawn) < keepClearOf) continue;
                if (Vector2.Distance(new Vector2(x, y), exitPos) < keepClearOf) continue;
                AddDecor(kind, x, y, blocking);
                placed++;
            }
        }

        /// Puntos candidatos de aparicion de enemigos repartidos por el interior.
        public void FillEnemySpawns(int count, float margin = 5f)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
            {
                float x = Mathf.Lerp(margin, W - margin, (float)rng.NextDouble());
                float y = Mathf.Lerp(margin, H - margin, (float)rng.NextDouble());
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                if (!land[cx, cy] || blocked[cx, cy]) continue;
                if (Vector2.Distance(new Vector2(x, y), playerSpawn) < 7f) continue;
                enemySpawns.Add(new Vector2(x, y));
                placed++;
            }
        }
    }
}
