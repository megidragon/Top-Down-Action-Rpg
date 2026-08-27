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
    /// Conserva dash y parry del kit comun.
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
