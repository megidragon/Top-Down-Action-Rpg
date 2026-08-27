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
        /// true mientras AllyAI.Spawn instancia un prefab de jugador para
        /// convertirlo en aliado: evita que ese Awake pise el registro global.
        public static bool SpawningAlly;

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
            if (!SpawningAlly) GameManager.RegisterPlayer(this);
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
                combat.AimPoint = mouseWorld;
                Vector2 aim = (Vector2)mouseWorld - combat.AttackOrigin;
                if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            }
            motor.AimDirection = AimDirection;
            if (!combat.IsBusy) unitAnimator?.SetFacing(AimDirection.x);

            // --- Acciones (cada clase define que hace cada click) ---
            if ((keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                && !combat.IsBusy)
                motor.TryDash();

            if (mouse.leftButton.wasPressedThisFrame)
                combat.OnPrimaryDown(AimDirection);
            if (mouse.leftButton.wasReleasedThisFrame)
                combat.OnPrimaryUp(AimDirection);

            if (mouse.rightButton.wasPressedThisFrame)
                combat.OnSecondaryDown(AimDirection);

            if (keyboard.spaceKey.wasPressedThisFrame)
                combat.OnSpecial(AimDirection); // parry (o curacion, en el monje)

            // --- Ordenes a los aliados: C atacar con todo, V huir ---
            if (keyboard.cKey.wasPressedThisFrame)
                AllyAI.IssueOrder(AllyAI.Order.Attack);
            if (keyboard.vKey.wasPressedThisFrame)
                AllyAI.IssueOrder(AllyAI.Order.Flee);

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
