using UnityEngine;

namespace TinyRpg
{
    /// Iconos de los objetos (asignados por el SceneBuilder desde Tiny Fantasy Icons).
    public class ItemLibrary : MonoBehaviour
    {
        public static ItemLibrary Instance { get; private set; }

        public Sprite coinIcon;
        public Sprite potionIcon;

        void Awake()
        {
            Instance = this;
        }

        public Sprite GetIcon(ItemType type)
        {
            switch (type)
            {
                case ItemType.Coin: return coinIcon;
                case ItemType.HealthPotion: return potionIcon;
                default: return null;
            }
        }
    }
}
