using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyRpg
{
    /// Aliado reclutado en los campamentos: una unidad azul de otra clase que
    /// acompana al jugador con la IA inteligente (tier 2).
    ///
    ///  - Por defecto (Auto) te sigue, y entra en combate cuando el combate
    ///    empieza (algun enemigo hace aggro al acercarte).
    ///  - Tecla C: orden de atacar con todo (persigue al enemigo mas cercano).
    ///  - Tecla V: orden de huir de los enemigos.
    ///  - Si muere, no revive: solo se puede reclutar un sustituto (con
    ///    estadisticas base) en una zona de descanso.
    public class AllyAI : MonoBehaviour
    {
        public static readonly List<AllyAI> Active = new List<AllyAI>();

        public enum Order { Auto, Attack, Flee }
        public static Order CurrentOrder { get; private set; } = Order.Auto;

        public int classIndex; // 0 guerrero, 1 lancero, 2 arquero, 3 monje, 4 mago
        public float preferredDistance = 1.2f;
        public float followDistance = 1.7f;

        public CharacterStats Stats => stats;

        CharacterMotor motor;
        CharacterCombat combat;
        CharacterStats stats;
        UnitAnimator unitAnimator;
        Rigidbody2D rb;

        float thinkTimer;
        float attackPauseTimer;
        float parryReactionCooldown;
        EnemyAI target;
        bool engaged;
        CharacterCombat watchedFoe; // rival suscrito para el parry reactivo

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<CharacterCombat>();
            stats = GetComponent<CharacterStats>();
            unitAnimator = GetComponent<UnitAnimator>();
            rb = GetComponent<Rigidbody2D>();
            stats.team = 0;
            stats.Died += OnDied;

            if (combat is ArcherCombat || combat is MageCombat) preferredDistance = 4.5f;
        }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }

        void OnDisable()
        {
            Active.Remove(this);
            WatchFoe(null);
        }

        void OnDied()
        {
            enabled = false;
            GameManager.HandleCorpse(gameObject, dropCoin: false);
            GameFlow.Instance?.Flash(Loc.T("msg.ally.down"));
        }

        // ----------------------------------------------------------------
        //  Ordenes del jugador (C atacar / V huir)
        // ----------------------------------------------------------------

        public static void IssueOrder(Order order)
        {
            if (Active.Count == 0) return;
            CurrentOrder = order;
            string key = order == Order.Attack ? "msg.ally.attack" : "msg.ally.flee";
            GameFlow.Instance?.Flash(Loc.T(key));
        }

        /// Vuelta al comportamiento automatico (al limpiar nivel o cambiar mapa).
        public static void ResetOrders() { CurrentOrder = Order.Auto; }

        // ----------------------------------------------------------------

        void Update()
        {
            if (stats.IsDead) { motor.SetMoveInput(Vector2.zero); return; }

            if (attackPauseTimer > 0f) attackPauseTimer -= Time.deltaTime;
            if (parryReactionCooldown > 0f) parryReactionCooldown -= Time.deltaTime;

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.13f;
                Think();
            }

            Steer();
        }

        void Think()
        {
            target = NearestEnemy(out float dist);
            WatchFoe(target);

            switch (CurrentOrder)
            {
                case Order.Flee:
                    engaged = false;
                    return;
                case Order.Attack:
                    engaged = target != null && dist <= 15f;
                    break;
                default:
                    // Auto: entrar al combate cuando algun enemigo ya hizo aggro
                    // (el jugador empezo la pelea al acercarse o atacar).
                    engaged = target != null && dist <= 12f && AnyEnemyAggroed();
                    break;
            }

            if (engaged && target != null)
                TryCombatActions(dist);
        }

        EnemyAI NearestEnemy(out float bestDist)
        {
            EnemyAI best = null;
            bestDist = float.MaxValue;
            foreach (var enemy in EnemyAI.Active)
            {
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead) continue;
                float d = Vector2.Distance(transform.position, enemy.transform.position);
                if (d < bestDist) { bestDist = d; best = enemy; }
            }
            return best;
        }

        // --- Parry reactivo (tier inteligente); el monje no tiene parry ---

        void WatchFoe(EnemyAI foe)
        {
            var foeCombat = foe != null ? foe.GetComponent<CharacterCombat>() : null;
            if (foeCombat == watchedFoe) return;
            if (watchedFoe != null) watchedFoe.AttackStarted -= OnFoeAttackStarted;
            watchedFoe = foeCombat;
            if (watchedFoe != null) watchedFoe.AttackStarted += OnFoeAttackStarted;
        }

        void OnFoeAttackStarted(CharacterCombat foe, CharacterCombat.AttackKind kind)
        {
            if (combat is MonkCombat) return;
            if (stats.IsDead || combat.IsBusy || motor.IsDashing) return;
            if (!engaged || parryReactionCooldown > 0f) return;

            float dist = Vector2.Distance(transform.position, foe.transform.position);
            float threatRange = kind == CharacterCombat.AttackKind.Sweep ? 2.4f : 3.4f;
            if (dist > threatRange) return;

            parryReactionCooldown = 1.4f;
            if (Random.value < 0.35f)
                StartCoroutine(ParryReaction(foe));
        }

        IEnumerator ParryReaction(CharacterCombat foe)
        {
            yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
            if (stats.IsDead || combat.IsBusy || foe == null) yield break;
            combat.TryParry(((Vector2)foe.transform.position - (Vector2)transform.position).normalized);
        }

        static bool AnyEnemyAggroed()
        {
            foreach (var enemy in EnemyAI.Active)
                if (enemy != null && enemy.IsAggroed) return true;
            return false;
        }

        // ----------------------------------------------------------------
        //  Combate (mismo repertorio inteligente que los enemigos tier 2)
        // ----------------------------------------------------------------

        void TryCombatActions(float dist)
        {
            if (combat.IsBusy || motor.IsDashing || attackPauseTimer > 0f) return;

            Vector2 foePos = target.transform.position;
            Vector2 aim = (foePos - (Vector2)transform.position).normalized;
            combat.AimPoint = foePos;

            if (combat is ArcherCombat)
            {
                if (dist <= 6f && stats.Energy >= 25f)
                {
                    combat.OnSecondaryDown(aim);
                    attackPauseTimer = Random.Range(1.1f, 1.8f);
                }
                else if (dist <= 10f && stats.Energy >= 25f)
                {
                    combat.OnPrimaryDown(aim);
                    StartCoroutine(ReleaseArtillery(target.transform));
                    attackPauseTimer = Random.Range(1.6f, 2.4f);
                }
                return;
            }

            if (combat is MageCombat)
            {
                if (dist > 3f && dist <= 7f && stats.Energy >= 25f && Random.value < 0.45f)
                {
                    combat.OnSecondaryDown(aim); // circulo magico
                    attackPauseTimer = Random.Range(1.3f, 2f);
                }
                else if (dist <= 7.5f && stats.Energy >= 25f)
                {
                    combat.OnPrimaryDown(aim); // proyectil
                    attackPauseTimer = Random.Range(0.9f, 1.5f);
                }
                return;
            }

            if (combat is MonkCombat monk)
            {
                // La curacion del monje es en area: reza si el o cualquier
                // companero cercano (jugador incluido) esta tocado.
                if (monk.TeamNeedsHealing(0.6f))
                    combat.OnSpecial(aim);
                if (dist > 2.2f && dist <= 7f && stats.Energy >= 25f && Random.value < 0.6f)
                {
                    combat.OnSecondaryDown(aim);
                    attackPauseTimer = Random.Range(0.8f, 1.4f);
                }
                else if (dist <= 1.6f && stats.Energy >= 25f)
                {
                    combat.TrySweep(aim);
                    attackPauseTimer = Random.Range(0.5f, 1f);
                }
                return;
            }

            // Cuerpo a cuerpo (guerrero / lancero).
            if (dist > 2.8f && dist < 6f && stats.Energy >= 50f && Random.value < 0.3f)
            {
                motor.TryDash();
                return;
            }

            if (dist <= 1.6f && stats.Energy >= 25f)
            {
                if (Random.value < 0.7f) combat.TrySweep(aim);
                else combat.TryStab(aim);
                attackPauseTimer = Random.Range(0.5f, 1.1f);
            }
            else if (dist <= 2.6f && stats.Energy >= 25f)
            {
                combat.TryStab(aim);
                attackPauseTimer = Random.Range(0.6f, 1.2f);
            }
        }

        IEnumerator ReleaseArtillery(Transform foe)
        {
            yield return new WaitForSeconds(0.45f);
            if (stats.IsDead || foe == null) yield break;
            combat.AimPoint = foe.position;
            combat.OnPrimaryUp(((Vector2)foe.position - (Vector2)transform.position).normalized);
        }

        // ----------------------------------------------------------------
        //  Movimiento
        // ----------------------------------------------------------------

        void Steer()
        {
            if (combat.IsStaggered) { motor.SetMoveInput(Vector2.zero); return; }

            Vector2 pos = transform.position;
            Vector2 desired = Vector2.zero;

            if (CurrentOrder == Order.Flee)
            {
                var threat = NearestEnemy(out float threatDist);
                if (threat != null && threatDist < 9f)
                {
                    desired = (pos - (Vector2)threat.transform.position).normalized;
                    motor.AimDirection = desired;
                }
                else desired = FollowPlayer(pos);
            }
            else if (engaged && target != null)
            {
                Vector2 toFoe = (Vector2)target.transform.position - pos;
                float dist = toFoe.magnitude;
                Vector2 dir = dist > 0.001f ? toFoe / dist : Vector2.right;
                motor.AimDirection = dir;

                if (dist > preferredDistance) desired = dir;
                else if (dist < preferredDistance * 0.55f) desired = -dir * 0.6f;
            }
            else
            {
                desired = FollowPlayer(pos);
            }

            desired += Separation(pos) * 0.55f;
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired = AvoidObstacles(pos, desired);
            motor.SetMoveInput(desired);
            if (!combat.IsBusy) unitAnimator?.SetFacing(motor.AimDirection.x);
        }

        Vector2 FollowPlayer(Vector2 pos)
        {
            var player = GameManager.Player;
            if (player == null) return Vector2.zero;

            // Cada aliado toma un punto de escolta distinto alrededor del jugador.
            int slot = Mathf.Max(0, Active.IndexOf(this));
            float angle = 210f + slot * 60f; // detras del jugador, en abanico
            Vector2 offset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * followDistance;
            Vector2 anchor = (Vector2)player.transform.position + offset;

            float dist = Vector2.Distance(pos, anchor);
            if (dist < 0.35f) return Vector2.zero;
            Vector2 dir = (anchor - pos).normalized;
            motor.AimDirection = dir;
            // Lejos del jugador: correr a tope para no quedarse atras.
            return dist > 4f ? dir : dir * 0.8f;
        }

        static readonly Collider2D[] separationBuffer = new Collider2D[8];

        Vector2 Separation(Vector2 pos)
        {
            int count = Physics2D.OverlapCircle(pos, 0.8f, new ContactFilter2D().NoFilter(), separationBuffer);
            Vector2 push = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var col = separationBuffer[i];
                if (col == null || col.attachedRigidbody == null) continue;
                var otherGo = col.attachedRigidbody.gameObject;
                if (otherGo == gameObject) continue;
                // Se separa de otros aliados y del jugador (no de los enemigos).
                if (otherGo.GetComponent<AllyAI>() == null
                    && otherGo.GetComponent<PlayerController>() == null) continue;
                Vector2 away = pos - (Vector2)col.attachedRigidbody.position;
                float d = away.magnitude;
                if (d > 0.01f) push += away / d * (1f - Mathf.Clamp01(d / 0.8f));
            }
            return push;
        }

        static readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];

        Vector2 AvoidObstacles(Vector2 pos, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return desired;
            if (!WallInDirection(desired)) return desired;
            Vector2 left = new Vector2(-desired.y, desired.x);
            Vector2 right = -left;
            return WallInDirection(left)
                ? (desired + right).normalized
                : (desired + left).normalized;
        }

        bool WallInDirection(Vector2 dir)
        {
            int count = rb.Cast(dir, wallCastBuffer, 0.7f);
            for (int i = 0; i < count; i++)
            {
                var col = wallCastBuffer[i].collider;
                if (col == null) continue;
                var hitRb = col.attachedRigidbody;
                if (hitRb == null || hitRb.bodyType == RigidbodyType2D.Static) return true;
            }
            return false;
        }

        // ----------------------------------------------------------------
        //  Creacion
        // ----------------------------------------------------------------

        /// Instancia una unidad azul de la clase dada como aliado. Con
        /// dormant=true queda de pie sin IA (el recluta esperando en el
        /// campamento); Activate() lo pone en marcha.
        public static AllyAI Spawn(int classIndex, Vector2 pos, bool dormant = false,
            Transform parent = null)
        {
            var screen = ClassSelectScreen.Instance;
            if (screen == null) return null;
            var prefab = classIndex == 4 ? screen.magePrefab
                       : classIndex == 3 ? screen.monkPrefab
                       : classIndex == 2 ? screen.archerPrefab
                       : classIndex == 1 ? screen.lancerPrefab : screen.warriorPrefab;
            if (prefab == null) return null;

            // El prefab de jugador trae PlayerController: suprimir su registro
            // global mientras se instancia y sustituirlo por la IA de aliado.
            PlayerController.SpawningAlly = true;
            var go = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            PlayerController.SpawningAlly = false;
            go.name = "Ally_" + prefab.name;

            var pc = go.GetComponent<PlayerController>();
            if (pc != null) { pc.enabled = false; Destroy(pc); }

            var combat = go.GetComponent<CharacterCombat>();
            if (combat != null) combat.isPlayer = false;

            var attrs = go.GetComponent<CharacterAttributes>();
            if (attrs == null) attrs = go.AddComponent<CharacterAttributes>();
            attrs.strength = 5;
            attrs.defense = 5;
            attrs.speed = 5;
            go.GetComponent<CharacterMotor>()?.RefreshAttributesCache();

            // Marcador de equipo: anillo verde tenue a los pies.
            var ring = AttackVfx.CreateRing(0.42f, new Color(0.45f, 1f, 0.55f, 0.3f), 5);
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            var ai = go.AddComponent<AllyAI>();
            ai.classIndex = classIndex;
            if (dormant) ai.enabled = false;
            return ai;
        }

        public void Activate()
        {
            transform.SetParent(null);
            enabled = true;
        }
    }
}
