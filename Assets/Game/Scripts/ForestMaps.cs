using UnityEngine;

namespace TinyRpg
{
    /// Los mapas del roguelike: la ciudad inicial, los 10 niveles del bosque
    /// peligroso y la parada de descanso. Todos pequenos (una-dos pantallas),
    /// cerrados por murallas de arboles.
    public static class ForestMaps
    {
        // ----------------------------------------------------------------
        //  Ciudad de inicio: pueblito compacto con NPCs de relleno y la
        //  entrada al bosque claramente marcada al norte.
        // ----------------------------------------------------------------
        public static MapBuildData Town()
        {
            var m = new MapBuildData(36, 24, 1101);
            m.baseColor = 2;
            m.FillLand();
            m.playerSpawn = new Vector2(18f, 7f);
            m.exitPos = new Vector2(18f, 21.5f);
            m.exitLabel = "exit.enter_forest";

            m.TreeWall(3, m.exitPos, 2.6f);

            // Caminito de tierra hacia la entrada del bosque.
            m.PatchEllipse(18f, 14f, 2.2f, 7.5f, 4);
            m.PatchEllipse(18f, 8f, 5.5f, 3f, 4);

            // Edificios esteticos.
            m.AddDecor(DecorKind.House, 10.5f, 15.5f);
            m.AddDecor(DecorKind.House2, 26f, 15f);
            m.AddDecor(DecorKind.Tower, 12.5f, 20f);
            m.AddDecor(DecorKind.Tower, 24f, 20f);
            m.AddDecor(DecorKind.House2, 8.5f, 9f);
            m.AddDecor(DecorKind.House, 27.5f, 8.5f);

            // NPC minando una roca (la veta al lado) y NPC talando un arbol.
            m.AddDecor(DecorKind.Gold, 30f, 12f, blocking: true, variant: 1);
            m.AddSpecial(SpecialKind.Miner, 29f, 11.4f);
            m.AddDecor(DecorKind.Tree, 6f, 12.5f, blocking: true);
            m.AddSpecial(SpecialKind.Chopper, 7f, 11.8f);

            // Relleno de vida.
            m.AddSpecial(SpecialKind.Walker, 15f, 11f);
            m.AddSpecial(SpecialKind.Walker, 22f, 13f);
            m.AddSpecial(SpecialKind.Sheep, 13f, 6f);
            m.AddSpecial(SpecialKind.Sheep, 24f, 6.5f);
            m.Scatter(DecorKind.Bush, 6, 5f, 5f, 31f, 19f, blocking: false);
            m.Scatter(DecorKind.Rock, 3, 5f, 5f, 31f, 18f);
            return m;
        }

        // ----------------------------------------------------------------
        //  Parada de descanso: campamento calido con fogata y dos mercaderes.
        // ----------------------------------------------------------------
        public static MapBuildData RestStop(int seed)
        {
            var m = new MapBuildData(26, 18, seed);
            m.baseColor = 2;
            m.FillLand();
            m.playerSpawn = new Vector2(4.5f, 9f);
            m.exitPos = new Vector2(23f, 9f);
            m.exitLabel = "exit.rest";

            m.TreeWall(3, m.exitPos, 2.4f);
            m.PatchEllipse(13f, 9f, 6.5f, 4.5f, 1); // claro calido de hierba clara
            m.PatchEllipse(13f, 9f, 2.3f, 1.7f, 4); // tierra quemada bajo la fogata

            m.AddSpecial(SpecialKind.Campfire, 13f, 9f);
            m.AddSpecial(SpecialKind.Vendor, 10f, 12.2f);
            m.AddSpecial(SpecialKind.Vendor, 16f, 12.2f);
            m.AddDecor(DecorKind.Stump, 11.2f, 6.8f);
            m.AddDecor(DecorKind.Stump, 14.8f, 6.6f);
            m.Scatter(DecorKind.Bush, 4, 5f, 4f, 21f, 14f, blocking: false);
            return m;
        }

        // ----------------------------------------------------------------
        //  Niveles del bosque (1..10). Disenos variados, todos dentro del
        //  bosque: cerrados por arboles, con la salida al fondo.
        // ----------------------------------------------------------------
        public static MapBuildData Level(int level)
        {
            switch (((level - 1) % 10) + 1)
            {
                case 1: return ClearingWithPond();
                case 2: return SnakePath();
                case 3: return TwinClearings();
                case 4: return SwampIsle();
                case 5: return RingGrove();
                case 6: return DryDiagonal();
                case 7: return GroveMaze();
                case 8: return RiverCrossing();
                case 9: return StoneCircle();
                default: return TreasureVault();
            }
        }

        // 1: claro amplio con un estanque central.
        static MapBuildData ClearingWithPond()
        {
            var m = Base(34, 22, 2201, out _);
            m.Ellipse(m.land, 17f, 11f, 4.2f, 2.8f, false, 0.3f); // estanque
            AutoTiles.CleanupMask(m.land);
            m.Scatter(DecorKind.Tree, 8, 6f, 5f, 28f, 17f);
            m.Scatter(DecorKind.Bush, 6, 5f, 4f, 29f, 18f, blocking: false);
            m.FillEnemySpawns(8);
            return m;
        }

        // 2: camino en S entre dos brazos de bosque denso.
        static MapBuildData SnakePath()
        {
            var m = Base(36, 22, 2302, out var rng);
            for (int i = 0; i < 60; i++)
            {
                // dos brazos de arboles que fuerzan la S
                float t = (float)rng.NextDouble();
                m.AddDecor(DecorKind.Tree, 10f + t * 16f, 13.5f + (float)(rng.NextDouble() - 0.5) * 2.2f);
            }
            for (int i = 0; i < 60; i++)
            {
                float t = (float)rng.NextDouble();
                m.AddDecor(DecorKind.Tree, 10f + t * 16f, 7.5f + (float)(rng.NextDouble() - 0.5) * 2.2f);
            }
            // huecos de paso: limpiar decor cerca de los vados de la S
            m.decor.RemoveAll(d => d.kind == DecorKind.Tree
                && (Vector2.Distance(d.pos, new Vector2(11f, 7.5f)) < 2.4f
                 || Vector2.Distance(d.pos, new Vector2(25f, 13.5f)) < 2.4f));
            m.PatchEllipse(18f, 10.5f, 12f, 1.6f, 4);
            m.FillEnemySpawns(8);
            return m;
        }

        // 3: dos claros gemelos unidos por un cuello estrecho.
        static MapBuildData TwinClearings()
        {
            var m = Base(38, 20, 2403, out _);
            for (int i = 0; i < 40; i++)
            {
                float y = 3f + (float)(m.rng.NextDouble()) * 14f;
                if (Mathf.Abs(y - 10f) < 2.1f) continue; // cuello libre
                m.AddDecor(DecorKind.Tree, 18.5f + (float)(m.rng.NextDouble() - 0.5) * 2.6f, y);
            }
            m.PatchEllipse(9f, 10f, 5.5f, 4.5f, 3);
            m.PatchEllipse(29f, 10f, 5.5f, 4.5f, 5);
            m.Scatter(DecorKind.Rock, 4, 4f, 4f, 34f, 16f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 4: isla pantanosa en un lago con dos vados de tierra.
        static MapBuildData SwampIsle()
        {
            var m = Base(34, 22, 2504, out _);
            m.baseColor = 5;
            m.Ellipse(m.land, 17f, 11f, 10.5f, 6.5f, false, 0.3f);   // lago
            m.Ellipse(m.land, 17f, 11f, 6f, 3.6f, true, 0.3f);       // isla central
            m.Strip(m.land, new Vector2(4f, 11f), new Vector2(14f, 11f), 1.4f, true);
            m.Strip(m.land, new Vector2(21f, 11f), new Vector2(31f, 11f), 1.4f, true);
            AutoTiles.CleanupMask(m.land);
            m.PatchEllipse(17f, 11f, 5f, 3f, 3);
            m.Scatter(DecorKind.Gold, 3, 13f, 8f, 21f, 14f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 5: anillo de paseo alrededor de un bosquecillo central impenetrable.
        static MapBuildData RingGrove()
        {
            var m = Base(32, 22, 2605, out _);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)(m.rng.NextDouble() * Mathf.PI * 2f);
                float r = (float)(m.rng.NextDouble()) * 3.4f;
                m.AddDecor(DecorKind.Tree, 16f + Mathf.Cos(a) * r, 11f + Mathf.Sin(a) * r * 0.7f);
            }
            m.PatchEllipse(16f, 11f, 4.6f, 3.2f, 5);
            m.Scatter(DecorKind.Stump, 4, 4f, 4f, 28f, 18f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 6: diagonal arida con pedreras.
        static MapBuildData DryDiagonal()
        {
            var m = Base(36, 22, 2706, out _);
            m.PatchEllipse(12f, 7f, 7f, 4.5f, 4);
            m.PatchEllipse(25f, 15f, 7f, 4.5f, 4);
            m.Scatter(DecorKind.Rock, 9, 5f, 4f, 31f, 18f);
            m.Scatter(DecorKind.Tree, 7, 5f, 4f, 31f, 18f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 7: laberinto suelto de bosquecillos.
        static MapBuildData GroveMaze()
        {
            var m = Base(38, 24, 2807, out var rng);
            var groves = new[]
            {
                new Vector2(10f, 8f), new Vector2(18f, 15f), new Vector2(26f, 7f),
                new Vector2(12f, 18f), new Vector2(30f, 17f), new Vector2(20f, 5f),
            };
            foreach (var g in groves)
                for (int i = 0; i < 7; i++)
                    m.AddDecor(DecorKind.Tree,
                        g.x + (float)(rng.NextDouble() - 0.5) * 4f,
                        g.y + (float)(rng.NextDouble() - 0.5) * 3f);
            m.Scatter(DecorKind.Bush, 8, 4f, 4f, 34f, 20f, blocking: false);
            m.FillEnemySpawns(9);
            return m;
        }

        // 8: rio que cruza el mapa con dos vados.
        static MapBuildData RiverCrossing()
        {
            var m = Base(36, 22, 2908, out _);
            m.Strip(m.land, new Vector2(0f, 13f), new Vector2(36f, 9f), 2.1f, false); // rio
            m.Strip(m.land, new Vector2(9f, 13.5f), new Vector2(9f, 8f), 1.5f, true);  // vado oeste
            m.Strip(m.land, new Vector2(27f, 12.2f), new Vector2(27f, 7f), 1.5f, true); // vado este
            // Tierra garantizada bajo el spawn y la salida (el rio cruza su altura).
            m.Strip(m.land, new Vector2(2f, 11f), new Vector2(7f, 11f), 1.8f, true);
            m.Strip(m.land, new Vector2(31f, 11f), new Vector2(34.5f, 11f), 1.8f, true);
            AutoTiles.CleanupMask(m.land);
            m.PatchEllipse(18f, 17f, 8f, 2.5f, 3);
            m.Scatter(DecorKind.Tree, 8, 4f, 3f, 32f, 19f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 9: circulo ceremonial de piedras y vetas doradas.
        static MapBuildData StoneCircle()
        {
            var m = Base(32, 22, 3009, out _);
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f;
                m.AddDecor(DecorKind.Rock, 16f + Mathf.Cos(a) * 5.5f, 11f + Mathf.Sin(a) * 4f);
            }
            m.PatchEllipse(16f, 11f, 6.5f, 4.6f, 3);
            m.AddDecor(DecorKind.Gold, 16f, 11f, blocking: true, variant: 2);
            m.Scatter(DecorKind.Tree, 7, 4f, 3f, 28f, 19f);
            m.FillEnemySpawns(8);
            return m;
        }

        // 10: la antesala del tesoro, en el corazon del bosque.
        static MapBuildData TreasureVault()
        {
            var m = Base(30, 22, 3110, out _);
            m.exitLabel = "";
            m.PatchEllipse(15f, 12f, 6f, 4.2f, 3);
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 2f * i / 10f;
                m.AddDecor(DecorKind.Tree, 15f + Mathf.Cos(a) * 7.5f, 12f + Mathf.Sin(a) * 5.6f);
            }
            m.AddSpecial(SpecialKind.Treasure, 15f, 12.6f);
            m.AddDecor(DecorKind.Gold, 13.3f, 11.6f, blocking: true, variant: 0);
            m.AddDecor(DecorKind.Gold, 16.7f, 11.6f, blocking: true, variant: 3);
            m.FillEnemySpawns(8);
            return m;
        }

        /// Base comun de los niveles del bosque: todo tierra, muralla de arboles
        /// con la salida al este, jugador entrando por el oeste.
        static MapBuildData Base(int w, int h, int seed, out System.Random rng)
        {
            var m = new MapBuildData(w, h, seed);
            m.FillLand();
            m.playerSpawn = new Vector2(4.5f, h / 2f);
            m.exitPos = new Vector2(w - 3f, h / 2f);
            m.exitLabel = "exit.next";
            m.TreeWall(3, m.exitPos, 2.4f);
            rng = m.rng;
            return m;
        }
    }
}

