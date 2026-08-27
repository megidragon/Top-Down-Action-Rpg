using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg
{
    /// IA enemiga con el mismo set de habilidades que el jugador:
    /// patrulla alrededor de su campamento, persigue al rival mas cercano
    /// (jugador o aliados) al detectarlo y ataca segun su clase.
    /// El aggro es PERMANENTE: no se puede romper huyendo, ni alejando al
    /// enemigo de su punto de aparicion.
    ///
    /// Tiene 3 niveles de inteligencia (tier), asignados por GameFlow segun el
    /// nivel del bosque:
    ///  0 = tonta (niveles 1-6): reacciona lento, poco aggro, sin dash ni
    ///      parry, y solo usa el ataque basico de su clase.
    ///  1 = media (7-12): reacciones y aggro intermedios, usa especiales a veces.
    ///  2 = inteligente (13+): el comportamiento completo (kiting del arquero,
    ///      curacion/embestida del monje, parry reactivo, dash).
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterCombat))]
    [RequireComponent(typeof(CharacterStats))]
    public class EnemyAI : MonoBehaviour
    {
        public static readonly List<EnemyAI> Active = new List<EnemyAI>();

        public float aggroRange = 7f;
        public float wanderRadius = 3.5f;
        public float sweepUseRange = 1.6f;
        public float stabUseRange = 2.6f;
        public float preferredDistance = 1.2f;
        [Range(0f, 1f)] public float parryChance = 0.35f;
        [Range(0f, 1f)] public float dashChance = 0.3f;

        public int tier = 2;              // 0 tonta, 1 media, 2 inteligente
        float thinkInterval = 0.15f;      // reflejos: cada cuanto decide
        float pauseScale = 1f;            // multiplicador de pausas entre ataques

        CharacterMotor motor;
        CharacterCombat combat;
        CharacterStats stats;
        UnitAnimator unitAnimator;
        Rigidbody2D rb;

        enum State { Patrol, Chase }
        State state = State.Patrol;

        // Un cadaver sigue ~1.5 s en Active mientras se desvanece: no cuenta.
        public bool IsAggroed => state == State.Chase && stats != null && !stats.IsDead;

        public CharacterStats Stats => stats;

        Vector2 home;
        Vector2 wanderTarget;
        float wanderTimer;
        float thinkTimer;
        float attackPauseTimer;   // pausa entre ataques para que el combate respire
        float parryReactionCooldown;
        bool subscribedToPlayer;

        // Rival elegido en el ultimo Think (jugador o aliado, el mas cercano).
        Transform foe;
        CharacterStats foeStats;

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

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// Configura los parametros del nivel de inteligencia. Llamar justo
        /// despues de instanciar (antes del primer Update).
        public void ApplyTier(int newTier)
        {
            tier = newTier;
            switch (tier)
            {
                case 0:
                    aggroRange = 5f;
                    thinkInterval = 0.45f;
                    pauseScale = 1.7f;
                    parryChance = 0f;
                    dashChance = 0f;
                    break;
                case 1:
                    aggroRange = 6f;
                    thinkInterval = 0.28f;
                    pauseScale = 1.25f;
                    parryChance = 0.15f;
                    dashChance = 0.15f;
                    break;
                default:
                    thinkInterval = 0.15f;
                    pauseScale = 1f;
                    break;
            }
            RefreshClassTuning();
        }

        void RefreshClassTuning()
        {
            // Las clases de rango listas mantienen la distancia; las tontas
            // caminan hacia ti.
            if (combat is ArcherCombat || combat is MageCombat)
                preferredDistance = tier == 2 ? 4.5f : tier == 1 ? 3f : 1.6f;
        }

        void Start()
        {
            home = transform.position;
            wanderTarget = home;
            thinkTimer = Random.value * 0.2f;
            RefreshClassTuning();
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
                thinkTimer = thinkInterval;
                Think();
            }

            Steer();
        }

        /// Rival vivo (jugador o aliado) mas cercano.
        void ResolveFoe()
        {
            foe = null;
            foeStats = null;
            float best = float.MaxValue;

            var player = GameManager.Player;
            if (player != null)
            {
                var ps = player.GetComponent<CharacterStats>();
                if (ps != null && !ps.IsDead)
                {
                    best = Vector2.Distance(transform.position, player.transform.position);
                    foe = player.transform;
                    foeStats = ps;
                }
            }

            foreach (var ally in AllyAI.Active)
            {
                if (ally == null || ally.Stats == null || ally.Stats.IsDead) continue;
                float d = Vector2.Distance(transform.position, ally.transform.position);
                if (d < best)
                {
                    best = d;
                    foe = ally.transform;
                    foeStats = ally.Stats;
                }
            }
        }

        void Think()
        {
            ResolveFoe();
            float distToFoe = foe != null
                ? Vector2.Distance(transform.position, foe.position) : float.MaxValue;

            switch (state)
            {
                case State.Patrol:
                    // Solo hace aggro si ademas tiene linea de vision con el rival.
                    if (foe != null && distToFoe <= aggroRange && HasLineOfSight(foe))
                        state = State.Chase;
                    break;

                case State.Chase:
                    // Aggro permanente: una vez detectado, persigue sin importar
                    // la distancia. Solo vuelve a patrullar si no queda rival vivo.
                    if (foe == null)
                    {
                        state = State.Patrol;
                        break;
                    }
                    TryCombatActions(distToFoe);
                    break;
            }
        }

        void Pause(float min, float max)
        {
            attackPauseTimer = Random.Range(min, max) * pauseScale;
        }

        void TryCombatActions(float dist)
        {
            if (combat.IsBusy || motor.IsDashing || attackPauseTimer > 0f) return;
            if (foe == null) return;

            Vector2 foePos = foe.position;
            Vector2 aim = (foePos - (Vector2)transform.position).normalized;
            combat.AimPoint = foePos; // las clases de area apuntan al rival

            // --- Arquero enemigo ---
            if (combat is ArcherCombat)
            {
                if (tier == 0)
                {
                    // Tonto: solo la lluvia de flechas, con calma y sin kitear.
                    if (dist <= 9f && stats.Energy >= 25f)
                    {
                        combat.OnPrimaryDown(aim);
                        StartCoroutine(ReleaseArtillery(foe));
                        Pause(1.6f, 2.4f);
                    }
                    return;
                }
                if (tier == 1)
                {
                    // Medio: rafaga de cerca, lluvia a veces.
                    if (dist <= 5f && stats.Energy >= 25f)
                    {
                        combat.OnSecondaryDown(aim);
                        Pause(1.1f, 1.8f);
                    }
                    else if (dist <= 9f && stats.Energy >= 25f && Random.value < 0.5f)
                    {
                        combat.OnPrimaryDown(aim);
                        StartCoroutine(ReleaseArtillery(foe));
                        Pause(1.6f, 2.4f);
                    }
                    return;
                }
                // Inteligente: rafaga a media distancia, lluvia a larga.
                if (dist <= 6f && stats.Energy >= 25f)
                {
                    combat.OnSecondaryDown(aim);
                    Pause(1.1f, 1.8f);
                }
                else if (dist <= 10f && stats.Energy >= 25f)
                {
                    combat.OnPrimaryDown(aim);
                    StartCoroutine(ReleaseArtillery(foe));
                    Pause(1.6f, 2.4f);
                }
                return;
            }

            // --- Mago enemigo ---
            if (combat is MageCombat)
            {
                if (tier == 0)
                {
                    // Tonto: solo el circulo telegrafiado, con calma.
                    if (dist <= 7f && stats.Energy >= 25f)
                    {
                        combat.OnSecondaryDown(aim);
                        Pause(1.8f, 2.6f);
                    }
                    return;
                }
                if (tier == 1)
                {
                    if (dist <= 5.5f && stats.Energy >= 25f)
                    {
                        combat.OnPrimaryDown(aim); // proyectil
                        Pause(1.2f, 1.8f);
                    }
                    else if (dist <= 7f && stats.Energy >= 25f && Random.value < 0.5f)
                    {
                        combat.OnSecondaryDown(aim); // circulo
                        Pause(1.6f, 2.4f);
                    }
                    return;
                }
                // Inteligente: circulo a media-larga distancia, proyectil de cerca.
                if (dist > 3f && dist <= 7f && stats.Energy >= 25f && Random.value < 0.45f)
                {
                    combat.OnSecondaryDown(aim);
                    Pause(1.3f, 2f);
                }
                else if (dist <= 7.5f && stats.Energy >= 25f)
                {
                    combat.OnPrimaryDown(aim);
                    Pause(0.9f, 1.5f);
                }
                return;
            }

            // --- Monje enemigo ---
            if (combat is MonkCombat)
            {
                if (tier >= 1)
                {
                    // El listo se cura antes; el medio solo al borde de la muerte.
                    float healThreshold = tier == 2 ? 0.5f : 0.25f;
                    if (stats.Health < stats.maxHealth * healThreshold)
                        combat.OnSpecial(aim); // curacion (cooldown interno de 5s)

                    float chargeChance = tier == 2 ? 0.6f : 0.4f;
                    if (dist > 2.2f && dist <= 7f && stats.Energy >= 25f && Random.value < chargeChance)
                    {
                        combat.OnSecondaryDown(aim); // embestida hacia el rival
                        Pause(0.8f, 1.4f);
                        return;
                    }
                }
                if (dist <= 1.6f && stats.Energy >= 25f)
                {
                    combat.TrySweep(aim); // patada
                    Pause(0.5f, 1f);
                }
                return;
            }

            // --- Cuerpo a cuerpo (guerrero / lancero) ---
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
                Pause(0.5f, 1.1f);
            }
            else if (dist <= stabUseRange && stats.Energy >= 25f)
            {
                combat.TryStab(aim);
                Pause(0.6f, 1.2f);
            }
        }

        /// El arquero enemigo suelta la lluvia de flechas tras un breve apuntado
        /// (el anillo fijado da al rival medio segundo para esquivar).
        IEnumerator ReleaseArtillery(Transform target)
        {
            yield return new WaitForSeconds(0.45f);
            if (stats.IsDead || target == null) yield break;
            combat.AimPoint = target.position;
            combat.OnPrimaryUp(((Vector2)target.position - (Vector2)transform.position).normalized);
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

        void Steer()
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
                    if (foe != null)
                    {
                        Vector2 toFoe = (Vector2)foe.position - pos;
                        float dist = toFoe.magnitude;
                        Vector2 dir = dist > 0.001f ? toFoe / dist : Vector2.right;
                        motor.AimDirection = dir; // los enemigos apuntan a donde miran

                        if (dist > preferredDistance) desired = dir;
                        else if (dist < preferredDistance * 0.55f) desired = -dir * 0.6f;

                        desired += Separation(pos) * 0.6f;
                        if (desired.sqrMagnitude > 1f) desired.Normalize();
                    }
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

        bool HasLineOfSight(Transform target)
        {
            return !CharacterCombat.BlockedByWall(
                (Vector2)transform.position + Vector2.up * 0.45f,
                (Vector2)target.position + Vector2.up * 0.45f);
        }
    }
}
