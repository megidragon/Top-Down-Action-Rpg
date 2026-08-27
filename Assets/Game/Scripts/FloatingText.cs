using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Texto flotante sobre un personaje ("+1 FUERZA"): sube, se desvanece y
    /// se destruye solo. Se usa para que una mejora permanente se NOTE en el
    /// momento de beberla, en vez de cambiar un numero escondido en un panel.
    public class FloatingText : MonoBehaviour
    {
        public float rise = 1.1f;
        public float life = 1.3f;

        TextMesh label;
        Color baseColor;

        /// Aparece justo encima de las barras del personaje.
        public static FloatingText SpawnOver(Transform target, string text, Color color)
        {
            if (target == null) return null;
            return Spawn((Vector2)target.position + Vector2.up * 1.95f, text, color);
        }

        public static FloatingText Spawn(Vector2 worldPos, string text, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 64;
            tm.characterSize = 0.055f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            // Por encima de barras (30000) y burbujas (31000).
            mr.sortingOrder = 31500;

            var ft = go.AddComponent<FloatingText>();
            ft.label = tm;
            ft.baseColor = color;
            return ft;
        }

        IEnumerator Start()
        {
            Vector3 from = transform.position;
            Vector3 to = from + Vector3.up * rise;

            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);

                // Sube rapido y frena (ease-out): se lee mejor al final.
                transform.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));

                // Se desvanece en el ultimo tercio.
                float alpha = k < 0.66f ? 1f : 1f - (k - 0.66f) / 0.34f;
                if (label != null)
                    label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
