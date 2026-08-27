using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg
{
    /// Sistema de combate compartido por jugador y NPC:
    ///  - Barrido: abanico de 130 grados, corto alcance, centrado en la direccion de apuntado.
    ///  - Estocada: linea recta de alcance medio (mas largo que el barrido).
    ///  - Parry: bloquea un ataque que llegue dentro de un cono de 60 grados hacia donde se apunta.
    /// Cada ataque consume 25 de energia.
    [RequireComponent(typeof(CharacterStats))]
    [RequireComponent(typeof(CharacterMotor))]
    public class CharacterCombat : MonoBehaviour
    {
        public enum AttackKind { Sweep, Stab }

        [Header("Barrido (abanico)")]
        public float sweepRange = 1.8f;
        public float sweepHalfAngle = 65f; // 130 grados totales
        public float sweepDamage = 20f;
        public float sweepKnockback = 5f;

        [Header("Estocada (linea)")]
        public float stabRange = 3.0f;
        public float stabWidth = 0.9f;
        public float stabDamage = 30f;
        public float stabKnockback = 6f;

        [Header("Tiempos y costes")]
        public float attackDuration = 0.4f;  // duracion del clip Attack a 10 fps
        public float hitDelay = 0.18f;       // momento del impacto dentro de la animacion
        public float attackRecovery = 0.1f;  // recarga tras el golpe antes de poder atacar de nuevo
        public float attackEnergyCost = 25f;
        public float staggerDuration = 0.55f;

        [Header("Parry")]
        public float parryDuration = 0.35f;
        public float parryCooldown = 0.5f;
        public float parryHalfAngle = 30f;   // cono de 60 grados totales
        public float parriedKnockback = 6.5f; // empuje al atacante bloqueado (como recibir un golpe)

        public bool isPlayer;

        /// Posicion del cursor en el mundo (la fija el PlayerController; las
        /// clases de rango la usan para apuntar en area).
        [NonSerialized] public Vector2 AimPoint;

        protected CharacterStats stats;
        protected CharacterMotor motor;
        protected UnitAnimator unitAnimator;

        protected Coroutine actionRoutine;
        float parryCooldownTimer;
        float staggerTimer;
        protected float attackRecoveryTimer;

        public bool IsAttacking { get; protected set; }
        public bool IsParryActive { get; private set; }
        public Vector2 ParryDirection { get; private set; }
        public bool IsStaggered => staggerTimer > 0f;
        public bool IsBusy => IsAttacking || IsParryActive || IsStaggered || stats.IsDead;

        /// Se dispara al comenzar un ataque (la IA lo usa para reaccionar con parry).
        public event Action<CharacterCombat, AttackKind> AttackStarted;
        public event Action<CharacterCombat> ParryPerformed; // parry que bloqueo un golpe

        // Origen de los ataques: un poco por encima de los pies.
        public Vector2 AttackOrigin => (Vector2)transform.position + Vector2.up * 0.45f;

        static readonly Collider2D[] overlapBuffer = new Collider2D[24];

        protected virtual void Awake()
        {
            stats = GetComponent<CharacterStats>();
            motor = GetComponent<CharacterMotor>();
            unitAnimator = GetComponent<UnitAnimator>();
            stats.Died += OnOwnerDied;
        }

        protected virtual void OnOwnerDied()
        {
            // La muerte cancela cualquier accion en curso (sin VFX postumos
            // ni desbloqueos del visual de muerte).
            if (actionRoutine != null) { StopCoroutine(actionRoutine); actionRoutine = null; }
            IsAttacking = false;
            IsParryActive = false;
            staggerTimer = 0f;
            motor.MoveControl = 1f;
        }

        protected virtual void Update()
        {
            if (parryCooldownTimer > 0f) parryCooldownTimer -= Time.deltaTime;
            if (attackRecoveryTimer > 0f) attackRecoveryTimer -= Time.deltaTime;
            if (staggerTimer > 0f)
            {
                staggerTimer -= Time.deltaTime;
                if (staggerTimer <= 0f) motor.MoveControl = 1f;
            }
        }

        // --- Entradas del jugador (las clases especiales las redefinen) ---
        public virtual void OnPrimaryDown(Vector2 aimDir) { TrySweep(aimDir); }
        public virtual void OnPrimaryUp(Vector2 aimDir) { }
        public virtual void OnSecondaryDown(Vector2 aimDir) { TryStab(aimDir); }
        public virtual void OnSpecial(Vector2 aimDir) { TryParry(aimDir); }

        public bool TrySweep(Vector2 aimDir) => TryAttack(AttackKind.Sweep, aimDir);
        public bool TryStab(Vector2 aimDir) => TryAttack(AttackKind.Stab, aimDir);

        bool TryAttack(AttackKind kind, Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || attackRecoveryTimer > 0f) return false;
            if (aimDir.sqrMagnitude < 0.001f) return false;
            if (!stats.TrySpendEnergy(attackEnergyCost)) return false;

            actionRoutine = StartCoroutine(AttackRoutine(kind, aimDir.normalized));
            return true;
        }

        public bool TryParry(Vector2 aimDir)
        {
            if (IsBusy || motor.IsDashing || parryCooldownTimer > 0f) return false;
            if (aimDir.sqrMagnitude < 0.001f) return false;

            actionRoutine = StartCoroutine(ParryRoutine(aimDir.normalized));
            return true;
        }

        IEnumerator AttackRoutine(AttackKind kind, Vector2 dir)
        {
            IsAttacking = true;
            motor.MoveControl = 0.25f;
            unitAnimator?.SetFacing(dir.x);
            unitAnimator?.PlayAction(kind == AttackKind.Sweep ? "Attack1" : "Attack2", attackDuration);
            AttackStarted?.Invoke(this, kind);

            yield return new WaitForSeconds(hitDelay);

            // Flash del area de impacto + resolucion de golpes en el momento activo.
            SpawnAttackVfx(kind, dir);
            ResolveHits(kind, dir);

            yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));

            IsAttacking = false;
            motor.MoveControl = 1f;
            attackRecoveryTimer = attackRecovery; // recarga del arma antes del siguiente golpe
            actionRoutine = null;
        }

        IEnumerator ParryRoutine(Vector2 dir)
        {
            IsParryActive = true;
            ParryDirection = dir;
            motor.MoveControl = 0.15f;
            unitAnimator?.SetFacing(dir.x);
            unitAnimator?.PlayAction("Guard", parryDuration);
            AttackVfx.SpawnArc(AttackOrigin, dir, 1.25f, parryHalfAngle,
                new Color(0.45f, 0.75f, 1f, 0.4f), SortOrderFor(this) + 5, parryDuration);

            yield return new WaitForSeconds(parryDuration);
            EndParry();
        }

        void EndParry()
        {
            if (!IsParryActive) return;
            IsParryActive = false;
            parryCooldownTimer = parryCooldown;
            motor.MoveControl = 1f;
            unitAnimator?.ClearAction();
            if (actionRoutine != null) { StopCoroutine(actionRoutine); actionRoutine = null; }
        }

        void SpawnAttackVfx(AttackKind kind, Vector2 dir)
        {
            int order = SortOrderFor(this) + 5;
            var color = new Color(1f, 1f, 1f, 0.35f);
            if (kind == AttackKind.Sweep)
                AttackVfx.SpawnArc(AttackOrigin, dir, sweepRange, sweepHalfAngle, color, order, 0.16f);
            else
                AttackVfx.SpawnLine(AttackOrigin, dir, stabRange, stabWidth, color, order, 0.16f);
        }

        void ResolveHits(AttackKind kind, Vector2 dir)
        {
            if (stats.IsDead) return;

            Vector2 origin = AttackOrigin;
            float maxRange = (kind == AttackKind.Sweep ? sweepRange : stabRange) + 0.5f;
            int count = Physics2D.OverlapCircle(origin, maxRange, new ContactFilter2D().NoFilter(), overlapBuffer);

            var alreadyHit = new HashSet<CharacterStats>();
            bool anyImpact = false;

            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var target = col.attachedRigidbody.GetComponent<CharacterStats>();
                if (target == null || target == stats || target.IsDead) continue;
                if (target.team == stats.team) continue;           // sin fuego amigo
                if (!alreadyHit.Add(target)) continue;

                Vector2 targetCenter = (Vector2)col.attachedRigidbody.worldCenterOfMass + Vector2.up * 0.1f;
                Vector2 to = targetCenter - origin;
                float targetRadius = 0.35f;

                bool inArea;
                if (kind == AttackKind.Sweep)
                {
                    inArea = to.magnitude <= sweepRange + targetRadius
                          && Vector2.Angle(dir, to) <= sweepHalfAngle;
                }
                else
                {
                    float forward = Vector2.Dot(to, dir);
                    float lateral = Mathf.Abs(to.x * dir.y - to.y * dir.x);
                    inArea = forward >= -targetRadius * 0.5f && forward <= stabRange + targetRadius
                          && lateral <= stabWidth * 0.5f + targetRadius;
                }
                if (!inArea) continue;

                // Sin linea de vision no hay golpe: los ataques no atraviesan
                // acantilados, muros ni edificios.
                if (BlockedByWall(origin, targetCenter)) continue;

                var targetCombat = target.GetComponent<CharacterCombat>();
                if (targetCombat != null && targetCombat.IsParryActive
                    && Vector2.Angle(targetCombat.ParryDirection, -to) <= targetCombat.parryHalfAngle)
                {
                    // Ataque bloqueado: el parry consume su bloqueo y el atacante es
                    // empujado hacia atras como si recibiese un golpe, ademas de
                    // quedar aturdido un instante.
                    targetCombat.OnParrySuccess();
                    Vector2 pushBack = to.sqrMagnitude > 0.001f ? -to.normalized : -dir;
                    GetStaggered(pushBack * targetCombat.parriedKnockback);
                    unitAnimator?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
                    AttackVfx.SpawnBlockSpark((Vector2)target.transform.position + Vector2.up * 0.6f,
                        SortOrderFor(targetCombat) + 6);
                    if (isPlayer || targetCombat.isPlayer) SmoothCameraFollow.Shake(0.32f);
                    return; // el golpe entero queda anulado por el bloqueo
                }

                // Impacto: dano + empuje en la direccion contraria al atacante.
                // La fuerza del atacante escala el dano (5 puntos = dano base).
                float damage = (kind == AttackKind.Sweep ? sweepDamage : stabDamage)
                    * CharacterAttributes.DamageOf(this);
                float knockback = kind == AttackKind.Sweep ? sweepKnockback : stabKnockback;
                Vector2 pushDir = to.sqrMagnitude > 0.001f ? to.normalized : dir;

                // Empuje y flash antes del dano, para que el golpe letal tambien
                // empuje y no pise el tinte gris de muerte.
                var targetMotor = target.GetComponent<CharacterMotor>();
                targetMotor?.AddKnockback(pushDir * knockback);
                target.GetComponent<UnitAnimator>()?.FlashHit(new Color(1f, 0.4f, 0.4f, 1f));
                target.TakeDamage(damage, pushDir);
                anyImpact = true;
            }

            // Temblor de camara cuando el ataque del jugador impacta.
            if (anyImpact && isPlayer) SmoothCameraFollow.Shake(0.4f);
        }

        /// Consume el bloqueo del parry desde fuera (p.ej. una flecha bloqueada).
        public void ConsumeParryBlock() => OnParrySuccess();

        void OnParrySuccess()
        {
            ParryPerformed?.Invoke(this);
            unitAnimator?.FlashHit(new Color(0.7f, 0.9f, 1f, 1f));
            EndParry();
        }

        public void GetStaggered(Vector2 push) => GetStaggered(push, staggerDuration);

        public void GetStaggered(Vector2 push, float duration)
        {
            if (stats.IsDead) return;
            if (actionRoutine != null) { StopCoroutine(actionRoutine); actionRoutine = null; }
            // Un ataque cancelado paga su recarga DESPUES del aturdimiento: ser
            // bloqueado nunca acelera el siguiente golpe (relevante para el ciclo
            // largo del lancero, cuyo stagger es mas corto que su recarga total).
            if (IsAttacking)
                attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, duration + attackRecovery);
            IsAttacking = false;
            IsParryActive = false;
            staggerTimer = duration;
            motor.MoveControl = 0f;
            motor.CancelDash();
            motor.AddKnockback(push);
            unitAnimator?.ClearAction();
            unitAnimator?.PlayAction("Idle", duration);
            unitAnimator?.FlashHit(new Color(1f, 0.9f, 0.5f, 1f));
        }

        static int SortOrderFor(CharacterCombat c)
        {
            return YSorter.OrderForY(c.transform.position.y);
        }

        static readonly RaycastHit2D[] losBuffer = new RaycastHit2D[12];

        /// true si hay escenario solido (colision estatica o collider sin rigidbody,
        /// como muros, acantilados o edificios) entre dos puntos.
        public static bool BlockedByWall(Vector2 from, Vector2 to)
        {
            int count = Physics2D.Linecast(from, to, new ContactFilter2D().NoFilter(), losBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = losBuffer[i].collider;
                if (col == null || col.isTrigger) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || rb.bodyType == RigidbodyType2D.Static) return true;
            }
            return false;
        }
    }
}
