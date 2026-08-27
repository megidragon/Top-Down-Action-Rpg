using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg
{
    /// Rastro de espinas de hielo que deja el rayo del mago. Vive unos
    /// segundos y hiere a quien pise la zona, a intervalos cortos: no revienta
    /// de golpe, castiga quedarse dentro.
    ///
    /// Es autonomo (como la lluvia del arquero): una vez lanzado sigue aunque
    /// el mago muera o cambie de sitio.
    public class IceSpikeField : MonoBehaviour
    {
        public float duration = 3f;
        public float tickInterval = 0.3f;
        public float damagePerTick = 10f;
        public float spikeRadius = 0.75f;

        int attackerTeam;
        bool attackerIsPlayer;
        readonly List<Vector2> spikes = new List<Vector2>();

        static readonly Collider2D[] overlapBuffer = new Collider2D[24];

        /// Siembra espinas a lo largo del rayo, de 'from' a 'to'.
        public static IceSpikeField Spawn(Vector2 from, Vector2 to, int attackerTeam,
            bool attackerIsPlayer, float spacing = 1.05f)
        {
            var go = new GameObject("IceSpikeField");
            go.transform.position = to;

            var field = go.AddComponent<IceSpikeField>();
            field.attackerTeam = attackerTeam;
            field.attackerIsPlayer = attackerIsPlayer;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 dir = length > 0.001f ? delta / length : Vector2.right;

            // Se empieza un poco por delante del lanzador para no plantarle
            // una espina en los pies.
            int count = Mathf.Max(2, Mathf.FloorToInt(length / spacing));
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / count;
                Vector2 p = from + dir * (length * t);
                // Zigzag leve: parece hielo agrietandose, no una fila regular.
                Vector2 side = new Vector2(-dir.y, dir.x);
                p += side * ((i % 2 == 0 ? 1f : -1f) * Random.Range(0.05f, 0.28f));
                field.spikes.Add(p);
            }

            return field;
        }

        IEnumerator Start()
        {
            var sprite = VfxLibrary.IceSpikeSprite;
            var visuals = new List<Transform>();

            foreach (var p in spikes)
            {
                var spikeGo = new GameObject("Spike");
                spikeGo.transform.SetParent(transform, false);
                spikeGo.transform.position = p;
                var sr = spikeGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = YSorter.OrderForY(p.y) + 2;
                if (AttackVfx.SharedMaterial != null) sr.sharedMaterial = AttackVfx.SharedMaterial;
                spikeGo.transform.localScale = Vector3.one * Random.Range(0.85f, 1.25f);
                visuals.Add(spikeGo.transform);
            }

            // Brotan rapido (escala 0 -> 1) para que se lea el barrido del rayo.
            float grow = 0.16f;
            for (int i = 0; i < visuals.Count; i++)
            {
                float target = visuals[i].localScale.x;
                visuals[i].localScale = new Vector3(target, target * 0.15f, 1f);
            }
            float g = 0f;
            while (g < grow)
            {
                g += Time.deltaTime;
                float k = Mathf.Clamp01(g / grow);
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (visuals[i] == null) continue;
                    float target = visuals[i].localScale.x;
                    visuals[i].localScale = new Vector3(target, target * Mathf.Lerp(0.15f, 1f, k), 1f);
                }
                yield return null;
            }

            float elapsed = 0f;
            float nextTick = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextTick)
                {
                    nextTick += tickInterval;
                    Tick();
                }
                yield return null;
            }

            // Se derriten: encogen y desaparecen.
            float melt = 0.25f;
            float m = 0f;
            while (m < melt)
            {
                m += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(m / melt);
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (visuals[i] == null) continue;
                    var s = visuals[i].localScale;
                    visuals[i].localScale = new Vector3(s.x, s.x * k, 1f);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        void Tick()
        {
            var alreadyHit = new HashSet<CharacterStats>();

            foreach (var p in spikes)
            {
                int count = Physics2D.OverlapCircle(p, spikeRadius,
                    new ContactFilter2D().NoFilter(), overlapBuffer);
                for (int i = 0; i < count; i++)
                {
                    var col = overlapBuffer[i];
                    if (col == null || col.attachedRigidbody == null) continue;
                    var victim = col.attachedRigidbody.GetComponent<CharacterStats>();
                    if (victim == null || victim.IsDead || victim.team == attackerTeam) continue;
                    // Un solo golpe por tick aunque pise varias espinas.
                    if (!alreadyHit.Add(victim)) continue;

                    Vector2 center = col.attachedRigidbody.worldCenterOfMass;
                    Vector2 push = (center - p).sqrMagnitude > 0.001f
                        ? (center - p).normalized : Vector2.up;

                    victim.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.6f, 0.85f, 1f, 1f));
                    victim.TakeDamage(damagePerTick, push);
                }
            }

            if (alreadyHit.Count > 0 && attackerIsPlayer)
                SmoothCameraFollow.Shake(0.1f);
        }
    }
}
