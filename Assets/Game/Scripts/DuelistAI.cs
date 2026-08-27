using System.Collections;
using UnityEngine;

namespace TinyRpg
{
    /// Seis cerebros de combate distintos para duelos de guerreros en el
    /// coliseo. Ninguno es "ir de frente y pegar" salvo el Rusher, que existe
    /// como referencia para medir a los demas.
    ///
    /// Datos del guerrero sobre los que se apoyan las tacticas:
    ///  - Barrido: alcance 1.8, abanico 130 grados, 20 de dano, 25 de energia.
    ///  - Estocada: alcance 3.0, linea recta, 30 de dano, 25 de energia.
    ///  - Energia: maximo 50, regenera 12.5/s -> solo DOS acciones seguidas.
    ///  - Parry: GRATIS (solo 0.5 s de recarga), dura 0.35 s y cubre un cono de
    ///    60 grados hacia donde apuntas. Bloquear aturde al atacante 0.55 s.
    ///  - Todo ataque tiene 0.18 s de anticipacion antes de golpear: hay tiempo
    ///    de reaccionar si lo estas vigilando.
    public enum CombatBrain
    {
        /// Referencia: va de frente y golpea en cuanto alcanza.
        Rusher,
        /// Mantiene la distancia justo fuera del barrido rival y solo entra a
        /// estocar cuando el otro esta vendido (recuperandose o sin energia).
        Spacer,
        /// Vive del parry: se ofrece como cebo, bloquea el golpe y castiga el
        /// aturdimiento.
        Counter,
        /// Finge entradas para gastarle el parry y la energia al rival, y
        /// ataca de verdad cuando ha mordido el anzuelo.
        Feinter,
        /// Espera lejos a tener energia llena y entra con dash + golpe; luego
        /// se retira a regenerar.
        Ambusher,
        /// Orbita para atacar desde fuera del cono de parry del rival.
        Flanker,
    }

    public enum DuelClass { Warrior, Lancer, Archer, Monk, Mage }

    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterCombat))]
    [RequireComponent(typeof(CharacterStats))]
    public class DuelistAI : MonoBehaviour
    {
        public CombatBrain brain = CombatBrain.Rusher;
        public Transform foe;

        /// true (lab): se mueve y piensa solo. false (juego): EnemyAI conduce
        /// —patrulla, aggro y movimiento— y solo pide decisiones de combate.
        public bool autoDrive = true;

        /// Distancia que el cerebro quiere mantener ahora mismo (la lee EnemyAI).
        public float DesiredDistance => desiredDistance;
        public bool WantsOrbit => brain == CombatBrain.Flanker;
        public float OrbitSign => orbitSign;

        CharacterMotor motor;
        CharacterCombat combat;
        CharacterStats stats;
        UnitAnimator unitAnimator;
        Rigidbody2D rb;

        // --- Capa de clase: que alcance tiene y como ejecuta cada intencion ---
        public DuelClass Kind { get; private set; }
        float primaryRange;    // alcance del ataque principal
        float secondaryRange;  // alcance del especial
        float safeDistance;    // distancia base preferida por la clase
        bool hasParry;         // el monje NO tiene: su defensa es curarse
        bool isRanged;
        Coroutine holdRoutine; // arquero: mantener y soltar la lluvia

        CharacterCombat foeCombat;
        CharacterStats foeStats;
        CharacterMotor foeMotor;

        float thinkTimer;
        float actionPauseTimer;
        float punishWindow;      // tiempo restante para castigar un parry logrado
        float feintTimer;        // fase actual de la finta
        bool feintAdvancing;
        int feintsDone;
        float orbitSign = 1f;
        float orbitFlipTimer;
        float stalemateTimer;    // sin golpes ni amagos: hay que tomar iniciativa
        float burstTimer;        // Ambusher: entrada ya comprometida

        // Distancia que el cerebro quiere mantener este instante.
        float desiredDistance = 2.6f;

        const float AttackCost = 25f;
        const float iceManaCost = 40f;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<CharacterCombat>();
            stats = GetComponent<CharacterStats>();
            unitAnimator = GetComponent<UnitAnimator>();
            rb = GetComponent<Rigidbody2D>();
            DetectClass();
        }

        /// Perfil de la clase: de aqui salen los alcances y la forma de pelear.
        void DetectClass()
        {
            if (combat is MageCombat)
            {
                Kind = DuelClass.Mage;
                primaryRange = 8.0f;    // proyectil
                secondaryRange = 6.8f;  // circulo magico (max 7)
                safeDistance = 5.5f;
                // El mago cambio el parry por el rayo de hielo: su "defensa"
                // es sembrar espinas entre el y quien se le acerca.
                hasParry = false; isRanged = true;
            }
            else if (combat is ArcherCombat)
            {
                Kind = DuelClass.Archer;
                primaryRange = 10.5f;   // lluvia de flechas (max 11)
                secondaryRange = 5.5f;  // rafaga de 3
                safeDistance = 6.0f;
                hasParry = true; isRanged = true;
            }
            else if (combat is MonkCombat)
            {
                Kind = DuelClass.Monk;
                primaryRange = combat.sweepRange;  // patada, 1.4
                secondaryRange = 7.0f;             // embestida
                safeDistance = 2.0f;
                hasParry = false; isRanged = false; // se cura en vez de parar
            }
            else
            {
                // Guerrero y lancero comparten kit; los distingue el alcance.
                Kind = combat.stabRange > 3.4f ? DuelClass.Lancer : DuelClass.Warrior;
                primaryRange = combat.sweepRange;
                secondaryRange = combat.stabRange;
                safeDistance = combat.sweepRange + 0.75f;
                hasParry = true; isRanged = false;
            }
        }

        void Start()
        {
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            combat.ParryPerformed += OnOwnParrySuccess;
            stats.Damaged += OnDamaged;
            // Desfase inicial para que dos cerebros identicos no queden
            // sincronizados clavados el uno frente al otro.
            stalemateTimer = Random.Range(0f, 1.2f);
            BindFoe();
        }

        void OnDestroy()
        {
            combat.ParryPerformed -= OnOwnParrySuccess;
            stats.Damaged -= OnDamaged;
            if (foeCombat != null) foeCombat.AttackStarted -= OnFoeAttackStarted;
        }

        void OnDamaged(Vector2 _) => stalemateTimer = 0f;

        void BindFoe()
        {
            if (foe == null) return;
            if (foeCombat != null) foeCombat.AttackStarted -= OnFoeAttackStarted;
            foeCombat = foe.GetComponent<CharacterCombat>();
            foeStats = foe.GetComponent<CharacterStats>();
            foeMotor = foe.GetComponent<CharacterMotor>();
            if (foeCombat != null) foeCombat.AttackStarted += OnFoeAttackStarted;
        }

        public void SetFoe(Transform newFoe)
        {
            foe = newFoe;
            BindFoe();
        }

        // ----------------------------------------------------------------
        //  Lectura de la situacion
        // ----------------------------------------------------------------

        float Dist => foe != null ? Vector2.Distance(transform.position, foe.position) : 99f;
        Vector2 ToFoe => foe != null
            ? ((Vector2)foe.position - (Vector2)transform.position).normalized : Vector2.right;

        bool FoeAlive => foeStats != null && !foeStats.IsDead;
        bool CanAct => !combat.IsBusy && !motor.IsDashing;
        bool CanAttack => CanAct && !combat.IsRecovering && stats.Energy >= AttackCost
                          && actionPauseTimer <= 0f;

        /// El rival no puede responder ahora mismo: aturdido, recuperandose del
        /// golpe o sin energia para atacar.
        bool FoeVulnerable => foeCombat != null &&
            (foeCombat.IsStaggered || foeCombat.IsRecovering
             || (foeStats != null && foeStats.Energy < AttackCost));

        /// Estoy fuera del cono de parry del rival (no puede bloquearme).
        bool OutsideFoeParryArc
        {
            get
            {
                if (foeMotor == null || foeCombat == null) return false;
                Vector2 foeToMe = ((Vector2)transform.position - (Vector2)foe.position).normalized;
                return Vector2.Angle(foeMotor.AimDirection, foeToMe) > foeCombat.parryHalfAngle + 12f;
            }
        }

        void OnOwnParrySuccess(CharacterCombat _)
        {
            // Bloqueo logrado: el rival queda aturdido 0.55 s. A castigar.
            punishWindow = 0.5f;
        }

        void OnFoeAttackStarted(CharacterCombat attacker, CharacterCombat.AttackKind kind)
        {
            stalemateTimer = 0f; // hay accion: no hace falta forzar iniciativa
            if (!ReactsToIncoming() || !CanAct || combat.IsRecovering) return;

            float threat = kind == CharacterCombat.AttackKind.Sweep ? 2.4f : 3.6f;
            if (Dist > threat) return;

            float chance = brain == CombatBrain.Counter ? 0.95f
                         : brain == CombatBrain.Feinter ? 0.5f
                         : brain == CombatBrain.Spacer ? 0.45f
                         : 0.25f;
            if (Random.value > chance) return;

            StartCoroutine(ParryReaction());
        }

        // El monje no tiene parry: reaccionar a un golpe entrante no le sirve
        // (curarse tarda 0.8 s y tiene 5 s de recarga).
        bool ReactsToIncoming() => hasParry
            && brain != CombatBrain.Rusher && brain != CombatBrain.Ambusher;

        bool FoeCommitted => foeCombat != null
            && (foeCombat.IsAttacking || foeCombat.IsRecovering || foeCombat.IsStaggered);

        // ----------------------------------------------------------------
        //  Capa de clase: COMO se ejecuta una intencion
        // ----------------------------------------------------------------

        /// Ataca con lo que corresponda a la clase para esta distancia.
        bool Strike(float dist)
        {
            if (!CanAttack || foe == null) return false;
            combat.AimPoint = foe.position;   // clases de area/proyectil
            Vector2 dir = ToFoe;

            switch (Kind)
            {
                case DuelClass.Archer:
                    if (dist <= secondaryRange)
                    {
                        combat.OnSecondaryDown(dir);   // rafaga de 3
                        Pause(0.55f, 0.85f);
                        return true;
                    }
                    if (dist <= primaryRange) { StartArtillery(); return true; }
                    return false;

                case DuelClass.Mage:
                    // Rayo de hielo: como el mana no se regenera solo hay uno
                    // por vida, asi que se guarda para cuando el rival ya esta
                    // encima y las espinas le van a caer de lleno.
                    if (stats.Mana >= iceManaCost && dist <= 6f)
                    {
                        combat.OnSpecial(dir);
                        Pause(0.7f, 1f);
                        return true;
                    }
                    // El circulo pega mas pero avisa medio segundo: solo merece
                    // la pena si el rival ya esta comprometido y no lo esquiva.
                    if (dist <= secondaryRange && FoeCommitted)
                    {
                        combat.OnSecondaryDown(dir);
                        Pause(0.6f, 0.9f);
                        return true;
                    }
                    if (dist <= primaryRange)
                    {
                        combat.OnPrimaryDown(dir);     // proyectil
                        Pause(0.45f, 0.7f);
                        return true;
                    }
                    return false;

                case DuelClass.Monk:
                    if (dist <= primaryRange)
                    {
                        combat.TrySweep(dir);          // patada
                        Pause(0.4f, 0.7f);
                        return true;
                    }
                    if (dist > 2.2f && dist <= secondaryRange)
                    {
                        combat.OnSecondaryDown(dir);   // embestida aturdidora
                        Pause(0.9f, 1.3f);
                        return true;
                    }
                    return false;

                default: // guerrero y lancero
                    if (dist <= primaryRange)
                    {
                        combat.TrySweep(dir);
                        Pause(0.4f, 0.75f);
                        return true;
                    }
                    if (dist <= secondaryRange)
                    {
                        combat.TryStab(dir);
                        Pause(0.45f, 0.85f);
                        return true;
                    }
                    return false;
            }
        }

        /// Defensa propia de la clase: parry, o curacion en el monje.
        bool Defend()
        {
            if (!CanAct) return false;
            if (hasParry) return combat.TryParry(ToFoe);

            if (Kind == DuelClass.Mage)
            {
                // Rayo de hielo hacia el rival: corta el paso y castiga
                // quedarse dentro. Gasta mana, que no se regenera.
                if (stats.Mana < iceManaCost) return false;
                combat.AimPoint = foe != null ? (Vector2)foe.position : combat.AimPoint;
                combat.OnSpecial(ToFoe);
                Pause(0.5f, 0.8f);
                return true;
            }

            if (stats.Health < stats.maxHealth * 0.7f)
            {
                combat.OnSpecial(ToFoe); // curacion del monje (recarga interna)
                return true;
            }
            return false;
        }

        /// Arquero: mantener el apuntado y soltar la lluvia sobre el rival.
        void StartArtillery()
        {
            if (holdRoutine != null) return;
            holdRoutine = StartCoroutine(ArtilleryRoutine());
        }

        IEnumerator ArtilleryRoutine()
        {
            combat.AimPoint = foe != null ? (Vector2)foe.position : combat.AimPoint;
            combat.OnPrimaryDown(ToFoe);

            float t = 0f;
            while (t < 0.3f && foe != null && !stats.IsDead)
            {
                t += Time.deltaTime;
                combat.AimPoint = foe.position;  // sigue al blanco mientras apunta
                yield return null;
            }

            if (foe != null && !stats.IsDead)
            {
                combat.AimPoint = foe.position;
                combat.OnPrimaryUp(ToFoe);
            }
            Pause(0.9f, 1.3f);
            holdRoutine = null;
        }

        IEnumerator ParryReaction()
        {
            // Reflejo humano: no instantaneo, pero dentro de los 0.18 s de aviso.
            yield return new WaitForSeconds(Random.Range(0.04f, 0.11f));
            if (!CanAct || foe == null) yield break;
            combat.TryParry(ToFoe);
        }

        // ----------------------------------------------------------------
        //  Bucle
        // ----------------------------------------------------------------

        void Update()
        {
            if (stats.IsDead)
            {
                if (autoDrive) motor.SetMoveInput(Vector2.zero);
                return;
            }

            // Los temporizadores corren siempre: tambien cuando conduce EnemyAI.
            if (actionPauseTimer > 0f) actionPauseTimer -= Time.deltaTime;
            if (punishWindow > 0f) punishWindow -= Time.deltaTime;
            if (burstTimer > 0f) burstTimer -= Time.deltaTime;
            stalemateTimer += Time.deltaTime;

            if (!autoDrive) return; // el dueno decide cuando pensar y como moverse

            if (foe == null || !FoeAlive)
            {
                motor.SetMoveInput(Vector2.zero);
                return;
            }

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.08f; // reflejos rapidos: son duelistas
                Think();
            }

            Steer();
        }

        /// Una decision de combate a peticion del dueno (EnemyAI). El ritmo con
        /// que se llama es lo que hace a un enemigo mas o menos reflexivo.
        public void ThinkOnce()
        {
            if (stats.IsDead || foe == null || !FoeAlive) return;
            Think();
        }

        void Think()
        {
            // Castigar un parry logrado es prioritario en todos los cerebros.
            if (punishWindow > 0f && Strike(Dist))
            {
                punishWindow = 0f;
                return;
            }

            // Dos cerebros reactivos identicos se quedaban mirandose 30 s sin
            // tocarse: ninguno atacaba porque el otro nunca se exponia. Como en
            // la esgrima real, si nadie se compromete hay que tantear.
            if (TryBreakStalemate()) return;

            switch (brain)
            {
                case CombatBrain.Rusher: ThinkRusher(); break;
                case CombatBrain.Spacer: ThinkSpacer(); break;
                case CombatBrain.Counter: ThinkCounter(); break;
                case CombatBrain.Feinter: ThinkFeinter(); break;
                case CombatBrain.Ambusher: ThinkAmbusher(); break;
                case CombatBrain.Flanker: ThinkFlanker(); break;
            }
        }

        void Pause(float min, float max)
        {
            actionPauseTimer = Random.Range(min, max);
            stalemateTimer = 0f;
        }

        /// Tanteo forzado tras varios segundos sin que pase nada. El Rusher no
        /// lo necesita (siempre entra) y el Ambusher tiene su propio ciclo.
        bool TryBreakStalemate()
        {
            if (brain == CombatBrain.Rusher || brain == CombatBrain.Ambusher) return false;
            if (stalemateTimer < 3.5f) return false;
            if (!CanAttack) return false;

            // Tantear con lo que tenga la clase a esta distancia.
            if (Strike(Dist)) return true;

            // Todavia lejos: acercarse decidido para poder tantear.
            desiredDistance = Mathf.Max(primaryRange, secondaryRange) * 0.8f;
            return false;
        }

        // --- 1. Rusher: referencia agresiva ---
        void ThinkRusher()
        {
            // Se pega al alcance de su mejor golpe y machaca.
            desiredDistance = isRanged ? secondaryRange * 0.75f : primaryRange * 0.7f;
            Strike(Dist);
        }

        // --- 2. Spacer: se queda fuera de peligro y entra solo a lo seguro ---
        void ThinkSpacer()
        {
            // Cuerpo a cuerpo: justo fuera del alcance corto rival pero dentro
            // del propio largo. A distancia: al borde de su alcance util.
            desiredDistance = isRanged ? primaryRange * 0.75f : safeDistance;

            if (!CanAttack) return;

            // Solo se compromete si el rival no puede responder.
            if (FoeVulnerable && Strike(Dist))
            {
                desiredDistance += 0.8f; // y se retira tras picar
                return;
            }

            // Si el rival se le echa encima, golpe defensivo y separacion.
            if (Dist < primaryRange * 0.85f && Strike(Dist))
                desiredDistance += 1.0f;
        }

        // --- 3. Counter: cebo, defensa y castigo ---
        void ThinkCounter()
        {
            // Se queda al alcance del rival para provocarle el ataque.
            desiredDistance = hasParry ? Mathf.Max(1.8f, safeDistance * 0.9f) : safeDistance;

            // Parry preparado en cuanto el rival se compromete de cerca.
            if (hasParry && CanAct && !combat.IsParryActive
                && Dist < safeDistance && foeCombat != null && foeCombat.IsAttacking)
            {
                Defend();
                return;
            }

            // El monje no para: aguanta y se cura cuando le han hecho pupa.
            if (!hasParry && stats.Health < stats.maxHealth * 0.6f && Defend()) return;

            if (!CanAttack) return;

            // Ataca por iniciativa solo si el otro esta seco: ahi no hay contra.
            if (foeStats != null && foeStats.Energy < AttackCost) Strike(Dist);
        }

        // --- 4. Feinter: entradas falsas para gastarle recursos ---
        void ThinkFeinter()
        {
            feintTimer -= 0.08f;
            if (feintTimer <= 0f)
            {
                feintAdvancing = !feintAdvancing;
                feintTimer = feintAdvancing ? Random.Range(0.35f, 0.6f) : Random.Range(0.3f, 0.5f);
                if (feintAdvancing) feintsDone++;
            }

            // Amaga entrando al alcance del rival y sale antes de comprometerse.
            float near = isRanged ? secondaryRange * 0.9f : primaryRange * 1.1f;
            float far = isRanged ? primaryRange * 0.9f : secondaryRange + 0.6f;
            desiredDistance = feintAdvancing ? near : far;

            if (!CanAttack) return;

            // Golpea de verdad cuando el cebo funciono: el rival gasto el parry
            // o la energia, o lleva ya varias fintas tragadas.
            bool foeSpent = foeStats != null && foeStats.Energy < AttackCost;
            bool foeBusy = foeCombat != null && (foeCombat.IsRecovering || foeCombat.IsStaggered);
            bool committed = feintsDone >= 3 && feintAdvancing;

            if ((foeSpent || foeBusy || committed) && Strike(Dist))
                feintsDone = 0;
        }

        // --- 5. Ambusher: espera la barra llena y entra de golpe ---
        void ThinkAmbusher()
        {
            // OJO: el dash gasta la mitad de la barra, asi que tras dashear ya
            // no esta "cargado". Sin este compromiso se daba la vuelta a medio
            // salto y no llegaba a pegar NUNCA (0 de dano en 30 s).
            bool committed = burstTimer > 0f;
            bool loaded = stats.Energy >= 50f || committed;
            float huntRange = Mathf.Max(primaryRange, secondaryRange);
            desiredDistance = loaded ? primaryRange * 0.7f : huntRange * 0.95f;

            if (!loaded || !CanAct) return;

            // A distancia no hace falta cerrar: dispara desde donde esta.
            if (isRanged)
            {
                if (Strike(Dist)) { burstTimer = 0f; desiredDistance = huntRange * 0.95f; }
                return;
            }

            // El monje tiene su propio cierre de distancia: la embestida.
            if (Kind == DuelClass.Monk && Dist > primaryRange && Dist <= secondaryRange)
            {
                if (Strike(Dist)) burstTimer = 1.4f;
                return;
            }

            // Cuerpo a cuerpo: dash para cerrar y rematar.
            if (Dist > secondaryRange && Dist < secondaryRange + 3.5f && !motor.IsDashing)
            {
                motor.AimDirection = ToFoe;
                if (motor.TryDash()) burstTimer = 1.4f; // comprometido a rematar
                return;
            }

            if (Strike(Dist))
            {
                burstTimer = 0f;
                desiredDistance = huntRange * 0.95f; // y a regenerar lejos
            }
        }

        // --- 6. Flanker: golpea desde fuera del cono de parry ---
        void ThinkFlanker()
        {
            desiredDistance = isRanged ? secondaryRange * 0.85f : safeDistance * 0.9f;

            orbitFlipTimer -= 0.08f;
            if (orbitFlipTimer <= 0f)
            {
                orbitFlipTimer = Random.Range(1.2f, 2.4f);
                if (Random.value < 0.35f) orbitSign = -orbitSign;
            }

            if (!CanAttack) return;

            // El parry solo cubre 60 grados frontales: si estoy en su costado,
            // el bloqueo no existe y el golpe entra seguro. OJO: un rival que
            // te encara siempre nunca deja ese hueco, asi que el momento real
            // es cuando esta COMPROMETIDO (atacando o recuperandose): ahi su
            // orientacion queda clavada y el costado se abre.
            if ((OutsideFoeParryArc || FoeCommitted) && Strike(Dist)) return;

            if (FoeVulnerable) Strike(Dist);
        }

        // ----------------------------------------------------------------
        //  Movimiento
        // ----------------------------------------------------------------

        void Steer()
        {
            if (combat.IsStaggered) { motor.SetMoveInput(Vector2.zero); return; }

            Vector2 pos = transform.position;
            Vector2 dir = ToFoe;
            motor.AimDirection = dir;  // siempre encarado (el parry es frontal)
            if (!combat.IsBusy) unitAnimator?.SetFacing(dir.x);

            float dist = Dist;
            Vector2 desired = Vector2.zero;

            // Acercarse / alejarse hasta la distancia que pide el cerebro.
            float gap = dist - desiredDistance;
            if (Mathf.Abs(gap) > 0.25f)
                desired = dir * Mathf.Sign(gap);

            // El Flanker ademas orbita para buscar el costado.
            if (brain == CombatBrain.Flanker)
            {
                Vector2 tangent = new Vector2(-dir.y, dir.x) * orbitSign;
                desired = (desired + tangent * 1.3f).normalized;
            }
            // Los demas se mueven en lateral un poco para no ser blanco fijo.
            else if (brain != CombatBrain.Rusher && Mathf.Abs(gap) <= 0.6f)
            {
                Vector2 tangent = new Vector2(-dir.y, dir.x) * orbitSign;
                desired = (desired + tangent * 0.8f).normalized;
            }

            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired = AvoidWalls(desired);
            motor.SetMoveInput(desired);
        }

        static readonly RaycastHit2D[] wallBuffer = new RaycastHit2D[8];

        Vector2 AvoidWalls(Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return desired;
            if (!WallAhead(desired)) return desired;
            Vector2 left = new Vector2(-desired.y, desired.x);
            return WallAhead(left) ? (desired - left).normalized : (desired + left).normalized;
        }

        bool WallAhead(Vector2 dir)
        {
            int count = rb.Cast(dir, wallBuffer, 0.8f);
            for (int i = 0; i < count; i++)
            {
                var col = wallBuffer[i].collider;
                if (col == null) continue;
                var hitRb = col.attachedRigidbody;
                if (hitRb == null || hitRb.bodyType == RigidbodyType2D.Static) return true;
            }
            return false;
        }
    }
}
