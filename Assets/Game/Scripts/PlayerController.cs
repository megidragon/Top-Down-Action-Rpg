using UnityEngine;

namespace TinyRpg
{
    /// Controles del jugador. La entrada concreta la resuelve GameInput:
    ///  - Escritorio: WASD mover | Shift dash | Click izq. barrido |
    ///    Click der. estocada | Espacio parry. Todo apunta hacia el raton.
    ///  - Movil: stick flotante, botones de accion arrastrables para apuntar
    ///    y auto-mira al enemigo mas cercano en los toques secos.
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

            // Juego pausado (menu de pausa): los clicks sobre el menu no deben
            // atravesarlo y disparar ataques ni gastar energia por debajo.
            if (Time.timeScale <= 0f) return;

            // --- Movimiento (normalizado: diagonal = misma velocidad) ---
            motor.SetMoveInput(GameInput.Move);

            // --- Apuntado: raton en escritorio, arrastre o auto-mira en tactil ---
            if (cam == null) cam = Camera.main;
            GameInput.ResolveAim(combat.AttackOrigin, AimDirection,
                out Vector2 aimDir, out Vector2 aimPoint);
            combat.AimPoint = aimPoint;
            AimDirection = aimDir;
            motor.AimDirection = AimDirection;
            if (!combat.IsBusy) unitAnimator?.SetFacing(AimDirection.x);

            // --- Acciones (cada clase define que hace cada boton) ---
            if (GameInput.DashPressed && !combat.IsBusy)
                motor.TryDash();

            if (GameInput.PrimaryPressed)
                combat.OnPrimaryDown(AimDirection);
            if (GameInput.PrimaryReleased)
                combat.OnPrimaryUp(AimDirection);

            if (GameInput.SecondaryPressed)
                combat.OnSecondaryDown(AimDirection);

            if (GameInput.SpecialPressed)
                combat.OnSpecial(AimDirection); // parry (o curacion, en el monje)

            // --- Ordenes a los aliados ---
            if (GameInput.AllyAttackPressed)
                AllyAI.IssueOrder(AllyAI.Order.Attack);
            if (GameInput.AllyFleePressed)
                AllyAI.IssueOrder(AllyAI.Order.Flee);

            // --- Inventario: teclas 1-4 o toque sobre el hueco ---
            if (inventory != null)
                for (int i = 0; i < 4; i++)
                    if (GameInput.ItemPressed(i)) inventory.UseSlot(i);
        }
    }
}
