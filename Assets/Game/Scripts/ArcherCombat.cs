using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Combate del arquero (unidad de rango):
    ///  - Click izq. (mantener y soltar): lluvia de flecha. Al mantener se muestra
    ///    un circulo de punteria alrededor del cursor (SOLO lo ve el tirador; en un
    ///    futuro multijugador seria local). Al soltar, el circulo queda fijado y
    ///    visible para todos, y una flecha cae medio segundo despues danando a
    ///    quien este dentro del radio.
    ///  - Click der.: rafaga de 3 flechas en abanico hacia el puntero, rango medio.
    /// Conserva dash y parry del kit comun.
    public class ArcherCombat : CharacterCombat
    {
        [Header("Lluvia de flecha (click izq.)")]
        public float artilleryRadius = 1.0f;    // circulo de diametro 2x el sprite del jugador
        public float artilleryDamage = 20f;     // igual que el barrido del guerrero
        public float artilleryDelay = 0.5f;
        public float artilleryMaxRange = 11f;   // casi toda la media pantalla visible
        public float artilleryKnockback = 4.5f;

        [Header("Rafaga de 3 flechas (click der.)")]
        public float tripleShotRange = 5.7f;    // 150% de la estocada del lancero (3.8)
        public float tripleShotDamage = 12f;    // por flecha
        public float tripleShotSpread = 12f;    // grados entre flechas
        public float tripleShotRecovery = 0.25f;
        public float arrowSpeed = 16f;

        public bool IsAiming { get; private set; }
        GameObject aimRing;

        public override void OnPrimaryDown(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f || IsAiming) return;
            if (stats.Energy < attackEnergyCost) return; // sin energia no se apunta

            IsAiming = true;
            // Anillo de punteria: visible solo para el tirador mientras apunta.
            aimRing = AttackVfx.CreateRing(artilleryRadius, new Color(1f, 1f, 1f, 0.4f),
                YSorter.OrderForY(transform.position.y) + 5);
            aimRing.transform.position = ClampToRange(AimPoint);
        }

        public override void OnPrimaryUp(Vector2 aimDir)
        {
            if (!IsAiming) return;
            Vector2 target = ClampToRange(AimPoint);
            CancelAiming();

            // Soltar durante un dash tambien dispara: el apuntado siguio vivo y
            // el impacto es autonomo, no necesita al arquero quieto.
            if (IsBusy) return;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return;

            // El marcador de impacto queda fijado y es visible para todos; la
            // flecha cae sola aunque el arquero muera o quede aturdido.
            ArrowStrike.Spawn(target, artilleryRadius,
                artilleryDamage * CharacterAttributes.DamageOf(this), artilleryDelay,
                artilleryKnockback, stats.team, isPlayer);
            actionRoutine = StartCoroutine(
                ShootLockRoutine(target - (Vector2)transform.position, attackRecovery));
        }

        public override void OnSecondaryDown(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f || IsAiming) return;
            if (aimDir.sqrMagnitude < 0.001f) return;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return;

            actionRoutine = StartCoroutine(TripleShotRoutine(aimDir.normalized));
        }

        protected override void Update()
        {
            base.Update();
            if (!IsAiming) return;

            if (IsBusy || stats.IsDead)
            {
                CancelAiming();
                return;
            }
            aimRing.transform.position = ClampToRange(AimPoint);
            unitAnimator?.SetFacing(AimPoint.x - transform.position.x);
        }

        Vector2 ClampToRange(Vector2 point)
        {
            Vector2 origin = transform.position;
            Vector2 to = point - origin;
            if (to.magnitude > artilleryMaxRange)
                point = origin + to.normalized * artilleryMaxRange;
            return point;
        }

        void CancelAiming()
        {
            IsAiming = false;
            if (aimRing != null) Destroy(aimRing);
            aimRing = null;
        }

        IEnumerator ShootLockRoutine(Vector2 faceDir, float recovery)
        {
            IsAttacking = true;
            motor.MoveControl = 0.25f;
            unitAnimator?.SetFacing(faceDir.x);
            unitAnimator?.PlayAction("Attack1", attackDuration);

            yield return new WaitForSeconds(attackDuration);

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, recovery);
            actionRoutine = null;
        }

        IEnumerator TripleShotRoutine(Vector2 dir)
        {
            IsAttacking = true;
            motor.MoveControl = 0.25f;
            unitAnimator?.SetFacing(dir.x);
            unitAnimator?.PlayAction("Attack1", 0.4f);

            yield return new WaitForSeconds(0.15f); // momento de soltar la cuerda

            if (!stats.IsDead)
            {
                Vector2 origin = AttackOrigin;
                foreach (float angle in new[] { -tripleShotSpread, 0f, tripleShotSpread })
                {
                    Vector2 shotDir = Quaternion.Euler(0f, 0f, angle) * dir;
                    ArrowProjectile.Spawn(origin, shotDir, arrowSpeed, tripleShotRange,
                        tripleShotDamage * CharacterAttributes.DamageOf(this), stats.team, isPlayer);
                }
            }

            yield return new WaitForSeconds(0.25f);

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, tripleShotRecovery);
            actionRoutine = null;
        }

        protected override void OnOwnerDied()
        {
            base.OnOwnerDied();
            CancelAiming();
        }
    }
}
