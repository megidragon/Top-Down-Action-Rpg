using System;
using UnityEngine;

namespace TinyRpg
{
    /// Movimiento fisico del personaje: WASD (o direccion de IA), dash y knockback.
    /// La velocidad diagonal se mantiene igual que la ortogonal (input normalizado).
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CharacterStats))]
    public class CharacterMotor : MonoBehaviour
    {
        public float moveSpeed = 4.6f;
        public float dashSpeed = 15f;
        public float dashDuration = 0.22f;
        public float dashCooldown = 0.35f;
        public float dashEnergyCost = 25f;
        public float knockbackDecay = 14f; // unidades/s^2 de frenado del empuje

        [NonSerialized] public Vector2 AimDirection = Vector2.right; // hacia el raton (jugador) o el objetivo (IA)
        [NonSerialized] public float MoveControl = 1f;               // el combate lo reduce durante ataques/parry

        Rigidbody2D rb;
        CharacterStats stats;
        CharacterAttributes attributes;
        Vector2 moveInput;
        Vector2 knockback;
        Vector2 dashDir;
        Vector2 externalDrive; // velocidad impuesta desde fuera (embestida del monje)
        float dashTimer;
        float dashCooldownTimer;

        public bool IsDashing => dashTimer > 0f;
        public Vector2 MoveInput => moveInput;
        public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
        public event Action DashStarted;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stats = GetComponent<CharacterStats>();
            attributes = GetComponent<CharacterAttributes>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public bool TryDash()
        {
            if (stats.IsDead || IsDashing || dashCooldownTimer > 0f) return false;
            Vector2 dir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : AimDirection;
            if (dir.sqrMagnitude < 0.01f) return false;
            if (!stats.TrySpendEnergy(dashEnergyCost)) return false;

            dashDir = dir.normalized;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown + dashDuration;
            DashStarted?.Invoke();
            return true;
        }

        public void AddKnockback(Vector2 impulse)
        {
            knockback += impulse;
        }

        /// Impone la velocidad del cuerpo (p.ej. embestida). Anula movimiento y dash
        /// mientras este activa; limpiar con ClearExternalDrive.
        public void SetExternalDrive(Vector2 velocity) => externalDrive = velocity;
        public void ClearExternalDrive() => externalDrive = Vector2.zero;

        /// Re-cachea el componente de atributos (para atributos anadidos despues
        /// de instanciar, p.ej. los de los enemigos por nivel).
        public void RefreshAttributesCache() => attributes = GetComponent<CharacterAttributes>();

        /// Posicion fisica real (rb.position): con interpolacion activa, el
        /// transform puede ir por detras dentro del bucle fijo.
        public Vector2 BodyPosition => rb != null ? rb.position : (Vector2)transform.position;

        public void CancelDash()
        {
            dashTimer = 0f;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dashCooldownTimer > 0f) dashCooldownTimer -= dt;

            if (stats.IsDead)
            {
                // El cadaver conserva el empuje del golpe letal hasta frenarse.
                rb.linearVelocity = knockback;
                knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDecay * dt);
                return;
            }

            // La velocidad escala con el atributo de velocidad (+2% por punto).
            float speedScale = attributes != null ? attributes.SpeedMultiplier : 1f;

            Vector2 velocity;
            if (externalDrive.sqrMagnitude > 0.0001f)
            {
                velocity = externalDrive;
            }
            else if (dashTimer > 0f)
            {
                dashTimer -= dt;
                // Curva de dash: arranque fuerte que se desvanece.
                float t = Mathf.Clamp01(dashTimer / dashDuration);
                velocity = dashDir * (dashSpeed * (0.45f + 0.55f * t));
            }
            else
            {
                velocity = moveInput * (moveSpeed * speedScale * Mathf.Clamp01(MoveControl));
            }

            velocity += knockback;
            knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDecay * dt);
            rb.linearVelocity = velocity;
        }
    }
}
