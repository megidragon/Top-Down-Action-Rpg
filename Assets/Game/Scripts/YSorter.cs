using UnityEngine;

namespace TinyRpg
{
    /// Ordena los sprites por profundidad (Y de los pies): cuanto mas abajo, mas delante.
    /// Los objetos estaticos pueden llamar a Apply una sola vez; los moviles lo hacen cada frame.
    public class YSorter : MonoBehaviour
    {
        public const int BaseOrder = 10000;

        public SpriteRenderer[] renderers;
        public bool isStatic;

        void Start()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>();
            Apply();
            if (isStatic) enabled = false;
        }

        void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            int order = OrderForY(transform.position.y);
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sortingOrder = order + i;
        }

        public static int OrderForY(float y)
        {
            return BaseOrder - Mathf.RoundToInt(y * 100f);
        }
    }
}
