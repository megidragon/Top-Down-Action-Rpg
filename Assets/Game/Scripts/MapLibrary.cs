using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyRpg
{
    /// Referencias de assets para construir mapas en runtime (las asigna el
    /// SceneBuilder al hornear la escena). Singleton de escena.
    public class MapLibrary : MonoBehaviour
    {
        public static MapLibrary Instance { get; private set; }

        [Header("Tilemaps de la escena")]
        public Tilemap waterLayer;
        public Tilemap foamLayer;
        public Tilemap groundLayer;
        public Tilemap detailLayer;
        public Tilemap collisionLayer;

        [Header("Tiles (44 por hoja de color)")]
        public TileBase[] color1;
        public TileBase[] color2;
        public TileBase[] color3;
        public TileBase[] color4;
        public TileBase[] color5;
        public TileBase waterBgTile;
        public TileBase foamTile;
        public TileBase collisionTile;

        [Header("Decoracion")]
        public Sprite[] treeSprites;
        public Sprite[] bushSprites;
        public Sprite[] rockSprites;
        public Sprite[] stumpSprites;
        public Sprite[] goldSprites;
        public Sprite houseSprite;
        public Sprite house2Sprite;
        public Sprite towerSprite;
        public Sprite woodTableSprite;
        public Sprite fireSprite;
        public RuntimeAnimatorController fireController;

        [Header("Unidades")]
        public GameObject[] enemyPrefabs; // Warrior, Lancer, Archer, Monk, Mage (rojos)
        public GameObject sheepPrefab;
        public GameObject pawnNpcPrefab;  // NPC ambiental (TownNpc)

        [Header("UI e iconos")]
        public Sprite coinHudIcon;      // Icon_03
        public Sprite potionSmallIcon;  // pocion comun (1 uso)
        public Sprite potionMediumIcon; // media (2 usos)
        public Sprite potionLargeIcon;  // avanzada (3 usos)
        public Sprite elixirStrengthIcon;
        public Sprite elixirDefenseIcon;
        public Sprite elixirSpeedIcon;

        void Awake()
        {
            Instance = this;
        }

        public TileBase[] GetColor(int index)
        {
            switch (index)
            {
                case 1: return color1;
                case 2: return color2;
                case 3: return color3;
                case 4: return color4;
                default: return color5;
            }
        }
    }
}
