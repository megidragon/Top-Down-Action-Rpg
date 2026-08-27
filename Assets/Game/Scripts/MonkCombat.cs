using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Combate del monje:
    ///  - Click izq.: patada de corta distancia (dano de guerrero, empuje mucho mayor)
    ///    — usa la maquinaria de barrido de la clase base con sus parametros.
    ///  - Click der.: embestida hacia el raton (2x el alcance de la estocada del
    ///    lancero). Redirigible en todo momento moviendo el raton, cancelable con
    ///    otro click derecho. Al impactar aturde 0.5 s. Puede ser bloqueada por un
    ///    parry. Recuperacion propia de 3 s (no bloquea la patada).
    ///  - Espacio: curacion de 30 en area (a si mismo y aliados cercanos, para un
    ///    futuro multijugador), 1 uso cada 5 s. El monje NO tiene parry.
    public class MonkCombat : CharacterCombat
    {
        [Header("Curacion (Espacio)")]
        public float healAmount = 30f;
        public float healRadius = 2.5f;
        public float healCooldown = 5f;
        public float healCastTime = 0.8f;

        [Header("Embestida (click der.)")]
        public float chargeDistance = 7.6f; // 2x la estocada del lancero (3.8)
        public float chargeSpeed = 13f;
        public float chargeDamage = 15f;
        public float chargeKnockback = 6f;
        public float chargeStun = 0.5f;     // aturdimiento al impactar
        public float chargeCooldown = 3f;

        public bool IsCharging { get; private set; }

        float healCooldownTimer;
        float chargeCooldownTimer;

        static readonly Collider2D[] overlapBuffer = new Collider2D[16];

        protected override void Update()
        {
            base.Update();
            if (healCooldownTimer > 0f) healCooldownTimer -= Time.deltaTime;
            if (chargeCooldownTimer > 0f) chargeCooldownTimer -= Time.deltaTime;
        }

        // --- Curacion: sustituye al parry ---
        public override void OnSpecial(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || healCooldownTimer > 0f) return;
            // No desperdiciar el rezo: solo si alguien del equipo en el area
            // (el propio monje incluido) tiene vida que recuperar.
            if (!TeamNeedsHealing(1f)) return;

            healCooldownTimer = healCooldown;
            actionRoutine = StartCoroutine(HealRoutine());
        }

        /// true si el monje o algun companero de equipo dentro del radio de
        /// curacion esta por debajo de la fraccion de vida dada.
        public bool TeamNeedsHealing(float fraction)
        {
            if (stats.Health < stats.maxHealth * fraction) return true;

            int count = Physics2D.OverlapCircle((Vector2)transform.position, healRadius,
                new ContactFilter2D().NoFilter(), overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var ally = col.attachedRigidbody.GetComponent<CharacterStats>();
                if (ally == null || ally.IsDead || ally.team != stats.team) continue;
                if (ally.Health < ally.maxHealth * fraction) return true;
            }
            return false;
        }

        IEnumerator HealRoutine()
        {
            IsAttacking = true; // bloquea otras acciones durante el rezo
            motor.MoveControl = 0.2f;
            unitAnimator?.PlayAction("Heal", healCastTime);

            yield return new WaitForSeconds(0.35f);

            // Cura en area: el propio monje y cualquier aliado dentro del radio.
            Vector2 center = transform.position;
            int count = Physics2D.OverlapCircle(center, healRadius,
                new ContactFilter2D().NoFilter(), overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var ally = col.attachedRigidbody.GetComponent<CharacterStats>();
                if (ally == null || ally.IsDead || ally.team != stats.team) continue;
                ally.Heal(healAmount);
                ally.GetComponent<UnitAnimator>()?.FlashHit(new Color(0.5f, 1f, 0.55f, 1f));
            }

            // Onda verde de la curacion.
            var ring = AttackVfx.CreateRing(healRadius, new Color(0.45f, 1f, 0.5f, 0.4f),
                YSorter.OrderForY(center.y) + 5);
            ring.transform.position = center;
            Destroy(ring, 0.35f);

            yield return new WaitForSeconds(Mathf.Max(0f, healCastTime - 0.35f));

            IsAttacking = false;
            motor.MoveControl = 1f;
            actionRoutine = null;
        }

        // --- Embestida ---
        public override void OnSecondaryDown(Vector2 aimDir)
        {
            if (IsCharging)
            {
                IsCharging = false; // segundo click derecho: cancelar
                return;
            }
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f || chargeCooldownTimer > 0f)
                return;
            // Con el cursor encima del monje no hay embestida posible: no gastar
            // energia ni cooldown por un click vacio.
            if ((AimPoint - motor.BodyPosition).magnitude < 0.6f) return;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return;

            actionRoutine = StartCoroutine(ChargeRoutine());
        }

        IEnumerator ChargeRoutine()
        {
            IsCharging = true;
            IsAttacking = true;
            motor.MoveControl = 0f;
            unitAnimator?.PlayAction("Run", 99f); // se desbloquea al terminar

            float traveled = 0f;
            // Medir con la posicion fisica real: el transform de un cuerpo
            // interpolado puede ir por detras dentro del bucle fijo.
            Vector2 lastPos = motor.BodyPosition;
            float minStep = chargeSpeed * Time.fixedDeltaTime * 0.25f;
            var waitFixed = new WaitForFixedUpdate();

            try
            {
                while (IsCharging && traveled < chargeDistance && !stats.IsDead)
                {
                    // Redirigible: siempre hacia la posicion actual del raton.
                    Vector2 toAim = AimPoint - motor.BodyPosition;
                    if (toAim.magnitude < 0.35f) break; // llego al cursor
                    Vector2 dir = toAim.normalized;

                    motor.SetExternalDrive(dir * chargeSpeed);
                    unitAnimator?.SetFacing(dir.x);

                    yield return waitFixed;

                    Vector2 now = motor.BodyPosition;
                    float step = Vector2.Distance(now, lastPos);
                    traveled += step;
                    lastPos = now;
                    // La cancelacion corta tambien el impacto de este tick, no
                    // solo el avance.
                    if (!IsCharging || stats.IsDead) break;
                    if (step < minStep) break; // frenado contra una pared

                    var victim = FindChargeVictim(now, dir);
                    if (victim != null)
                    {
                        ResolveChargeImpact(victim, dir);
                        break;
                    }
                }
            }
            finally
            {
                // Se ejecuta tambien si la corrutina muere por stagger o muerte:
                // el drive externo jamas queda pegado.
                IsCharging = false;
                IsAttacking = false;
                motor.ClearExternalDrive();
                // Si acabo aturdido (parry), el stagger conserva su bloqueo de movimiento.
                motor.MoveControl = IsStaggered ? 0f : 1f;
                chargeCooldownTimer = chargeCooldown;
                if (!IsStaggered) unitAnimator?.ClearAction();
            }
            actionRoutine = null;
        }

        CharacterStats FindChargeVictim(Vector2 position, Vector2 dir)
        {
            Vector2 probe = position + Vector2.up * 0.35f + dir * 0.5f;
            int count = Physics2D.OverlapCircle(probe, 0.45f,
                new ContactFilter2D().NoFilter(), overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var victim = col.attachedRigidbody.GetComponent<CharacterStats>();
                if (victim != null && !victim.IsDead && victim.team != stats.team)
                    return victim;
            }
            return null;
        }

        void ResolveChargeImpact(CharacterStats victim, Vector2 dir)
        {
            Vector2 to = (Vector2)victim.transform.position - (Vector2)transform.position;
            var victimCombat = victim.GetComponent<CharacterCombat>();

            if (victimCombat != null && victimCombat.IsParryActive
                && Vector2.Angle(victimCombat.ParryDirection, -to) <= victimCombat.parryHalfAngle)
            {
                // Embestida bloqueada: el monje rebota aturdido.
                victimCombat.ConsumeParryBlock();
                AttackVfx.SpawnBlockSpark((Vector2)victim.transform.position + Vector2.up * 0.6f,
                    YSorter.OrderForY(victim.transform.position.y) + 6);
                // El empuje del rebote lo define el DEFENSOR, igual que en ResolveHits.
                GetStaggered(-dir * victimCombat.parriedKnockback);
                unitAnimator?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
                if (isPlayer || victimCombat.isPlayer) SmoothCameraFollow.Shake(0.32f);
                return;
            }

            // Impacto: dano, empujon y aturdimiento de medio segundo.
            var victimMotor = victim.GetComponent<CharacterMotor>();
            victimMotor?.AddKnockback(dir * chargeKnockback);
            victim.GetComponent<UnitAnimator>()?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
            victim.TakeDamage(chargeDamage * CharacterAttributes.DamageOf(this), dir);
            if (!victim.IsDead) victimCombat?.GetStaggered(dir * 1.5f, chargeStun);
            if (isPlayer) SmoothCameraFollow.Shake(0.4f);
        }

        protected override void OnOwnerDied()
        {
            base.OnOwnerDied();
            IsCharging = false;
            motor.ClearExternalDrive();
        }
    }
}
