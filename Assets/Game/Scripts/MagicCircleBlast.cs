using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Estallido del circulo magico del mago. Autonomo: el circulo aparece en
    /// el objetivo (visible para todos), crece durante el retardo y estalla
    /// danando al bando contrario dentro del radio. Ignora muros.
    public class MagicCircleBlast : MonoBehaviour
    {
        float radius;
        float damage;
        float delay;
        float knockback;
        int attackerTeam;
        bool attackerIsPlayer;

        static readonly Collider2D[] overlapBuffer = new Collider2D[24];

        public static void Spawn(Vector2 target, float radius, float damage, float delay,
            float knockback, int attackerTeam, bool attackerIsPlayer)
        {
            var go = new GameObject("MagicCircleBlast");
            go.transform.position = target;
            var blast = go.AddComponent<MagicCircleBlast>();
            blast.radius = radius;
            blast.damage = damage;
            blast.delay = delay;
            blast.knockback = knockback;
            blast.attackerTeam = attackerTeam;
            blast.attackerIsPlayer = attackerIsPlayer;
        }

        IEnumerator Start()
        {
            Vector2 target = transform.position;
            int order = YSorter.OrderForY(target.y) + 4;

            // Telegrafo: el circulo magico crece y gira durante el retardo.
            GameObject circle = null;
            SpriteRenderer sr = null;
            if (VfxLibrary.MagicCircleSprite != null)
            {
                circle = new GameObject("MagicCircle");
                circle.transform.SetParent(transform, false);
                sr = circle.AddComponent<SpriteRenderer>();
                sr.sprite = VfxLibrary.MagicCircleSprite;
                sr.sortingOrder = order;
                if (AttackVfx.SharedMaterial != null) sr.sharedMaterial = AttackVfx.SharedMaterial;
            }

            // El arte del circulo mide ~1.1 unidades de diametro dentro de su lienzo.
            float finalScale = radius * 2f / 1.1f;
            float t = 0f;
            while (t < delay)
            {
                t += Time.deltaTime;
                if (circle != null)
                {
                    float k = Mathf.Clamp01(t / delay);
                    circle.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, k) * finalScale;
                    circle.transform.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
                    if (sr != null) sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, k));
                }
                yield return null;
            }

            Impact(target);

            AttackVfx.SpawnArc(target, Vector2.right, radius, 180f,
                new Color(0.5f, 0.95f, 0.7f, 0.5f), order + 1, 0.18f);
            if (circle != null) Destroy(circle, 0.12f);
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

                victim.GetComponent<CharacterMotor>()?.AddKnockback(pushDir * knockback);
                victim.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.6f, 1f, 0.7f, 1f));
                victim.TakeDamage(damage, pushDir);

                if (attackerIsPlayer) SmoothCameraFollow.Shake(0.32f);
            }
        }
    }
}
