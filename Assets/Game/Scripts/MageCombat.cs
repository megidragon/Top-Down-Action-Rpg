using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Combate del mago (unidad de rango):
    ///  - Click izq.: proyectil magico rapido en linea recta hacia el puntero
    ///    (dano de guerrero, se rompe en muros, parryable como una flecha).
    ///  - Click der.: fija el circulo magico en el cursor (alcance limitado) y
    ///    tras un retardo estalla en area — telegrafiado y esquivable, como la
    ///    lluvia del arquero pero instantaneo de lanzar y de menor radio.
    ///  - Espacio: rayo de hielo hasta el suelo bajo el raton. NO tiene parry:
    ///    a cambio siembra espinas por todo el recorrido que hieren durante
    ///    unos segundos. Cuesta MANA, que solo se recupera en la fogata.
    public class MageCombat : CharacterCombat
    {
        [Header("Proyectil magico (click izq.)")]
        public float boltSpeed = 15f;
        public float boltRange = 8.5f;
        public float boltDamage = 20f;   // igual que el barrido del guerrero

        [Header("Circulo magico (click der.)")]
        public float circleMaxRange = 7f;
        public float circleRadius = 1.6f;
        public float circleDamage = 34f;
        public float circleDelay = 0.5f;
        public float circleKnockback = 7f;
        public float circleRecovery = 0.5f;

        public override void OnPrimaryDown(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f) return;
            if (aimDir.sqrMagnitude < 0.001f) return;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return;

            actionRoutine = StartCoroutine(BoltRoutine(aimDir.normalized));
        }

        [Header("Rayo de hielo (Espacio)")]
        public float iceRayRange = 8.5f;
        public float iceManaCost = 40f;
        public float iceRayCastTime = 0.35f;
        public float iceRayRecovery = 0.4f;

        /// El mago cambia el parry por el rayo de hielo.
        public override void OnSpecial(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f) return;
            if (!stats.TrySpendMana(iceManaCost)) return;

            actionRoutine = StartCoroutine(IceRayRoutine());
        }

        IEnumerator IceRayRoutine()
        {
            Vector2 origin = AttackOrigin;
            Vector2 target = ClampToRay(AimPoint);
            Vector2 dir = (target - origin).sqrMagnitude > 0.001f
                ? (target - origin).normalized : Vector2.right;

            IsAttacking = true;
            motor.MoveControl = 0.2f;
            unitAnimator?.SetFacing(dir.x);
            unitAnimator?.PlayAction("Attack2", iceRayCastTime);

            yield return new WaitForSeconds(iceRayCastTime * 0.5f);

            if (!stats.IsDead)
            {
                // Destello del rayo y siembra de espinas por todo el recorrido.
                AttackVfx.SpawnArc(origin, dir, Vector2.Distance(origin, target), 8f,
                    new Color(0.65f, 0.9f, 1f, 0.55f),
                    YSorter.OrderForY(origin.y) + 6, 0.22f);
                IceSpikeField.Spawn(origin, target, stats.team, isPlayer);
            }

            yield return new WaitForSeconds(iceRayCastTime * 0.5f);

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, iceRayRecovery);
            actionRoutine = null;
        }

        /// El rayo llega hasta el suelo bajo el raton, con tope de alcance.
        Vector2 ClampToRay(Vector2 point)
        {
            Vector2 origin = AttackOrigin;
            Vector2 to = point - origin;
            if (to.magnitude > iceRayRange)
                point = origin + to.normalized * iceRayRange;
            return point;
        }

        public override void OnSecondaryDown(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f) return;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return;

            // El circulo queda fijado donde apunta el raton (alcance limitado);
            // el estallido es autonomo aunque el mago muera o sea aturdido.
            Vector2 target = ClampToRange(AimPoint);
            MagicCircleBlast.Spawn(target, circleRadius,
                circleDamage * CharacterAttributes.DamageOf(this), circleDelay,
                circleKnockback, stats.team, isPlayer);
            actionRoutine = StartCoroutine(
                CastLockRoutine(target - (Vector2)transform.position, circleRecovery));
        }

        Vector2 ClampToRange(Vector2 point)
        {
            Vector2 origin = transform.position;
            Vector2 to = point - origin;
            if (to.magnitude > circleMaxRange)
                point = origin + to.normalized * circleMaxRange;
            return point;
        }

        IEnumerator BoltRoutine(Vector2 dir)
        {
            IsAttacking = true;
            motor.MoveControl = 0.25f;
            unitAnimator?.SetFacing(dir.x);
            unitAnimator?.PlayAction("Attack1", attackDuration);

            yield return new WaitForSeconds(hitDelay); // frame del baston extendido

            if (!stats.IsDead)
                ArrowProjectile.Spawn(AttackOrigin, dir, boltSpeed, boltRange,
                    boltDamage * CharacterAttributes.DamageOf(this), stats.team, isPlayer,
                    VfxLibrary.MagicBoltSprite);

            yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, attackRecovery);
            actionRoutine = null;
        }

        IEnumerator CastLockRoutine(Vector2 faceDir, float recovery)
        {
            IsAttacking = true;
            motor.MoveControl = 0.25f;
            unitAnimator?.SetFacing(faceDir.x);
            unitAnimator?.PlayAction("Attack2", attackDuration);

            yield return new WaitForSeconds(attackDuration);

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, recovery);
            actionRoutine = null;
        }
    }
}
