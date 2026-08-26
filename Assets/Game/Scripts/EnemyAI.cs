using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// IA agresiva con el mismo set de habilidades que el jugador:
    /// patrulla alrededor de su campamento, persigue al jugador al detectarlo,
    /// ataca con barrido/estocada, usa dash para cerrar distancia y puede
    /// reaccionar con parry cuando el jugador inicia un ataque cerca.
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterCombat))]
    [RequireComponent(typeof(CharacterStats))]
    public class EnemyAI : MonoBehaviour
    {
        public float aggroRange = 7f;
        public float leashRange = 16f;
        public float wanderRadius = 3.5f;
        public float sweepUseRange = 1.6f;
        public float stabUseRange = 2.6f;
        public float preferredDistance = 1.2f;
        [Range(0f, 1f)] public float parryChance = 0.35f;
        [Range(0f, 1f)] public float dashChance = 0.3f;

        CharacterMotor motor;
        CharacterCombat combat;
        CharacterStats stats;
        UnitAnimator unitAnimator;
        Rigidbody2D rb;

        enum State { Patrol, Chase, Return }
        State state = State.Patrol;

        Vector2 home;
        Vector2 wanderTarget;
        float wanderTimer;
        float thinkTimer;
        float attackPauseTimer;   // pausa entre ataques para que el combate respire
        float parryReactionCooldown;
        bool subscribedToPlayer;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<CharacterCombat>();
            stats = GetComponent<CharacterStats>();
            unitAnimator = GetComponent<UnitAnimator>();
            rb = GetComponent<Rigidbody2D>();
            stats.team = 1;
            stats.Damaged += OnDamaged;
        }

        void Start()
        {
            home = transform.position;
            wanderTarget = home;
            thinkTimer = Random.value * 0.2f;
        }

        void OnDestroy()
        {
            if (subscribedToPlayer && GameManager.Player != null)
                GameManager.Player.GetComponent<CharacterCombat>().AttackStarted -= OnPlayerAttackStarted;
        }

        void OnDamaged(Vector2 fromDir)
        {
            if (!stats.IsDead) state = State.Chase;
        }

        void Update()
        {
            if (stats.IsDead) { motor.SetMoveInput(Vector2.zero); return; }

            var player = GameManager.Player;
            if (player != null && !subscribedToPlayer)
            {
                player.GetComponent<CharacterCombat>().AttackStarted += OnPlayerAttackStarted;
                subscribedToPlayer = true;
            }

            if (parryReactionCooldown > 0f) parryReactionCooldown -= Time.deltaTime;
            if (attackPauseTimer > 0f) attackPauseTimer -= Time.deltaTime;

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.15f;
                Think(player);
            }

            Steer(player);
        }

        void Think(PlayerController player)
        {
            bool playerAlive = player != null && !player.GetComponent<CharacterStats>().IsDead;
            float distToPlayer = playerAlive
                ? Vector2.Distance(transform.position, player.transform.position) : float.MaxValue;
            float distToHome = Vector2.Distance(transform.position, home);

            switch (state)
            {
                case State.Patrol:
                    // Solo hace aggro si ademas tiene linea de vision con el jugador.
                    if (playerAlive && distToPlayer <= aggroRange && HasLineOfSight(player))
                        state = State.Chase;
                    break;

                case State.Chase:
                    if (!playerAlive || distToHome > leashRange || distToPlayer > aggroRange * 1.8f)
                    {
                        state = State.Return;
                        break;
                    }
                    TryCombatActions(player, distToPlayer);
                    break;

                case State.Return:
                    // Histeresis: no re-aggro hasta estar de vuelta dentro del leash.
                    if (distToHome < 1.5f) state = State.Patrol;
                    else if (playerAlive && distToPlayer <= aggroRange * 0.8f
                             && distToHome < leashRange * 0.85f && HasLineOfSight(player))
                        state = State.Chase;
                    break;
            }
        }

        void TryCombatActions(PlayerController player, float dist)
        {
            if (combat.IsBusy || motor.IsDashing || attackPauseTimer > 0f) return;

            Vector2 aim = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;

            // Dash para cerrar distancia (tambien cuesta 25 de energia).
            if (dist > 2.8f && dist < 6f && stats.Energy >= 50f && Random.value < dashChance)
            {
                motor.TryDash();
                return;
            }

            if (dist <= sweepUseRange && stats.Energy >= 25f)
            {
                if (Random.value < 0.7f) combat.TrySweep(aim);
                else combat.TryStab(aim);
                attackPauseTimer = Random.Range(0.5f, 1.1f);
            }
            else if (dist <= stabUseRange && stats.Energy >= 25f)
            {
                combat.TryStab(aim);
                attackPauseTimer = Random.Range(0.6f, 1.2f);
            }
        }

        void OnPlayerAttackStarted(CharacterCombat playerCombat, CharacterCombat.AttackKind kind)
        {
            if (stats.IsDead || combat.IsBusy || motor.IsDashing) return;
            if (state != State.Chase || parryReactionCooldown > 0f) return;

            float dist = Vector2.Distance(transform.position, playerCombat.transform.position);
            float threatRange = kind == CharacterCombat.AttackKind.Sweep ? 2.4f : 3.4f;
            if (dist > threatRange) return;

            parryReactionCooldown = 1.4f;
            if (Random.value < parryChance)
                StartCoroutine(ParryReaction(playerCombat));
        }

        IEnumerator ParryReaction(CharacterCombat playerCombat)
        {
            yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
            if (stats.IsDead || combat.IsBusy) yield break;
            Vector2 dir = ((Vector2)playerCombat.transform.position - (Vector2)transform.position).normalized;
            combat.TryParry(dir);
        }

        void Steer(PlayerController player)
        {
            if (combat.IsStaggered) { motor.SetMoveInput(Vector2.zero); return; }

            Vector2 pos = transform.position;
            Vector2 desired = Vector2.zero;
            float speedScale = 1f;

            switch (state)
            {
                case State.Patrol:
                    wanderTimer -= Time.deltaTime;
                    if (wanderTimer <= 0f)
                    {
                        wanderTimer = Random.Range(2.5f, 5f);
                        wanderTarget = home + Random.insideUnitCircle * wanderRadius;
                    }
                    if (Vector2.Distance(pos, wanderTarget) > 0.4f)
                        desired = (wanderTarget - pos).normalized;
                    speedScale = 0.45f;
                    motor.AimDirection = desired.sqrMagnitude > 0.01f ? desired : motor.AimDirection;
                    break;

                case State.Chase:
                    if (player != null)
                    {
                        Vector2 toPlayer = (Vector2)player.transform.position - pos;
                        float dist = toPlayer.magnitude;
                        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.right;
                        motor.AimDirection = dir; // los enemigos apuntan a donde miran

                        if (dist > preferredDistance) desired = dir;
                        else if (dist < preferredDistance * 0.55f) desired = -dir * 0.6f;

                        desired += Separation(pos) * 0.6f;
                        if (desired.sqrMagnitude > 1f) desired.Normalize();
                    }
                    break;

                case State.Return:
                    desired = (home - pos).normalized;
                    motor.AimDirection = desired;
                    speedScale = 0.7f;
                    break;
            }

            desired = AvoidObstacles(pos, desired);
            motor.SetMoveInput(desired * speedScale);
            if (!combat.IsBusy) unitAnimator?.SetFacing(motor.AimDirection.x);
        }

        static readonly Collider2D[] separationBuffer = new Collider2D[8];

        Vector2 Separation(Vector2 pos)
        {
            int count = Physics2D.OverlapCircle(pos, 0.9f, new ContactFilter2D().NoFilter(), separationBuffer);
            Vector2 push = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var col = separationBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                if (col.attachedRigidbody.gameObject == gameObject) continue;
                var other = col.attachedRigidbody.GetComponent<EnemyAI>();
                if (other == null) continue;
                Vector2 away = pos - (Vector2)col.attachedRigidbody.position;
                float d = away.magnitude;
                if (d > 0.01f) push += away / d * (1f - Mathf.Clamp01(d / 0.9f));
            }
            return push;
        }

        Vector2 AvoidObstacles(Vector2 pos, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return desired;
            if (!WallInDirection(desired)) return desired;

            // Pared delante: deslizarse en perpendicular hacia el lado mas despejado.
            Vector2 left = new Vector2(-desired.y, desired.x);
            Vector2 right = -left;
            return WallInDirection(left)
                ? (desired + right).normalized
                : (desired + left).normalized;
        }

        static readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];

        bool WallInDirection(Vector2 dir)
        {
            // Rigidbody2D.Cast excluye los colliders propios automaticamente.
            int count = rb.Cast(dir, wallCastBuffer, 0.7f);
            for (int i = 0; i < count; i++)
            {
                var col = wallCastBuffer[i].collider;
                if (col == null) continue;
                var hitRb = col.attachedRigidbody;
                // Solo cuenta el escenario (cuerpos estaticos o colliders sin rigidbody).
                if (hitRb == null || hitRb.bodyType == RigidbodyType2D.Static) return true;
            }
            return false;
        }

        bool HasLineOfSight(PlayerController player)
        {
            return !CharacterCombat.BlockedByWall(
                (Vector2)transform.position + Vector2.up * 0.45f,
                (Vector2)player.transform.position + Vector2.up * 0.45f);
        }
    }
}
