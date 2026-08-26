using UnityEngine;

namespace TinyRpg
{
    /// Flecha en linea recta (rafaga del arquero). Dana al primer personaje
    /// enemigo que toca, respeta el parry del objetivo y se rompe contra muros.
    public class ArrowProjectile : MonoBehaviour
    {
        Vector2 direction;
        float speed;
        float remainingRange;
        float damage;
        int attackerTeam;
        bool attackerIsPlayer;

        static readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];

        public static void Spawn(Vector2 origin, Vector2 direction, float speed, float range,
            float damage, int attackerTeam, bool attackerIsPlayer)
        {
            var go = new GameObject("Arrow");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VfxLibrary.ArrowSprite;
            sr.sortingOrder = YSorter.OrderForY(origin.y) + 3;

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow.direction = direction.normalized;
            arrow.speed = speed;
            arrow.remainingRange = range;
            arrow.damage = damage;
            arrow.attackerTeam = attackerTeam;
            arrow.attackerIsPlayer = attackerIsPlayer;
        }

        void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remainingRange);
            if (step <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 pos = transform.position;
            int count = Physics2D.CircleCast(pos, 0.12f, direction,
                new ContactFilter2D().NoFilter(), castBuffer, step);
            for (int i = 0; i < count; i++)
            {
                var hit = castBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var rb = hit.collider.attachedRigidbody;

                if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                {
                    // Muro o escenario: la flecha se rompe.
                    Destroy(gameObject);
                    return;
                }

                var victim = rb.GetComponent<CharacterStats>();
                if (victim == null || victim.IsDead || victim.team == attackerTeam) continue;

                var victimCombat = victim.GetComponent<CharacterCombat>();
                if (victimCombat != null && victimCombat.IsParryActive
                    && Vector2.Angle(victimCombat.ParryDirection, -direction) <= victimCombat.parryHalfAngle)
                {
                    // Flecha bloqueada por el parry (consume su bloqueo).
                    victimCombat.ConsumeParryBlock();
                    AttackVfx.SpawnBlockSpark((Vector2)victim.transform.position + Vector2.up * 0.6f,
                        YSorter.OrderForY(victim.transform.position.y) + 6);
                    Destroy(gameObject);
                    return;
                }

                victim.GetComponent<CharacterMotor>()?.AddKnockback(direction * 3f);
                victim.GetComponent<UnitAnimator>()?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
                victim.TakeDamage(damage, direction);
                if (attackerIsPlayer) SmoothCameraFollow.Shake(0.22f);
                Destroy(gameObject);
                return;
            }

            remainingRange -= step;
            transform.position = pos + direction * step;
            var srr = GetComponent<SpriteRenderer>();
            if (srr != null) srr.sortingOrder = YSorter.OrderForY(transform.position.y) + 3;
        }
    }
}
