using UnityEngine;

namespace TinyRpg
{
    /// Flashes de area de ataque generados por codigo: abanico (barrido/parry),
    /// rectangulo (estocada) y chispa de bloqueo. Se desvanecen y se destruyen solos.
    public class AttackVfx : MonoBehaviour
    {
        public static Material SharedMaterial; // asignado por VfxLibrary al cargar la escena

        float lifetime;
        float age;
        Color[] initialColors;
        Color[] fadedColors;
        Mesh mesh;

        public static void SpawnArc(Vector2 origin, Vector2 dir, float radius, float halfAngleDeg,
            Color color, int sortingOrder, float lifetime)
        {
            int segments = Mathf.Max(6, Mathf.CeilToInt(halfAngleDeg / 9f));
            var verts = new Vector3[segments + 2];
            var colors = new Color[segments + 2];
            var tris = new int[segments * 3];

            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            float half = halfAngleDeg * Mathf.Deg2Rad;
            verts[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float a = baseAngle - half + (2f * half) * i / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            Color edge = color; edge.a *= 0.25f;
            colors[0] = color;
            for (int i = 1; i < colors.Length; i++) colors[i] = edge;

            Create(origin, verts, tris, colors, color, sortingOrder, lifetime);
        }

        public static void SpawnLine(Vector2 origin, Vector2 dir, float length, float width,
            Color color, int sortingOrder, float lifetime)
        {
            dir = dir.normalized;
            Vector2 side = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            var verts = new Vector3[]
            {
                (Vector3)(side),
                (Vector3)(dir * length + side),
                (Vector3)(dir * length - side),
                (Vector3)(-side),
            };
            var tris = new int[] { 0, 1, 2, 0, 2, 3 };
            Color tip = color; tip.a *= 0.3f;
            var colors = new Color[] { color, tip, tip, color };
            Create(origin, verts, tris, colors, color, sortingOrder, lifetime);
        }

        public static void SpawnBlockSpark(Vector2 position, int sortingOrder)
        {
            SpawnArc(position, Vector2.up, 0.55f, 180f, new Color(0.75f, 0.92f, 1f, 0.85f), sortingOrder, 0.18f);
        }

        static void Create(Vector2 origin, Vector3[] verts, int[] tris, Color[] colors,
            Color color, int sortingOrder, float lifetime)
        {
            var go = new GameObject("AttackVfx");
            go.transform.position = new Vector3(origin.x, origin.y, 0f);

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateBounds();

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedMaterial != null ? SharedMaterial : GetFallbackMaterial();
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var vfx = go.AddComponent<AttackVfx>();
            vfx.lifetime = lifetime;
            vfx.mesh = mesh;
            vfx.initialColors = (Color[])colors.Clone();
            vfx.fadedColors = new Color[colors.Length];
        }

        static Material fallbackMaterial;
        static Material GetFallbackMaterial()
        {
            if (fallbackMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                fallbackMaterial = new Material(shader);
            }
            return fallbackMaterial;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(mesh);
                Destroy(gameObject);
                return;
            }
            // Desvanecimiento por color de vertice (funciona con cualquier shader de sprite).
            float fade = 1f - (age / lifetime);
            for (int i = 0; i < initialColors.Length; i++)
            {
                var c = initialColors[i];
                c.a *= fade;
                fadedColors[i] = c;
            }
            mesh.colors = fadedColors;
        }
    }

}
