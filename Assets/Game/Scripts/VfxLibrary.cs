using UnityEngine;

namespace TinyRpg
{
    /// Contenedor en escena que provee el material URP correcto a los VFX.
    public class VfxLibrary : MonoBehaviour
    {
        public Material vfxMaterial;

        void Awake()
        {
            if (vfxMaterial != null) AttackVfx.SharedMaterial = vfxMaterial;
        }
    }
}
