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
        /// Red neuronal evolucionada: no tiene reglas escritas, decide movimiento
        /// y accion a partir de lo que observa. Requiere asignarle un genoma.
        Neural,
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

        /// Tiempo que tarda la IA en "darse cuenta" de lo que ve. NO es un
        /// intervalo de pensamiento: es que actua sobre el estado del mundo de
        /// hace 'reactionDelay' segundos. Sin esto la IA para cualquier golpe
        /// (la anticipacion dura 0.18 s y ella reaccionaba en 0.05) y apunta con
        /// precision imposible, lo que resulta injusto contra una persona.
        public float reactionDelay = 0.22f;

        struct Perception
        {
            public float time;
            public Vector2 foePos;
            public Vector2 foeAim;
            public float foeHealth, foeMaxHealth, foeEnergy;
            public bool attacking, recovering, parrying, staggered, dead;
            public bool valid;
        }

        const int PerceptionSlots = 64;
        readonly Perception[] memory = new Perception[PerceptionSlots];
        int memoryHead = -1;
        Perception perceived;

        float thinkTimer;
        float actionPauseTimer;
        float anticipateTimer;   // espaciado entre parries por lectura
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

        /// Clase de un combatiente cualquiera, por el tipo de su componente de
        /// combate (guerrero y lancero comparten kit; los separa el alcance).
        public static DuelClass ClassOf(CharacterCombat c)
        {
            if (c is MageCombat) return DuelClass.Mage;
            if (c is ArcherCombat) return DuelClass.Archer;
            if (c is MonkCombat) return DuelClass.Monk;
            return c.stabRange > 3.4f ? DuelClass.Lancer : DuelClass.Warrior;
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

        void OnDamaged(Vector2 _)
        {
            stalemateTimer = 0f;
            lastHealth = Mathf.Min(lastHealth, stats.Health);
        }

        float lastHealth = -1f;

        /// Contabiliza el dano recibido comparando la vida entre frames: sirve
        /// para cualquier fuente (golpe, espinas, area) sin tocar el combate.
        void TrackDamage()
        {
            if (lastHealth < 0f) { lastHealth = stats.Health; return; }
            if (stats.Health < lastHealth) DamageTaken += lastHealth - stats.Health;
            lastHealth = stats.Health;

            if (foeStats == null) return;
            if (foeLastHealth < 0f) { foeLastHealth = foeStats.Health; return; }
            if (foeStats.Health < foeLastHealth) DamageDealt += foeLastHealth - foeStats.Health;
            foeLastHealth = foeStats.Health;
        }

        float foeLastHealth = -1f;

        void BindFoe()
        {
            if (foe == null) return;
            if (foeCombat != null) foeCombat.AttackStarted -= OnFoeAttackStarted;
            foeCombat = foe.GetComponent<CharacterCombat>();
            foeStats = foe.GetComponent<CharacterStats>();
            foeMotor = foe.GetComponent<CharacterMotor>();
            if (foeCombat != null) foeCombat.AttackStarted += OnFoeAttackStarted;
            foeClass = foeCombat != null ? (int)ClassOf(foeCombat) : -1;

            // La memoria del rival anterior no vale para este: si se conservara,
            // durante 'reactionDelay' se estaria leyendo la posicion de otro.
            for (int i = 0; i < PerceptionSlots; i++) memory[i].valid = false;
            memoryHead = -1;
            perceived = default;
            closingSpeed = 0f;
        }

        public void SetFoe(Transform newFoe)
        {
            // EnemyAI reafirma su rival en cada ciclo: si no ha cambiado, no
            // hay nada que reatar (y sobre todo, nada que olvidar: borrar la
            // memoria de percepcion aqui anularia el retardo de reaccion).
            if (newFoe == foe) return;
            foe = newFoe;
            BindFoe();
        }

        // ----------------------------------------------------------------
        //  Percepcion retardada
        // ----------------------------------------------------------------

        /// Guarda una foto del rival y deja en 'perceived' la de hace
        /// 'reactionDelay' segundos. TODO lo que consulta el cerebro pasa por
        /// aqui, asi que ninguna rama puede saltarse el retardo leyendo el
        /// estado real: ni las decisiones ni la punteria.
        void SamplePerception()
        {
            var shot = new Perception { time = Time.time, valid = true };
            if (foe != null)
            {
                shot.foePos = foe.position;
                shot.foeAim = foeMotor != null ? foeMotor.AimDirection : Vector2.right;
                if (foeStats != null)
                {
                    shot.foeHealth = foeStats.Health;
                    shot.foeMaxHealth = foeStats.maxHealth;
                    shot.foeEnergy = foeStats.Energy;
                    shot.dead = foeStats.IsDead;
                }
                if (foeCombat != null)
                {
                    shot.attacking = foeCombat.IsAttacking;
                    shot.recovering = foeCombat.IsRecovering;
                    shot.parrying = foeCombat.IsParryActive;
                    shot.staggered = foeCombat.IsStaggered;
                }
            }

            memoryHead = (memoryHead + 1) % PerceptionSlots;
            memory[memoryHead] = shot;

            // La foto mas reciente que YA tenga la antiguedad pedida. Si aun no
            // hay tanta historia se usa la mas vieja disponible.
            float target = Time.time - reactionDelay;
            perceived = shot;
            int found = memoryHead;
            for (int i = 0; i < PerceptionSlots; i++)
            {
                int idx = memoryHead - i;
                if (idx < 0) idx += PerceptionSlots;
                if (!memory[idx].valid) break;
                perceived = memory[idx];
                found = idx;
                if (memory[idx].time <= target) break;
            }

            // Un paso mas atras, para medir a que velocidad se acerca el rival.
            int older = found - 1;
            if (older < 0) older += PerceptionSlots;
            if (memory[older].valid && perceived.time > memory[older].time)
            {
                Vector2 me = transform.position;
                float dNow = Vector2.Distance(me, perceived.foePos);
                float dOld = Vector2.Distance(me, memory[older].foePos);
                closingSpeed = (dOld - dNow) / (perceived.time - memory[older].time);
            }
            else closingSpeed = 0f;
        }

        float closingSpeed;

        /// A que velocidad se me echa encima el rival (positivo = viene a por
        /// mi). Con el retardo puesto, enterarse del ataque llega despues del
        /// impacto, asi que esto es lo unico con lo que se puede ANTICIPAR un
        /// golpe. Es exactamente lo que lee una persona: el que cierra la
        /// distancia rapido va a pegar.
        float FoeClosingSpeed => closingSpeed;

        // ----------------------------------------------------------------
        //  Lectura de la situacion (siempre a traves de 'perceived')
        // ----------------------------------------------------------------

        float Dist => foe != null && perceived.valid
            ? Vector2.Distance(transform.position, perceived.foePos) : 99f;
        Vector2 ToFoe => foe != null && perceived.valid
            ? (perceived.foePos - (Vector2)transform.position).normalized : Vector2.right;

        /// Donde CREE la IA que esta el rival. Apuntar aqui en vez de a la
        /// posicion real es lo que hace que un blanco en movimiento sea dificil.
        Vector2 AimTarget => foe != null && perceived.valid
            ? perceived.foePos
            : (foe != null ? (Vector2)foe.position : combat.AimPoint);

        // La muerte se lee en directo a proposito: retrasarla haria que la IA
        // siguiera golpeando cadaveres y enredaria el fin de los duelos.
        bool FoeAlive => foeStats != null && !foeStats.IsDead;
        bool CanAct => !combat.IsBusy && !motor.IsDashing;
        bool CanAttack => CanAct && !combat.IsRecovering && stats.Energy >= AttackCost
                          && actionPauseTimer <= 0f;

        /// El rival no puede responder ahora mismo: aturdido, recuperandose del
        /// golpe o sin energia para atacar.
        bool FoeVulnerable => foe != null && perceived.valid &&
            (perceived.staggered || perceived.recovering || perceived.foeEnergy < AttackCost);

        /// Estoy fuera del cono de parry del rival (no puede bloquearme).
        bool OutsideFoeParryArc
        {
            get
            {
                if (foeMotor == null || foeCombat == null || !perceived.valid) return false;
                Vector2 foeToMe = ((Vector2)transform.position - perceived.foePos).normalized;
                return Vector2.Angle(perceived.foeAim, foeToMe) > foeCombat.parryHalfAngle + 12f;
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

        bool FoeCommitted => foe != null && perceived.valid
            && (perceived.attacking || perceived.recovering || perceived.staggered);

        // ----------------------------------------------------------------
        //  Capa de clase: COMO se ejecuta una intencion
        // ----------------------------------------------------------------

        /// Ataca con lo que corresponda a la clase para esta distancia.
        bool Strike(float dist)
        {
            if (!CanAttack || foe == null) return false;
            combat.AimPoint = AimTarget;   // clases de area/proyectil
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
                combat.AimPoint = AimTarget;
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
            combat.AimPoint = AimTarget;
            combat.OnPrimaryDown(ToFoe);

            float t = 0f;
            while (t < 0.3f && foe != null && !stats.IsDead)
            {
                t += Time.deltaTime;
                combat.AimPoint = AimTarget;  // sigue al blanco mientras apunta
                yield return null;
            }

            if (foe != null && !stats.IsDead)
            {
                combat.AimPoint = AimTarget;
                combat.OnPrimaryUp(ToFoe);
            }
            Pause(0.9f, 1.3f);
            holdRoutine = null;
        }

        IEnumerator ParryReaction()
        {
            // Tiempo de reflejo completo. Como la anticipacion del golpe dura
            // 0.18 s, esto casi siempre llega TARDE para el golpe que lo
            // disparo: la IA solo para de reflejo los encadenados, igual que una
            // persona. Los parries que si entran salen de la anticipacion por
            // distancia (ver Defend), no de este atajo.
            yield return new WaitForSeconds(reactionDelay * Random.Range(0.85f, 1.2f));
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
            if (anticipateTimer > 0f) anticipateTimer -= Time.deltaTime;
            stalemateTimer += Time.deltaTime;
            TrackDamage();

            // Antes que cualquier decision, y tambien cuando conduce EnemyAI:
            // ThinkOnce() debe encontrar la percepcion de este frame.
            SamplePerception();

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
                case CombatBrain.Neural: ThinkNeural(); break;
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

            // Parry ANTICIPADO: se adelanta al golpe leyendo que el rival cierra
            // la distancia, porque enterarse del ataque ya le llegaria tarde.
            // Si se equivoca paga la recarga de 0.5 s, que es el riesgo que
            // corre tambien una persona al bloquear por lectura.
            bool lunging = FoeClosingSpeed > 1.2f && Dist < safeDistance + 1.2f;
            if (hasParry && CanAct && !combat.IsParryActive && anticipateTimer <= 0f
                && (lunging || (Dist < safeDistance && perceived.attacking)))
            {
                // Una lectura de vez en cuando, no un muro permanente: con 0.35 s
                // de parry y 0.5 s de recarga, bloquear sin pausa dejaria al
                // Counter sin atacar nunca y el duelo no acabaria.
                anticipateTimer = Random.Range(1.1f, 1.8f);
                Defend();
                return;
            }

            // El monje no para: aguanta y se cura cuando le han hecho pupa.
            if (!hasParry && stats.Health < stats.maxHealth * 0.6f && Defend()) return;

            if (!CanAttack) return;

            // Ataca por iniciativa solo si el otro esta seco: ahi no hay contra.
            if (perceived.valid && perceived.foeEnergy < AttackCost) Strike(Dist);
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
            bool foeSpent = perceived.valid && perceived.foeEnergy < AttackCost;
            bool foeBusy = perceived.valid && (perceived.recovering || perceived.staggered);
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
        //  Cerebro neuronal
        // ----------------------------------------------------------------

        /// Red asignada por el entrenador. Sin ella el cerebro Neural no hace
        /// nada (no se inventa comportamiento).
        public AI.NeuralNet net;

        /// Estadisticas del duelo, las lee el entrenador para la aptitud.
        public float DamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public int ActionsTaken { get; private set; }

        // 15 de combate + 8 sensores de terreno + 5 de clase del rival.
        public const int ObservationCount = 28;

        // Clase del rival como indice 0-4, o -1 si no hay rival atado.
        int foeClass = -1;
        public const int ActionCount = 6;

        readonly float[] observation = new float[ObservationCount];
        Vector2 neuralMove;

        /// Vector de movimiento decidido por la red, ya pasado por el esquive
        /// de muros. Lo consume EnemyAI cuando conduce el a un cerebro Neural.
        public Vector2 NeuralMove => AvoidWalls(neuralMove);

        public void ResetDuelStats()
        {
            DamageDealt = DamageTaken = 0f;
            ActionsTaken = 0;
            neuralMove = Vector2.zero;
        }

        /// Lo llama el entrenador cuando este duelista hiere al rival.
        public void ReportDamageDealt(float amount) => DamageDealt += amount;

        void ThinkNeural()
        {
            if (net == null) return;

            BuildObservation();
            var output = net.Evaluate(observation);

            // Salidas 0-1: direccion de movimiento. 2-5: ganas de cada accion.
            neuralMove = new Vector2(output[0], output[1]);
            if (neuralMove.sqrMagnitude > 1f) neuralMove.Normalize();
            else if (neuralMove.magnitude < 0.12f) neuralMove = Vector2.zero; // zona muerta

            if (!CanAct || actionPauseTimer > 0f) return;

            int best = -1;
            float bestScore = 0.15f; // umbral: por debajo, no hace nada
            for (int i = 2; i < output.Length; i++)
                if (output[i] > bestScore) { bestScore = output[i]; best = i; }
            if (best < 0) return;

            float dist = Dist;
            Vector2 dir = ToFoe;
            combat.AimPoint = AimTarget;

            bool did = false;
            switch (best)
            {
                case 2: did = ExecutePrimary(dist, dir); break;
                case 3: did = ExecuteSecondary(dist, dir); break;
                case 4: did = ExecuteSpecial(dir); break;
                case 5:
                    motor.AimDirection = neuralMove.sqrMagnitude > 0.01f ? neuralMove : dir;
                    did = motor.TryDash();
                    break;
            }
            if (did)
            {
                ActionsTaken++;
                Pause(0.12f, 0.22f); // freno minimo: el resto lo decide la red
            }
        }

        bool ExecutePrimary(float dist, Vector2 dir)
        {
            switch (Kind)
            {
                case DuelClass.Archer:
                    if (dist > primaryRange) return false;
                    StartArtillery(); return true;
                case DuelClass.Mage:
                    if (dist > primaryRange) return false;
                    combat.OnPrimaryDown(dir); return true;
                default:
                    if (dist > primaryRange) return false;
                    return combat.TrySweep(dir);
            }
        }

        bool ExecuteSecondary(float dist, Vector2 dir)
        {
            switch (Kind)
            {
                case DuelClass.Archer:
                case DuelClass.Mage:
                case DuelClass.Monk:
                    if (dist > secondaryRange) return false;
                    combat.OnSecondaryDown(dir); return true;
                default:
                    if (dist > secondaryRange) return false;
                    return combat.TryStab(dir);
            }
        }

        bool ExecuteSpecial(Vector2 dir)
        {
            if (hasParry) return combat.TryParry(dir);
            combat.OnSpecial(dir); // curacion del monje o rayo del mago
            return true;
        }

        /// Lo que la red "ve". Todo normalizado a ~[-1, 1] para que ningun
        /// canal domine por escala.
        void BuildObservation()
        {
            float dist = Dist;
            Vector2 dir = ToFoe;

            observation[0] = Mathf.Clamp(dist / 10f, 0f, 1.5f);
            observation[1] = dir.x;
            observation[2] = dir.y;
            observation[3] = stats.maxHealth > 0f ? stats.Health / stats.maxHealth : 0f;
            observation[4] = stats.maxEnergy > 0f ? stats.Energy / stats.maxEnergy : 0f;
            observation[5] = stats.MaxMana > 0f ? stats.Mana / stats.MaxMana : 0f;
            // Canales 6..10: el rival, tal y como se percibio hace
            // 'reactionDelay'. La red NO ve el estado real por el mismo motivo
            // que no lo ve una persona.
            observation[6] = perceived.foeMaxHealth > 0f
                ? perceived.foeHealth / perceived.foeMaxHealth : 0f;
            observation[7] = foeStats != null && foeStats.maxEnergy > 0f
                ? perceived.foeEnergy / foeStats.maxEnergy : 0f;
            observation[8] = perceived.attacking ? 1f : 0f;
            observation[9] = perceived.recovering ? 1f : 0f;
            observation[10] = perceived.parrying ? 1f : 0f;
            // El propio estado si es inmediato: uno sabe lo que esta haciendo.
            observation[11] = combat.IsRecovering ? 1f : 0f;
            observation[12] = OutsideFoeParryArc ? 1f : 0f;
            // Alcance relativo: informa de si su clase pega mas lejos que tu.
            observation[13] = Mathf.Clamp((secondaryRange - dist) / 4f, -1f, 1f);
            // Velocidad de acercamiento: la unica pista con la que la red puede
            // aprender a anticipar en lugar de reaccionar tarde.
            observation[14] = Mathf.Clamp(FoeClosingSpeed / 5f, -1f, 1f);
            // Y el terreno. Sin esto la red decide a ciegas y AvoidWalls le
            // corrige el movimiento por detras: la misma entrada acaba dando
            // resultados distintos segun una geometria que no ve, que es ruido
            // que impide aprender a acorralar o a no dejarse acorralar.
            SenseWalls(dir, observation, 15);

            // 23..27: la clase del rival, uno-de-cinco. Es informacion
            // perceptiva (el jugador tambien la lee del sprite de un vistazo)
            // y no pasa por el retardo porque la clase no cambia en combate.
            // Sin esto la red solo puede aprender una politica promedio, y
            // "entrarle encima al arquero" y "no regalarle el cuerpo a cuerpo
            // al monje" son decisiones opuestas ante la misma foto.
            for (int i = 0; i < 5; i++) observation[23 + i] = 0f;
            if (foeClass >= 0 && foeClass < 5) observation[23 + foeClass] = 1f;
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

            // La red conduce directamente: no hay "distancia deseada" que
            // interpretar, ella decide el vector de movimiento.
            if (brain == CombatBrain.Neural)
            {
                motor.SetMoveInput(AvoidWalls(neuralMove));
                return;
            }

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

        public const int WhiskerCount = 8;
        const float WhiskerRange = 3f;
        static readonly Vector2[] whiskerDirs = new Vector2[WhiskerCount];
        static readonly float[] whiskerDist = new float[WhiskerCount];

        /// Distancia libre en 8 direcciones alrededor, tomadas RELATIVAS a
        /// donde esta el rival: la 0 apunta a el y la 4 a mi espalda. Al ser
        /// relativas, la lectura no depende de la orientacion de la arena y la
        /// red aprende "tengo la pared detras" con muy pocos pesos.
        void SenseWalls(Vector2 forward, float[] into, int offset)
        {
            if (forward.sqrMagnitude < 0.01f) forward = Vector2.right;
            for (int i = 0; i < WhiskerCount; i++)
            {
                float ang = Mathf.PI * 2f * i / WhiskerCount;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                whiskerDirs[i] = new Vector2(forward.x * cos - forward.y * sin,
                                             forward.x * sin + forward.y * cos);
                whiskerDist[i] = WallDistance(whiskerDirs[i]);
                // 1 = pared pegada, 0 = despejado.
                if (into != null)
                    into[offset + i] = 1f - Mathf.Clamp01(whiskerDist[i] / WhiskerRange);
            }
        }

        float WallDistance(Vector2 dir)
        {
            int count = rb.Cast(dir, wallBuffer, WhiskerRange);
            float best = WhiskerRange;
            for (int i = 0; i < count; i++)
            {
                var col = wallBuffer[i].collider;
                if (col == null) continue;
                var hitRb = col.attachedRigidbody;
                // Solo cuenta el escenario: los otros luchadores no son muro.
                if (hitRb != null && hitRb.bodyType != RigidbodyType2D.Static) continue;
                if (wallBuffer[i].distance < best) best = wallBuffer[i].distance;
            }
            return best;
        }

        /// Aparta el rumbo de las paredes de forma CONTINUA. La version anterior
        /// giraba 90 grados de golpe al detectar muro a 0.8 unidades, y contra
        /// una pared curva eso oscilaba y dejaba al personaje pegado sin poder
        /// salir. Ahora cada sensor empuja en proporcion a lo cerca que esta,
        /// asi que el resultado es deslizarse a lo largo del muro.
        Vector2 AvoidWalls(Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return desired;

            SenseWalls(desired, null, 0);

            Vector2 push = Vector2.zero;
            float worst = 0f;
            for (int i = 0; i < WhiskerCount; i++)
            {
                float near = 1f - Mathf.Clamp01(whiskerDist[i] / 1.6f);
                if (near <= 0f) continue;
                push -= whiskerDirs[i] * near * near;
                if (near > worst) worst = near;
            }

            if (worst <= 0f) return desired;

            // Cuanto mas encajonado, mas manda el escape sobre la intencion.
            Vector2 blended = desired + push * (1.2f + worst * 2f);
            if (blended.sqrMagnitude < 0.04f)
            {
                // Empotrado de frente: sal por el lado mas despejado.
                Vector2 side = new Vector2(-desired.y, desired.x);
                blended = WallDistance(side) >= WallDistance(-side) ? side : -side;
            }
            return blended.normalized;
        }
    }
}
