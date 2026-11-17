using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Enemy;
using Outpost.Player;

namespace Outpost.Final
{
    public enum AllyAction { Defend, Intercept, Support, Repair, Retreat, Down }

    /// <summary>
    /// NPC sekutu "Defender" — Utility AI.
    /// Tiap 0,3 detik lima aksi diberi skor dari kondisi dunia (HP player, HP generator, ancaman, HP sendiri),
    /// aksi dengan skor tertinggi dipilih (+hysteresis). Bobot dasar dari FinalProfile, digeser DDA.
    /// Lapisan tembak berjalan paralel: target = zombie dengan skor ancaman tertinggi yang terlihat.
    /// Zombie bisa menyerang sekutu lewat EnemyBrain.Allies (IAllyTarget).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AllyBrain : MonoBehaviour, IAllyTarget
    {
        public static readonly List<AllyBrain> All = new List<AllyBrain>();
        public static readonly string[] ActionNames = { "DEFEND", "INTERCEPT", "SUPPORT", "REPAIR", "RETREAT", "DOWN" };

        public string Callsign { get; private set; }
        public AllyAction Action { get; private set; }
        public readonly float[] Scores = new float[5];
        public float Health { get; private set; }
        public float MaxHealth { get { return profile != null ? profile.allyHealth : 100f; } }
        public EnemyHealth Target { get; private set; }
        public TacticalPoint HoldPoint { get; private set; }
        public int Ammo { get; private set; }
        public bool Reloading { get { return Time.time < reloadEnd; } }
        public int Kills { get; set; }
        public float Repaired { get; private set; }
        public Vector3 Destination { get; private set; }

        public Transform TargetTransform { get { return transform; } }
        public bool IsTargetable { get { return isActiveAndEnabled && Action != AllyAction.Down && Health > 0f; } }

        FinalProfile profile;
        FinalSession session;
        NavMeshAgent agent;
        AllyBody visual;
        AudioSource sfx;
        AudioClip shootClip, reloadClip;
        GameObject impactPrefab;
        float nextThink, nextScan, nextFire, reloadEnd, downUntil, lastHurt = -99f, nextNoise;
        static readonly int ShootMask = ~((1 << 8) | (1 << 14) | (1 << 2) | (1 << 5));

        public void Init(FinalProfile p, FinalSession s, AllyBody body, int agentTypeId, string callsign)
        {
            profile = p; session = s; visual = body; Callsign = callsign;
            agent = GetComponent<NavMeshAgent>();
            agent.agentTypeID = agentTypeId;
            agent.speed = p.allyMoveSpeed; agent.acceleration = 14f; agent.angularSpeed = 0f;
            agent.updateRotation = false; agent.stoppingDistance = 0.6f; agent.radius = 0.35f; agent.height = 1.8f;
            agent.avoidancePriority = 10;
            Health = MaxHealth; Ammo = p.allyMagazine;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 1f; sfx.minDistance = 4f; sfx.maxDistance = 70f; sfx.dopplerLevel = 0f; sfx.playOnAwake = false;
            var player = PlayerHealth.Instance;
            var manager = player != null ? player.GetComponentInChildren<WeaponManager>(true) : null;
            if (manager != null && manager.weapons != null)
                foreach (var w in manager.weapons)
                    if (w != null && w.kind == WeaponKind.Rifle) { shootClip = w.shootClip; reloadClip = w.reloadClip; impactPrefab = w.enemyImpactPrefab; break; }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); if (!EnemyBrain.Allies.Contains(this)) EnemyBrain.Allies.Add(this); }
        void OnDisable() { All.Remove(this); EnemyBrain.Allies.Remove(this); if (HoldPoint != null && HoldPoint.owner == this) HoldPoint.owner = null; }

        // ---------------------------------------------------------------- IAllyTarget

        public void TakeHit(float damage, Vector3 direction)
        {
            if (!IsTargetable) return;
            Health = Mathf.Max(0f, Health - damage); lastHurt = Time.time;
            if (Health <= 0f) GoDown();
        }

        void GoDown()
        {
            Action = AllyAction.Down; Target = null;
            downUntil = Time.time + profile.allyRespawnDelay;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            visual.down = true;
            if (HoldPoint != null && HoldPoint.owner == this) HoldPoint.owner = null;
            HoldPoint = null;
            session.Log(Callsign + " is down. Back in " + profile.allyRespawnDelay.ToString("0") + "s.");
        }

        void Revive()
        {
            Vector3 p = session.GroundCenter + Random.insideUnitSphere * 4f; p.y = session.GroundCenter.y;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 6f, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas })) agent.Warp(hit.position);
            Health = MaxHealth; Ammo = profile.allyMagazine; visual.down = false;
            Action = AllyAction.Defend; agent.isStopped = false; nextThink = 0f;
            session.Log(Callsign + " is back in the fight.");
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            visual.speed = agent.enabled ? agent.velocity.magnitude : 0f;
            if (Action == AllyAction.Down) { if (Time.time >= downUntil && session.Running) Revive(); return; }
            if (!session.Running) { if (agent.isOnNavMesh) agent.isStopped = true; visual.RelaxAim(); return; }

            if (Time.time - lastHurt > 4f) Health = Mathf.Min(MaxHealth, Health + profile.allyRegenPerSecond * Time.deltaTime);
            if (Time.time >= nextScan) { nextScan = Time.time + 0.2f; ScanTarget(); }
            if (Time.time >= nextThink) { nextThink = Time.time + 0.3f; Think(); }

            if (Action == AllyAction.Repair) DoRepair();
            Face();
            Shoot();
        }

        // ---------------------------------------------------------------- Utility AI

        void Think()
        {
            var o = session.DdaOut;
            var player = PlayerHealth.Instance;
            var gen = session.Generator;
            float playerFrac = player != null && !player.IsDead ? player.Health / player.maxHealth : 1f;
            float genFrac = gen != null ? gen.Fraction : 1f;
            float selfFrac = Health / MaxHealth;
            int nearGen = 0, nearPlayer = 0, nearSelf = 0;
            foreach (var e in EnemyHealth.Alive)
            {
                if (e == null || e.IsDead) continue;
                Vector3 p = e.transform.position;
                if (gen != null && Flat(p - gen.transform.position).magnitude < 9f) nearGen++;
                if (player != null && Flat(p - player.transform.position).magnitude < 8f) nearPlayer++;
                if (Flat(p - transform.position).magnitude < 6f) nearSelf++;
            }

            Scores[(int)AllyAction.Defend] = profile.defendWeight + (nearGen + nearPlayer == 0 ? 0.15f : 0f);
            Scores[(int)AllyAction.Intercept] = o.interceptWeight * Mathf.Clamp01(nearGen / 4f);
            Scores[(int)AllyAction.Support] = player == null || player.IsDead ? 0f
                : o.supportWeight * (Mathf.Pow(1f - playerFrac, 1.5f) * 0.9f + Mathf.Clamp01(nearPlayer / 4f) * 0.6f);
            Scores[(int)AllyAction.Repair] = genFrac > 0.97f ? 0f : o.repairWeight * (1f - genFrac) * 1.6f * (nearGen == 0 ? 1f : 0.15f);
            Scores[(int)AllyAction.Retreat] = selfFrac >= 0.45f ? 0f
                : profile.retreatWeight * (1f - selfFrac / 0.45f) * 1.2f * (nearSelf > 0 ? 1f : 0.5f);

            int best = (int)Action < Scores.Length ? (int)Action : 0;
            float bestScore = Scores[best] + 0.1f;                                   // hysteresis supaya tidak plin-plan
            for (int i = 0; i < Scores.Length; i++) if (Scores[i] > bestScore) { best = i; bestScore = Scores[i]; }
            if ((AllyAction)best != Action) session.Log(Callsign + ": " + ActionNames[(int)Action] + " → " + ActionNames[best]);
            Action = (AllyAction)best;
            Execute(player, gen);
        }

        void Execute(PlayerHealth player, GeneratorHealth gen)
        {
            Vector3 dest = transform.position;
            switch (Action)
            {
                case AllyAction.Defend:
                    Claim(session.BestPoint(this, false));
                    dest = HoldPoint != null ? HoldPoint.position : session.GroundCenter;
                    break;
                case AllyAction.Intercept:
                    Claim(session.BestPoint(this, true));
                    dest = HoldPoint != null ? HoldPoint.position : session.GroundCenter;
                    break;
                case AllyAction.Support:
                    if (player != null)
                    {
                        Vector3 side = Flat(transform.position - player.transform.position);
                        dest = player.transform.position + (side.sqrMagnitude > 0.1f ? side.normalized : -player.transform.forward) * 3f;
                    }
                    break;
                case AllyAction.Repair:
                    if (gen != null && gen.Body != null) dest = gen.Body.ClosestPoint(transform.position);
                    break;
                case AllyAction.Retreat:
                    Claim(session.SafestPoint(this));
                    dest = HoldPoint != null ? HoldPoint.position : session.GroundCenter;
                    break;
            }
            MoveTo(dest);
        }

        void Claim(TacticalPoint p)
        {
            if (HoldPoint == p) return;
            if (HoldPoint != null && HoldPoint.owner == this) HoldPoint.owner = null;
            HoldPoint = p;
            if (p != null) p.owner = this;
        }

        void MoveTo(Vector3 dest)
        {
            if (!agent.isOnNavMesh) return;
            Destination = dest;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(dest, out hit, 2.5f, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas }))
                dest = hit.position;
            agent.isStopped = false;
            if ((agent.destination - dest).sqrMagnitude > 0.25f) agent.SetDestination(dest);
        }

        void DoRepair()
        {
            var gen = session.Generator;
            if (gen == null || gen.Body == null || !gen.IsAlive) return;
            if (Flat(gen.Body.ClosestPoint(transform.position) - transform.position).magnitude > 2.2f) return;
            float before = gen.Health;
            gen.Repair(profile.allyRepairPerSecond * Time.deltaTime);
            Repaired += gen.Health - before;
        }

        // ---------------------------------------------------------------- tembak

        void ScanTarget()
        {
            Target = null;
            var gen = session.Generator;
            var player = PlayerHealth.Instance;
            Vector3 eye = visual.aim != null ? visual.aim.position : transform.position + Vector3.up * 1.5f;
            var candidates = new List<KeyValuePair<float, EnemyHealth>>();
            foreach (var e in EnemyHealth.Alive)
            {
                if (e == null || e.IsDead) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d > profile.allyRange) continue;
                float threat = 0.3f * (1f - d / profile.allyRange);
                if (gen != null)
                {
                    float dg = Flat(e.transform.position - gen.transform.position).magnitude;
                    threat += 0.4f * Mathf.Clamp01(1f - dg / 40f) + (dg < 4.5f ? 0.4f : 0f);
                }
                if (player != null && Flat(e.transform.position - player.transform.position).magnitude < 5f) threat += 0.35f;
                if (d < 4f) threat += 0.5f;                                           // bela diri
                if (e.config != null && e.config.kind == EnemyKind.Boss) threat += 0.25f;
                candidates.Add(new KeyValuePair<float, EnemyHealth>(threat, e));
            }
            candidates.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int i = 0; i < candidates.Count && i < 6; i++)
            {
                var e = candidates[i].Value;
                Vector3 chest = Chest(e);
                RaycastHit hit;
                if (Physics.Raycast(eye, (chest - eye).normalized, out hit, profile.allyRange + 2f, ShootMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<EnemyHealth>() == e) { Target = e; return; }
            }
        }

        static Vector3 Chest(EnemyHealth e)
        {
            float sc = (e.config != null ? e.config.visualScale : 1f) * e.transform.localScale.y;
            return e.transform.position + Vector3.up * 1.2f * sc;
        }

        void Face()
        {
            Vector3 look = Target != null ? Target.transform.position - transform.position
                : agent.velocity.sqrMagnitude > 0.2f ? agent.velocity : transform.forward;
            look = Flat(look);
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 420f * Time.deltaTime);
            if (Target != null) visual.AimAt(Chest(Target)); else visual.RelaxAim();
        }

        void Shoot()
        {
            if (Reloading) return;
            if (Ammo <= 0 || Target == null && Ammo < profile.allyMagazine / 2) { if (Ammo < profile.allyMagazine) StartReload(); return; }
            if (Target == null || Action == AllyAction.Repair || Time.time < nextFire) return;
            Vector3 to = Flat(Target.transform.position - transform.position);
            if (Vector3.Angle(transform.forward, to) > 25f) return;                 // tunggu badan menghadap target
            nextFire = Time.time + 60f / profile.allyRoundsPerMinute;
            Ammo--;
            Vector3 eye = visual.aim.position;
            Vector3 dir = Quaternion.Euler(Random.Range(-1f, 1f) * profile.allySpread, Random.Range(-1f, 1f) * profile.allySpread, 0f) * (Chest(Target) - eye).normalized;
            Vector3 end = eye + dir * profile.allyRange;
            RaycastHit hit;
            if (Physics.Raycast(eye, dir, out hit, profile.allyRange, ShootMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead)
                {
                    EnemyBrain.DamageSource = this;
                    try { enemy.TakeDamage(profile.allyDamage, hit.point, dir); }
                    finally { EnemyBrain.DamageSource = null; }
                    if (enemy.IsDead) Kills++;
                    if (impactPrefab != null) Destroy(Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
                }
            }
            visual.Flash(end);
            if (shootClip != null) sfx.PlayOneShot(shootClip, 0.55f);
            if (Time.time >= nextNoise) { nextNoise = Time.time + 0.5f; EnemyBrain.AllyNoise(this, transform.position, profile.allyNoiseRadius); }
        }

        void StartReload()
        {
            reloadEnd = Time.time + profile.allyReloadTime; Ammo = profile.allyMagazine;
            if (reloadClip != null) sfx.PlayOneShot(reloadClip, 0.6f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
