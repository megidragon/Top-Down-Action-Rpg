using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg.EditorTools
{
    /// Genera el mapa de la isla de forma determinista (semilla fija):
    /// mascara de tierra, parches de bioma, mesetas elevadas con acantilados,
    /// escaleras, espuma costera y rejilla de celdas transitables.
    public class MapData
    {
        public const int W = 96;
        public const int H = 64;

        public bool[,] land = new bool[W, H];
        public bool[,] elev1 = new bool[W, H];
        public bool[,] elev2 = new bool[W, H];
        public int[,] detailPatch = new int[W, H]; // 0 = sin parche, si no: indice de color (3,4,5)
        public bool[,] cliff1 = new bool[W, H];
        public bool[,] cliff2 = new bool[W, H];
        public bool[,] foam = new bool[W, H];
        public bool[,] walkable = new bool[W, H];

        // Celdas de escalera: posicion -> indice de tile (32,33,38,39)
        public Dictionary<Vector2Int, int> stairTiles1 = new Dictionary<Vector2Int, int>();
        public Dictionary<Vector2Int, int> stairTiles2 = new Dictionary<Vector2Int, int>();
        public HashSet<Vector2Int> stairWalkable = new HashSet<Vector2Int>();

        public bool InBounds(int x, int y) => x >= 0 && x < W && y >= 0 && y < H;
        public bool Land(int x, int y) => InBounds(x, y) && land[x, y];
        public bool Elev1(int x, int y) => InBounds(x, y) && elev1[x, y];
        public bool Elev2(int x, int y) => InBounds(x, y) && elev2[x, y];
        public bool Walkable(int x, int y) => InBounds(x, y) && walkable[x, y];

        public Vector2Int FindWalkableNear(int cx, int cy, int maxRadius = 12)
        {
            for (int r = 0; r <= maxRadius; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (Walkable(x, y)) return new Vector2Int(x, y);
                    }
            return new Vector2Int(cx, cy);
        }

        /// BFS de celdas transitables (4 direcciones) desde un origen.
        public bool[,] ComputeReachable(Vector2Int start)
        {
            var reachable = new bool[W, H];
            if (!Walkable(start.x, start.y)) return reachable;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            reachable[start.x, start.y] = true;
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in dirs)
                {
                    int nx = c.x + d.x, ny = c.y + d.y;
                    if (!Walkable(nx, ny) || reachable[nx, ny]) continue;
                    reachable[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return reachable;
        }

        /// Celda transitable Y alcanzable desde el spawn mas cercana al punto pedido.
        public bool TryFindReachableNear(bool[,] reachable, int cx, int cy, int maxRadius,
            out Vector2Int result)
        {
            for (int r = 0; r <= maxRadius; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (InBounds(x, y) && reachable[x, y]) { result = new Vector2Int(x, y); return true; }
                    }
            result = new Vector2Int(cx, cy);
            return false;
        }
    }

    public static class MapGenerator
    {
        const float NoiseScale = 0.09f;
        const float NoiseAmp = 0.17f;
        static Vector2 noiseOffset;

        public static MapData Generate()
        {
            var map = new MapData();
            noiseOffset = new Vector2(137.31f, 942.17f); // semilla fija del ruido

            int W = MapData.W, H = MapData.H;

            // ---------- Mascara de tierra ----------
            var land = map.land;
            AddEllipse(land, 46f, 32f, 38f, 25f, true);    // isla principal
            AddEllipse(land, 86f, 53f, 8f, 6.5f, true);    // isla noreste
            AddEllipse(land, 12f, 11f, 8f, 6f, true);      // isla suroeste
            AddStrip(land, 76, 47, 85, 52, 1.7f);          // istmo NE
            AddStrip(land, 18, 14, 27, 20, 1.7f);          // istmo SO
            AddEllipse(land, 60f, 27f, 6f, 4.5f, false);   // lago interior
            AddEllipse(land, 38f, 5f, 10f, 5f, false);     // bahia sur
            AddEllipse(land, 26f, 41f, 3f, 2.6f, false);   // estanque

            // Borde del mapa siempre agua.
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) land[x, y] = false;

            CleanupMask(land);

            // ---------- Mesetas elevadas (nivel 1, color 2) ----------
            var elev1 = map.elev1;
            AddEllipse(elev1, 48f, 44f, 17f, 8.5f, true);  // meseta norte
            AddEllipse(elev1, 22f, 30f, 8f, 5.5f, true);   // meseta oeste
            AddEllipse(elev1, 86f, 54f, 4.5f, 3.2f, true); // cima isla NE
            Intersect(elev1, land);
            CleanupMask(elev1);
            // La fila inferior de una meseta necesita sitio para el acantilado:
            // no puede empezar en el borde inferior del mapa.
            for (int x = 0; x < W; x++) { elev1[x, 0] = false; elev1[x, 1] = false; }
            CleanupMask(elev1);

            // ---------- Segundo nivel (color 3) sobre la meseta norte ----------
            var elev2 = map.elev2;
            AddEllipse(elev2, 52f, 46.5f, 8f, 4.2f, true);
            Intersect(elev2, Erode(elev1, 2));
            CleanupMask(elev2);

            // ---------- Parches de bioma sobre el suelo llano ----------
            FillPatch(map, 66f, 14f, 10f, 6f, 4);   // zona arida (color 4)
            FillPatch(map, 78f, 34f, 8f, 5.5f, 5);  // humedal (color 5)
            FillPatch(map, 14f, 44f, 6.5f, 4.5f, 3); // pradera brillante (color 3)

            // ---------- Acantilados ----------
            ComputeCliffs(map.elev1, map.cliff1);
            ComputeCliffs(map.elev2, map.cliff2);

            // ---------- Escaleras ----------
            PlaceStairs(map, map.elev1, map.cliff1, map.stairTiles1, isTier2: false);
            PlaceStairs(map, map.elev2, map.cliff2, map.stairTiles2, isTier2: true);

            // ---------- Espuma costera ----------
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (land[x, y])
                    {
                        for (int dx = -1; dx <= 1 && !map.foam[x, y]; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (!map.Land(x + dx, y + dy)) { map.foam[x, y] = true; break; }
                    }
                    else if (map.cliff1[x, y] || map.cliff2[x, y])
                    {
                        map.foam[x, y] = true; // acantilado que cae al agua
                    }
                }

            ComputeWalkable(map);
            return map;
        }

        // ------------------------------------------------------------------
        static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(noiseOffset.x + x * NoiseScale, noiseOffset.y + y * NoiseScale) - 0.5f;
        }

        static void AddEllipse(bool[,] mask, float cx, float cy, float rx, float ry, bool add)
        {
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny) + Noise(x, y) * NoiseAmp * 2f;
                    if (d <= 1f) mask[x, y] = add;
                }
        }

        static void AddStrip(bool[,] mask, int x0, int y0, int x1, int y1, float halfWidth)
        {
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    Vector2 p = new Vector2(x, y);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    if (Vector2.Distance(p, a + ab * t) <= halfWidth) mask[x, y] = true;
                }
        }

        static void Intersect(bool[,] mask, bool[,] other)
        {
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                    mask[x, y] = mask[x, y] && other[x, y];
        }

        static bool[,] Erode(bool[,] mask, int steps)
        {
            var result = (bool[,])mask.Clone();
            for (int s = 0; s < steps; s++)
            {
                var next = (bool[,])result.Clone();
                for (int x = 0; x < MapData.W; x++)
                    for (int y = 0; y < MapData.H; y++)
                    {
                        if (!result[x, y]) continue;
                        if (!Get(result, x - 1, y) || !Get(result, x + 1, y) ||
                            !Get(result, x, y - 1) || !Get(result, x, y + 1))
                            next[x, y] = false;
                    }
                result = next;
            }
            return result;
        }

        static bool Get(bool[,] mask, int x, int y)
        {
            return x >= 0 && x < MapData.W && y >= 0 && y < MapData.H && mask[x, y];
        }

        static int OrthoCount(bool[,] mask, int x, int y)
        {
            int n = 0;
            if (Get(mask, x - 1, y)) n++;
            if (Get(mask, x + 1, y)) n++;
            if (Get(mask, x, y - 1)) n++;
            if (Get(mask, x, y + 1)) n++;
            return n;
        }

        /// Limpia la mascara para que el autotile de 16 piezas funcione:
        /// sin picos de 1 celda, sin agujeros de alfiler y sin contactos solo diagonales.
        static void CleanupMask(bool[,] mask)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool changed = false;
                for (int x = 0; x < MapData.W; x++)
                    for (int y = 0; y < MapData.H; y++)
                    {
                        if (mask[x, y])
                        {
                            if (OrthoCount(mask, x, y) <= 1) { mask[x, y] = false; changed = true; }
                        }
                        else
                        {
                            if (OrthoCount(mask, x, y) >= 4) { mask[x, y] = true; changed = true; }
                        }
                    }

                // Contactos diagonales: dos celdas en diagonal sin vecino ortogonal comun.
                for (int x = 0; x < MapData.W - 1; x++)
                    for (int y = 0; y < MapData.H - 1; y++)
                    {
                        bool a = mask[x, y], b = mask[x + 1, y], c = mask[x, y + 1], d = mask[x + 1, y + 1];
                        if (a && d && !b && !c) { mask[x + 1, y] = true; changed = true; }
                        else if (b && c && !a && !d) { mask[x, y] = true; changed = true; }
                    }

                if (!changed) break;
            }
        }

        static void FillPatch(MapData map, float cx, float cy, float rx, float ry, int colorIndex)
        {
            var patch = new bool[MapData.W, MapData.H];
            AddEllipse(patch, cx, cy, rx, ry, true);
            Intersect(patch, map.land);
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                    if (map.elev1[x, y]) patch[x, y] = false;
            CleanupMask(patch);
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                    if (patch[x, y]) map.detailPatch[x, y] = colorIndex;
        }

        static void ComputeCliffs(bool[,] elev, bool[,] cliff)
        {
            for (int x = 0; x < MapData.W; x++)
                for (int y = 1; y < MapData.H; y++)
                    if (elev[x, y] && !elev[x, y - 1])
                        cliff[x, y - 1] = true;
        }

        /// Coloca escaleras de 2 celdas de ancho en los bordes sur de las mesetas.
        /// Garantiza al menos una escalera por meseta (componente conexa) para que
        /// ninguna zona con enemigos quede inaccesible.
        static void PlaceStairs(MapData map, bool[,] elev, bool[,] cliff,
            Dictionary<Vector2Int, int> stairTiles, bool isTier2)
        {
            int[,] comp = LabelComponents(elev, out int compCount);
            if (compCount == 0) return;

            // Tramos horizontales de acantilado aptos, etiquetados por meseta.
            var runs = new List<(int compId, int y, int x0, int x1)>();
            for (int y = 0; y < MapData.H - 1; y++)
            {
                int runStart = -1;
                int runComp = -1;
                for (int x = 0; x <= MapData.W; x++)
                {
                    bool ok = x < MapData.W && cliff[x, y] && CellOkForStair(map, x, y, isTier2);
                    int cellComp = ok ? comp[x, y + 1] : -1; // meseta a la que pertenece el acantilado
                    if (ok && runStart >= 0 && cellComp != runComp) ok = false; // cambio de meseta

                    if (ok && runStart < 0) { runStart = x; runComp = cellComp; }
                    else if (!ok && runStart >= 0)
                    {
                        if (x - runStart >= 2) runs.Add((runComp, y, runStart, x - 1));
                        runStart = -1;
                        runComp = -1;
                        // El tramo que corto por cambio de meseta empieza aqui.
                        if (x < MapData.W && cliff[x, y] && CellOkForStair(map, x, y, isTier2))
                        {
                            runStart = x;
                            runComp = comp[x, y + 1];
                        }
                    }
                }
            }

            var placed = new List<Vector2Int>();

            bool TryPlaceAt(int cx, int y, float minSeparation)
            {
                if (placed.Exists(p => Vector2Int.Distance(p, new Vector2Int(cx, y)) < minSeparation))
                    return false;
                if (!CellOkForStair(map, cx, y, isTier2) || !CellOkForStair(map, cx + 1, y, isTier2))
                    return false;
                stairTiles[new Vector2Int(cx, y + 1)] = 33;     // parte alta izquierda
                stairTiles[new Vector2Int(cx + 1, y + 1)] = 32; // parte alta derecha
                stairTiles[new Vector2Int(cx, y)] = 39;         // parte baja izquierda
                stairTiles[new Vector2Int(cx + 1, y)] = 38;     // parte baja derecha
                map.stairWalkable.Add(new Vector2Int(cx, y + 1));
                map.stairWalkable.Add(new Vector2Int(cx + 1, y + 1));
                map.stairWalkable.Add(new Vector2Int(cx, y));
                map.stairWalkable.Add(new Vector2Int(cx + 1, y));
                placed.Add(new Vector2Int(cx, y));
                return true;
            }

            bool TryPlaceOnRun((int compId, int y, int x0, int x1) run, float minSeparation)
            {
                // Probar el centro y despues el resto de posiciones del tramo.
                int mid = (run.x0 + run.x1) / 2;
                if (TryPlaceAt(mid, run.y, minSeparation)) return true;
                for (int x = run.x0; x < run.x1; x++)
                    if (TryPlaceAt(x, run.y, minSeparation)) return true;
                return false;
            }

            // 1) Una escalera por meseta, en su mejor tramo (los largos primero).
            for (int c = 1; c <= compCount; c++)
            {
                var compRuns = runs.FindAll(r => r.compId == c);
                compRuns.Sort((a, b) => (b.x1 - b.x0).CompareTo(a.x1 - a.x0));
                bool done = false;
                foreach (var run in compRuns)
                    if (TryPlaceOnRun(run, 0f)) { done = true; break; }
                if (!done)
                    Debug.LogError($"[MapGenerator] Meseta {c} (tier2={isTier2}) sin sitio para " +
                        "escalera: quedara inaccesible a pie.");
            }

            // 2) Escaleras extra en los tramos mas largos, con separacion.
            int extraWanted = isTier2 ? 1 : 2;
            runs.Sort((a, b) => (b.x1 - b.x0).CompareTo(a.x1 - a.x0));
            foreach (var run in runs)
            {
                if (extraWanted <= 0) break;
                if (run.x1 - run.x0 < 4) continue;
                if (TryPlaceOnRun(run, 10f)) extraWanted--;
            }
        }

        /// Etiqueta componentes conexas (4 direcciones) de una mascara. Devuelve
        /// ids 1..count en la matriz (0 = fuera de la mascara).
        static int[,] LabelComponents(bool[,] mask, out int count)
        {
            var comp = new int[MapData.W, MapData.H];
            count = 0;
            var stack = new Stack<Vector2Int>();
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    if (!mask[x, y] || comp[x, y] != 0) continue;
                    count++;
                    comp[x, y] = count;
                    stack.Push(new Vector2Int(x, y));
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        {
                            int nx = c.x + d.x, ny = c.y + d.y;
                            if (nx < 0 || nx >= MapData.W || ny < 0 || ny >= MapData.H) continue;
                            if (!mask[nx, ny] || comp[nx, ny] != 0) continue;
                            comp[nx, ny] = count;
                            stack.Push(new Vector2Int(nx, ny));
                        }
                    }
                }
            return comp;
        }

        /// Aserto de conectividad tras generar: toda celda transitable debe
        /// alcanzarse a pie desde 'start'. Registra un error por cada bolsa
        /// aislada (p.ej. una meseta que se quedo sin escalera) para que las
        /// regresiones del generador salten a la vista en consola y batchmode.
        public static bool ValidateConnectivity(MapData map, Vector2Int start)
        {
            if (!map.Walkable(start.x, start.y))
            {
                Debug.LogError($"[MapGenerator] El origen {start} no es transitable; conectividad no validada.");
                return false;
            }

            var visited = map.ComputeReachable(start);
            var stack = new Stack<Vector2Int>();
            bool ok = true;
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    if (!map.walkable[x, y] || visited[x, y]) continue;

                    // Medir la bolsa aislada para el mensaje de error.
                    int size = 0;
                    visited[x, y] = true;
                    stack.Push(new Vector2Int(x, y));
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        size++;
                        foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        {
                            int nx = c.x + d.x, ny = c.y + d.y;
                            if (!map.Walkable(nx, ny) || visited[nx, ny]) continue;
                            visited[nx, ny] = true;
                            stack.Push(new Vector2Int(nx, ny));
                        }
                    }
                    Debug.LogError($"[MapGenerator] Zona transitable aislada de {size} celdas cerca de ({x},{y}): inaccesible a pie desde {start}.");
                    ok = false;
                }
            return ok;
        }

        static bool CellOkForStair(MapData map, int x, int y, bool isTier2)
        {
            if (!map.InBounds(x, y) || !map.InBounds(x, y - 1)) return false;
            if (isTier2)
            {
                // La escalera del nivel 2 desemboca en el interior del nivel 1
                // (no en su borde, que esta bloqueado).
                return map.Elev1(x, y) && !map.Elev2(x, y) && !map.Elev2(x, y - 1)
                    && Interior(map.elev1, x, y - 1);
            }
            // La escalera del nivel 1 desemboca en suelo llano (no agua, no otro acantilado).
            return map.Land(x, y) && !map.Elev1(x, y)
                && map.Land(x, y - 1) && !map.Elev1(x, y - 1) && !map.cliff1[x, y - 1];
        }

        static void ComputeWalkable(MapData map)
        {
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    var cell = new Vector2Int(x, y);
                    bool ok = map.land[x, y];

                    if (ok && (map.cliff1[x, y] || map.cliff2[x, y]))
                        ok = map.stairWalkable.Contains(cell);

                    if (ok && map.elev1[x, y] && IsRim(map.elev1, x, y))
                        ok = map.stairWalkable.Contains(cell);

                    if (ok && map.elev2[x, y] && IsRim(map.elev2, x, y))
                        ok = map.stairWalkable.Contains(cell);

                    map.walkable[x, y] = ok;
                }

            // Los islotes decorativos del oceano (bolsas pequenas y llanas, aisladas
            // por diseno) no cuentan como zona jugable; asi ValidateConnectivity no
            // da falsos positivos. Una meseta aislada (con celdas elevadas) NO se
            // excluye: esa si es una regresion que debe saltar en la validacion.
            int[,] comp = LabelComponents(map.walkable, out int compCount);
            if (compCount <= 1) return;

            var sizes = new int[compCount + 1];
            var hasElevated = new bool[compCount + 1];
            int largest = 1;
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    int c = comp[x, y];
                    if (c == 0) continue;
                    sizes[c]++;
                    if (map.elev1[x, y]) hasElevated[c] = true;
                    if (sizes[c] > sizes[largest]) largest = c;
                }

            int dropped = 0;
            for (int x = 0; x < MapData.W; x++)
                for (int y = 0; y < MapData.H; y++)
                {
                    int c = comp[x, y];
                    if (c == 0 || c == largest) continue;
                    if (sizes[c] < 25 && !hasElevated[c])
                    {
                        map.walkable[x, y] = false;
                        dropped++;
                    }
                }
            if (dropped > 0)
                Debug.Log($"[MapGenerator] {dropped} celdas de islotes decorativos excluidas de la zona jugable.");
        }

        static bool IsRim(bool[,] mask, int x, int y)
        {
            return !Get(mask, x - 1, y) || !Get(mask, x + 1, y) ||
                   !Get(mask, x, y - 1) || !Get(mask, x, y + 1);
        }

        static bool Interior(bool[,] mask, int x, int y)
        {
            return Get(mask, x, y) && !IsRim(mask, x, y);
        }
    }
}
