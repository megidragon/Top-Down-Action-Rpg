using UnityEngine;

namespace TinyRpg
{
    /// Autotile del blob de 16 piezas del tileset Tiny Swords (suelo llano),
    /// utilizable en runtime. Bits de vecinos presentes: N=1, S=2, E=4, W=8.
    public static class AutoTiles
    {
        public static readonly int[] FlatByMask = BuildTable();

        static int[] BuildTable()
        {
            var t = new int[16];
            t[0] = 27;                       // suelto
            t[4] = 24; t[8] = 26; t[12] = 25; // tira horizontal
            t[2] = 3; t[1] = 19; t[3] = 11;   // tira vertical
            t[6] = 0; t[10] = 2; t[5] = 16; t[9] = 18; // esquinas
            t[14] = 1; t[13] = 17; t[7] = 8; t[11] = 10; // bordes
            t[15] = 9;                       // centro
            return t;
        }

        public static bool Get(bool[,] mask, int x, int y)
        {
            return x >= 0 && x < mask.GetLength(0) && y >= 0 && y < mask.GetLength(1)
                && mask[x, y];
        }

        public static int MaskOf(bool[,] mask, int x, int y)
        {
            int m = 0;
            if (Get(mask, x, y + 1)) m |= 1;
            if (Get(mask, x, y - 1)) m |= 2;
            if (Get(mask, x + 1, y)) m |= 4;
            if (Get(mask, x - 1, y)) m |= 8;
            return m;
        }

        /// Limpieza para que el blob de 16 piezas siempre tenga pieza valida:
        /// sin picos de una celda, sin agujeros de alfiler, sin contactos diagonales.
        public static void CleanupMask(bool[,] mask)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int pass = 0; pass < 6; pass++)
            {
                bool changed = false;
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        int n = 0;
                        if (Get(mask, x - 1, y)) n++;
                        if (Get(mask, x + 1, y)) n++;
                        if (Get(mask, x, y - 1)) n++;
                        if (Get(mask, x, y + 1)) n++;
                        if (mask[x, y] && n <= 1) { mask[x, y] = false; changed = true; }
                        else if (!mask[x, y] && n >= 4) { mask[x, y] = true; changed = true; }
                    }
                for (int x = 0; x < w - 1; x++)
                    for (int y = 0; y < h - 1; y++)
                    {
                        bool a = mask[x, y], b = mask[x + 1, y], c = mask[x, y + 1], d = mask[x + 1, y + 1];
                        if (a && d && !b && !c) { mask[x + 1, y] = true; changed = true; }
                        else if (b && c && !a && !d) { mask[x, y] = true; changed = true; }
                    }
                if (!changed) break;
            }
        }
    }
}
