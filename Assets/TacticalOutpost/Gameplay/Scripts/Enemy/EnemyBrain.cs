using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Player;

namespace Outpost.Enemy
{
    /// <summary>
    /// Otak zombie: persepsi (lihat + dengar + ingat) -> pilih target (player atau generator, berbasis skor utility)
    /// -> bergerak lewat NavMeshAgent -> menyerang. Semua angka ada di EnemyConfig.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyBrain : MonoBehaviour
    {
        public enum State { Spawning, Advance, Chase, AttackPlayer, AttackGenerator, Stagger, Dead }
        enum TargetType { None, Player, Generator }

        public EnemyConfig config;
        public Animator animator;

        public State CurrentState { get { return state; } }
        public bool IsAware { get { return Time.time - lastSeen < (config != null ? config.memoryTime : 8f); } }

        // reservasi titik serang generator: 1 titik = 1 zombie
        static readonly Dictionary<Transform, EnemyBrain> reserved = new Dictionary<Transform, EnemyBrain>();
        static int blockMask = -1;

        NavMeshAgent agent;
        NavMeshQueryFilter NavigationFilter { get { return new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }; } }
        NavMeshPath navigationPath;
        EnemyHealth health;
        State state = State.Advance;
        TargetType targetType = TargetType.None;
        GeneratorHealth targetGen;
        Transform attackPoint;
        Vector3 lastDest = new Vector3(1e6f, 0, 0);

        Vector3 lastKnownPos;
        float lastSeen = -999f;
        bool playerVisible;

        float nextThink, stateTimer, nextAttackTime, hitTime;
        bool pendingHit;
        float speedParam;
        bool enraged;
        float cooldownMul = 1f, speedMul = 1f;
        PlayerHealth playerHp;
        Transform playerTr;

        static readonly int pSpeed = Animator.StringToHash("Speed");
        static readonly int pAttack = Animator.StringToHash("Attack");
        static readonly int pScream = Animator.StringToHash("Scream");
        static readonly int pDie = Animator.StringToHash("Die");
        static readonly int pDeathType = Animator.StringToHash("DeathType");
        static readonly int pAltWalk = Animator.StringToHash("AltWalk");

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            navigationPath = new NavMeshPath();
            health = GetComponent<EnemyHealth>();
            if (config == null) config = health.config;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (blockMask < 0)
                blockMask = ~((1 << 9) | (1 << 8) | (1 << 14) | (1 << 2) | (1 << 5));   // abaikan Enemy, Player, Marker, IgnoreRaycast, UI

            agent.speed = config.walkSpeed;
            agent.acceleration = config.acceleration;
            agent.angularSpeed = config.angularSpeed;
            agent.stoppingDistance = Mathf.Max(0.2f, config.attackRange * 0.6f);
            agent.autoBraking = true;
            agent.avoidancePriority = Random.Range(30, 70);

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = config.animSpeed;
                animator.SetBool(pAltWalk, Random.value < 0.5f);
            }
            ApplyTint();
        }

        void ApplyTint()
        {
            if (config.tint == Color.white) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", config.tint);
                r.SetPropertyBlock(mpb);
            }
        }

        void OnEnable()
        {
            health.OnDamaged += OnDamaged;
            health.OnDied += OnDied;
            FpsWeapon.NoiseEmitted += OnNoise;
        }

        void OnDisable()
        {
            health.OnDamaged -= OnDamaged;
            health.OnDied -= OnDied;
            FpsWeapon.NoiseEmitted -= OnNoise;
            ReleaseTarget();
        }

        void Start()
        {
            if (health.IsDead) return;
            if (!agent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 1.5f, NavigationFilter)) agent.Warp(hit.position);
                else { Debug.LogWarning("[Enemy] " + name + " tidak berada di NavMesh, dinonaktifkan."); enabled = false; return; }
            }
            nextThink = Time.time + Random.Range(0f, 0.2f);
            if (config.screamOnSpawn) EnterSpawning(config.screamDuration);
            else state = State.Advance;
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            if (state == State.Dead || !agent.enabled || !agent.isOnNavMesh) return;
            UpdateAnimatorSpeed();

            switch (state)
            {
                case State.Spawning:
                case State.Stagger:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) { agent.isStopped = false; state = State.Advance; nextThink = 0f; }
                    break;

                case State.AttackPlayer:
                case State.AttackGenerator:
                    TickAttack();
                    break;

                default:
                    if (Time.time >= nextThink) { nextThink = Time.time + 0.15f; Think(); }
                    break;
            }
        }

        void UpdateAnimatorSpeed()
        {
            if (animator == null) return;
            float v = agent.enabled ? agent.velocity.magnitude : 0f;
            float walk = Mathf.Max(0.1f, config.walkSpeed), run = Mathf.Max(walk + 0.1f, config.runSpeed);
            // petakan kecepatan nyata ke ambang blend tree: 0 / 1.2 (jalan) / 3.4 (lari)
            float s = v <= walk ? v / walk * 1.2f : 1.2f + (v - walk) / (run - walk) * (3.4f - 1.2f);
            speedParam = Mathf.Lerp(speedParam, s, 10f * Time.deltaTime);
            animator.SetFloat(pSpeed, speedParam);
        }

        // ---------------------------------------------------------------- persepsi

        void FindPlayer()
        {
            if (playerHp == null) { playerHp = PlayerHealth.Instance; playerTr = playerHp != null ? playerHp.transform : null; }
        }

        void UpdatePerception()
        {
            FindPlayer();
            playerVisible = false;
            if (playerHp == null || playerHp.IsDead) return;
            Vector3 eye = transform.position + Vector3.up * (1.6f * config.visualScale * transform.localScale.y);
            Vector3 chest = playerTr.position + Vector3.up * 1.2f;
            Vector3 to = chest - eye;
            float dist = to.magnitude;
            if (dist > config.sightRadius) return;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            bool inCone = dist <= config.closeSenseRadius || Vector3.Angle(transform.forward, flat) <= config.sightFov * 0.5f;
            if (!inCone) return;
            if (Physics.Linecast(eye, chest, blockMask, QueryTriggerInteraction.Ignore)) return;
            playerVisible = true;
            lastKnownPos = playerTr.position;
            lastSeen = Time.time;
        }

        void OnNoise(Vector3 pos, float radius)
        {
            if (state == State.Dead || config == null) return;
            if ((pos - transform.position).magnitude > radius * config.hearingMultiplier) return;
            Vector2 j = Random.insideUnitCircle * 2f;
            lastKnownPos = pos + new Vector3(j.x, 0f, j.y);
            lastSeen = Mathf.Max(lastSeen, Time.time - config.memoryTime * 0.4f);   // ingatan suara lebih lemah dari melihat langsung
        }

        // ---------------------------------------------------------------- keputusan

        void Think()
        {
            UpdatePerception();

            float playerScore = 0f;
            float memAge = Time.time - lastSeen;
            bool mem = memAge < config.memoryTime;
            float dPlayer = playerTr != null ? Vector3.Distance(transform.position, playerTr.position) : 999f;
            bool playerAlive = playerHp != null && !playerHp.IsDead;
            if (playerAlive)
            {
                if (playerVisible) playerScore = 0.6f + 0.4f * Mathf.Clamp01(1f - dPlayer / config.sightRadius);
                else if (mem) playerScore = 0.5f * (1f - memAge / config.memoryTime);
                if (dPlayer < 5f) playerScore = 1f;
                if (config.kind == EnemyKind.Boss && (playerVisible || mem || dPlayer < 5f)) playerScore += 0.2f;
            }

            GeneratorHealth bestGen = null; float bestGenScore = -1f;
            foreach (var g in GeneratorHealth.All)
            {
                if (g == null || !g.IsAlive) continue;
                float d = Vector3.Distance(transform.position, g.transform.position);
                float s = 0.45f + 0.3f / (1f + d / 25f) + 0.25f * (1f - g.Fraction) - 0.12f * (g.attackers - (g == targetGen ? 1 : 0));
                if (g == targetGen) s += 0.08f;                              // hysteresis supaya tidak plin-plan
                if (s > bestGenScore) { bestGenScore = s; bestGen = g; }
            }

            bool playerKnown = playerAlive && (playerVisible || mem || dPlayer < 5f);
            bool goPlayer = playerKnown && (playerScore > bestGenScore || bestGen == null);
            if (state == State.Chase && targetType == TargetType.Player) playerScore += 0.08f;
            goPlayer = playerKnown && (playerScore > bestGenScore || bestGen == null);

            if (goPlayer)
            {
                SetTargetPlayer();
                Vector3 dest = playerVisible ? playerTr.position : (mem ? lastKnownPos : playerTr.position);
                if (!playerVisible && mem && Flat(dest - transform.position).magnitude < 1.6f) lastSeen = -999f;   // tiba di titik terakhir, tidak ada apa-apa
                MoveTo(dest, true);
                state = State.Chase;
                // serang bila dalam jangkauan
                if (playerVisible || dPlayer < config.attackRange + 0.6f)
                {
                    float flatD = Flat(playerTr.position - transform.position).magnitude;
                    if (flatD <= config.attackRange && Mathf.Abs(playerTr.position.y - transform.position.y) < 1.8f && Time.time >= nextAttackTime) BeginAttack(TargetType.Player);
                }
            }
            else if (bestGen != null)
            {
                SetTargetGenerator(bestGen);
                if (attackPoint == null) PickAttackPoint();
                Vector3 dest = attackPoint != null ? attackPoint.position : bestGen.transform.position;
                MoveTo(dest, playerVisible || IsAware);
                state = State.Advance;
                if (targetGen.Body != null)
                {
                    Vector3 cp = targetGen.Body.ClosestPoint(transform.position);
                    if (Flat(cp - transform.position).magnitude <= config.attackRange + 0.5f && Time.time >= nextAttackTime) BeginAttack(TargetType.Generator);
                }
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void MoveTo(Vector3 dest, bool run)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = (run ? config.runSpeed : config.walkSpeed) * speedMul;
            if ((dest - lastDest).sqrMagnitude > 0.25f)
            {
                lastDest = dest;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(dest, out hit, 3f, NavigationFilter)) agent.SetDestination(hit.position);
                else agent.SetDestination(dest);
            }
        }

        void SetTargetPlayer()
        {
            if (targetType == TargetType.Generator) ReleaseTarget();
            targetType = TargetType.Player;
        }

        void SetTargetGenerator(GeneratorHealth g)
        {
            if (targetGen != g) { ReleaseTarget(); targetGen = g; g.attackers++; }
            targetType = TargetType.Generator;
        }

        void ReleaseTarget()
        {
            if (targetGen != null) { targetGen.attackers = Mathf.Max(0, targetGen.attackers - 1); targetGen = null; }
            if (attackPoint != null) { EnemyBrain o; if (reserved.TryGetValue(attackPoint, out o) && o == this) reserved.Remove(attackPoint); attackPoint = null; }
            targetType = TargetType.None;
        }

        void PickAttackPoint()
        {
            var cands = new List<Transform>(targetGen.AttackPoints);
            Transform best = null; float bd = float.MaxValue;
            foreach (var t in cands)
            {
                if (t == null) continue;
                EnemyBrain o;
                if (reserved.TryGetValue(t, out o) && o != null && o != this && o.state != State.Dead) continue;
                float d = (t.position - transform.position).sqrMagnitude;
                if (!NavMesh.SamplePosition(t.position, out NavMeshHit hit, 1.5f, NavigationFilter) ||
                    !agent.CalculatePath(hit.position, navigationPath) || navigationPath.status != NavMeshPathStatus.PathComplete) continue;
                if (d < bd) { bd = d; best = t; }
            }
            if (best != null) { attackPoint = best; reserved[best] = this; }
        }

        // ---------------------------------------------------------------- serangan

        void BeginAttack(TargetType type)
        {
            state = type == TargetType.Player ? State.AttackPlayer : State.AttackGenerator;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            nextAttackTime = Time.time + config.attackCooldown * cooldownMul;
            hitTime = Time.time + config.attackHitDelay / Mathf.Max(0.1f, config.animSpeed);
            pendingHit = true;
            stateTimer = Mathf.Min(config.attackCooldown * cooldownMul, 1.0f / Mathf.Max(0.1f, config.animSpeed)) ;
            if (animator != null) animator.SetTrigger(pAttack);
        }

        void TickAttack()
        {
            // menghadap target
            Vector3 targetPos = state == State.AttackPlayer && playerTr != null ? playerTr.position
                : (targetGen != null ? targetGen.transform.position : transform.position + transform.forward);
            Vector3 dir = Flat(targetPos - transform.position);
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), config.angularSpeed * 1.5f * Time.deltaTime);

            if (pendingHit && Time.time >= hitTime) { pendingHit = false; ApplyHit(); }

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f && !pendingHit)
            {
                agent.isStopped = false;
                state = State.Advance;
                nextThink = 0f;
            }
        }

        void ApplyHit()
        {
            if (state == State.AttackPlayer)
            {
                if (playerHp == null || playerHp.IsDead || playerTr == null) return;
                float d = Flat(playerTr.position - transform.position).magnitude;
                if (d <= config.attackRange * 1.4f)
                {
                    Vector3 dir = Flat(playerTr.position - transform.position).normalized;
                    playerHp.TakeDamage(config.attackDamage, playerTr.position + Vector3.up, dir);
                }
            }
            else if (state == State.AttackGenerator && targetGen != null && targetGen.IsAlive && targetGen.Body != null)
            {
                Vector3 cp = targetGen.Body.ClosestPoint(transform.position);
                if (Flat(cp - transform.position).magnitude <= config.attackRange + 1f)
                    targetGen.Damage(config.attackDamage * config.generatorDamageMultiplier);
            }
        }

        // ---------------------------------------------------------------- reaksi

        void EnterSpawning(float duration)
        {
            state = State.Spawning; stateTimer = duration;
            agent.isStopped = true; agent.velocity = Vector3.zero;
            if (animator != null) animator.SetTrigger(pScream);
        }

        void OnDamaged(float amount, Vector3 dir)
        {
            if (state == State.Dead) return;
            // ditembak: langsung tahu posisi player
            FindPlayer();
            if (playerTr != null) { lastKnownPos = playerTr.position; lastSeen = Time.time; }

            if (config.kind == EnemyKind.Boss && !enraged && health.Fraction <= config.enrageHealthFraction)
            {
                enraged = true; speedMul = config.enrageSpeedMultiplier; cooldownMul = config.enrageCooldownMultiplier;
                if (animator != null) animator.speed = config.animSpeed * 1.15f;
                pendingHit = false;
                EnterSpawning(config.screamDuration);
                return;
            }

            if (config.canStagger && state != State.Spawning && amount >= config.staggerThreshold * health.MaxHealth)
            {
                pendingHit = false;
                state = State.Stagger; stateTimer = config.staggerDuration;
                agent.isStopped = true; agent.velocity = Vector3.zero;
            }
        }

        void OnDied(EnemyHealth h)
        {
            state = State.Dead;
            pendingHit = false;
            ReleaseTarget();
            if (agent.enabled) { agent.isStopped = true; agent.enabled = false; }
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (animator != null)
            {
                bool shotFromFront = Vector3.Dot(h.LastHitDirection, transform.forward) < 0f;   // peluru datang dari depan -> jatuh ke belakang
                animator.SetInteger(pDeathType, shotFromFront ? 0 : 1);
                animator.SetTrigger(pDie);
                animator.speed = 1f;
            }
            StartCoroutine(SinkAndDestroy());
        }

        IEnumerator SinkAndDestroy()
        {
            yield return new WaitForSeconds(Mathf.Max(1f, config.corpseTime - 1.5f));
            float t = 0f; Vector3 p = transform.position;
            while (t < 1.5f) { t += Time.deltaTime; transform.position = p + Vector3.down * (t / 1.5f * 0.8f); yield return null; }
            Destroy(gameObject);
        }
    }
}
