using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Impacto de flecha en area (lluvia de flecha del arquero). Es autonomo:
    /// una vez fijado el objetivo, el marcador es visible para todos y la flecha
    /// cae aunque el tirador muera. Cae del cielo, asi que ignora muros.
    public class ArrowStrike : MonoBehaviour
    {
        float radius;
        float damage;
        float delay;
        float knockback;
        int attackerTeam;
        bool attackerIsPlayer;

        GameObject ring;
        GameObject arrow;

        static readonly Collider2D[] overlapBuffer = new Collider2D[24];

        public static void Spawn(Vector2 target, float radius, float damage, float delay,
            float knockback, int attackerTeam, bool attackerIsPlayer)
        {
            var go = new GameObject("ArrowStrike");
            go.transform.position = target;
            var strike = go.AddComponent<ArrowStrike>();
            strike.radius = radius;
            strike.damage = damage;
            strike.delay = delay;
            strike.knockback = knockback;
            strike.attackerTeam = attackerTeam;
            strike.attackerIsPlayer = attackerIsPlayer;
        }

        IEnumerator Start()
        {
            Vector2 target = transform.position;
            int order = YSorter.OrderForY(target.y) + 4;

            // Marcador de impacto fijado: a partir de aqui lo ve cualquier unidad.
            // Como hijo del strike: si GameFlow destruye el strike al cambiar de
            // mapa, el anillo cae con el (no queda huerfano).
            ring = AttackVfx.CreateRing(radius, new Color(1f, 0.45f, 0.3f, 0.6f), order);
            ring.transform.SetParent(transform, false);
            ring.transform.position = target;

            // La flecha cae durante el ultimo tramo del retardo.
            float fallTime = Mathf.Min(0.16f, delay * 0.4f);
            yield return new WaitForSeconds(delay - fallTime);

            if (VfxLibrary.ArrowSprite != null)
            {
                arrow = new GameObject("FallingArrow");
                arrow.transform.SetParent(transform, false);
                var sr = arrow.AddComponent<SpriteRenderer>();
                sr.sprite = VfxLibrary.ArrowSprite;
                sr.sortingOrder = order + 2;
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // punta hacia abajo
                Vector2 from = target + Vector2.up * 7f;
                float t = 0f;
                while (t < fallTime)
                {
                    t += Time.deltaTime;
                    arrow.transform.position = Vector2.Lerp(from, target, Mathf.Clamp01(t / fallTime));
                    yield return null;
                }
                arrow.transform.position = target;
            }
            else
            {
                yield return new WaitForSeconds(fallTime);
            }

            Impact(target);

            // Flash final del anillo y limpieza.
            AttackVfx.SpawnArc(target, Vector2.right, radius, 180f,
                new Color(1f, 0.75f, 0.45f, 0.45f), order + 1, 0.18f);
            Destroy(ring);
            if (arrow != null) Destroy(arrow, 0.25f);
            Destroy(gameObject, 0.3f);
        }

        void Impact(Vector2 target)
        {
            int count = Physics2D.OverlapCircle(target, radius + 0.35f,
                new ContactFilter2D().NoFilter(), overlapBuffer);
            var alreadyHit = new System.Collections.Generic.HashSet<CharacterStats>();
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var victim = col.attachedRigidbody.GetComponent<CharacterStats>();
                if (victim == null || victim.IsDead || victim.team == attackerTeam) continue;
                if (!alreadyHit.Add(victim)) continue;

                Vector2 victimCenter = (Vector2)col.attachedRigidbody.worldCenterOfMass;
                if (Vector2.Distance(victimCenter, target) > radius + 0.35f) continue;

                Vector2 pushDir = victimCenter - target;
                pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : Vector2.up;

                var victimMotor = victim.GetComponent<CharacterMotor>();
                victimMotor?.AddKnockback(pushDir * knockback);
                victim.GetComponent<UnitAnimator>()?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
                victim.TakeDamage(damage, pushDir);

                if (attackerIsPlayer) SmoothCameraFollow.Shake(0.35f);
            }
        }
    }
}
