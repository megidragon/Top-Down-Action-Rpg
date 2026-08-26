using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyRpg
{
    /// Controles del jugador:
    ///  WASD mover (diagonales a la misma velocidad) | Shift dash | Click izq. barrido
    ///  Click der. estocada | Espacio parry. Todo apunta hacia el raton.
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterCombat))]
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerController : MonoBehaviour
    {
        CharacterMotor motor;
        CharacterCombat combat;
        CharacterStats stats;
        UnitAnimator unitAnimator;
        Inventory inventory;
        Camera cam;

        public Vector2 AimDirection { get; private set; } = Vector2.right;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<CharacterCombat>();
            stats = GetComponent<CharacterStats>();
            unitAnimator = GetComponent<UnitAnimator>();
            inventory = GetComponent<Inventory>();
            combat.isPlayer = true;
            stats.team = 0;
            GameManager.RegisterPlayer(this);
        }

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (stats.IsDead)
            {
                motor.SetMoveInput(Vector2.zero);
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            // --- Movimiento WASD (normalizado: diagonal = misma velocidad) ---
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            motor.SetMoveInput(move);

            // --- Apuntado con el raton (relativo al personaje) ---
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 aim = (Vector2)mouseWorld - combat.AttackOrigin;
                if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            }
            motor.AimDirection = AimDirection;
            if (!combat.IsBusy) unitAnimator?.SetFacing(AimDirection.x);

            // --- Acciones ---
            if ((keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                && !combat.IsBusy)
                motor.TryDash();

            if (mouse.leftButton.wasPressedThisFrame)
                combat.TrySweep(AimDirection);

            if (mouse.rightButton.wasPressedThisFrame)
                combat.TryStab(AimDirection);

            if (keyboard.spaceKey.wasPressedThisFrame)
                combat.TryParry(AimDirection);

            // --- Inventario: teclas 1-4 usan el slot correspondiente ---
            if (inventory != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) inventory.UseSlot(0);
                if (keyboard.digit2Key.wasPressedThisFrame) inventory.UseSlot(1);
                if (keyboard.digit3Key.wasPressedThisFrame) inventory.UseSlot(2);
                if (keyboard.digit4Key.wasPressedThisFrame) inventory.UseSlot(3);
            }
        }
    }
}
