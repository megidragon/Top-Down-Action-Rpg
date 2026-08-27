using UnityEngine;

namespace TinyRpg
{
    /// Contenedor en escena que provee el material URP correcto a los VFX
    /// y sprites compartidos (flecha del arquero).
    public class VfxLibrary : MonoBehaviour
    {
        public static Sprite ArrowSprite { get; private set; }
        public static Sprite MagicBoltSprite { get; private set; }
        public static Sprite MagicCircleSprite { get; private set; }

        public Material vfxMaterial;
        public Sprite arrowSprite;
        public Sprite magicBoltSprite;
        public Sprite magicCircleSprite;

        void Awake()
        {
            if (vfxMaterial != null) AttackVfx.SharedMaterial = vfxMaterial;
            ArrowSprite = arrowSprite;
            MagicBoltSprite = magicBoltSprite;
            MagicCircleSprite = magicCircleSprite;
        }
    }
}
