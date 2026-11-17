using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Enemy;
using Outpost.Env;

namespace Outpost.Final
{
    /// <summary>Satu posisi jaga hasil PCG untuk NPC sekutu (tidak membuat objek di map).</summary>
    public sealed class TacticalPoint
    {
        public Vector3 position;
        public float score, laneView, cover, distance;
        public bool seesGenerator;
        public string bestLane;
        public Object owner;                      // sekutu yang sedang menempati titik ini
    }

    /// <summary>
    /// PCG titik taktis di map statis: sampling seeded di cincin sekitar generator → generate-and-test
    /// (NavMesh, jarak dari attack point zombie, jalur ke generator) → scoring (pandangan ke lane, cover, pandangan ke generator)
    /// → pilih top-K dengan jarak minimum antar-titik. Dipakai Utility AI sekutu sebagai posisi Defend/Intercept.
    /// </summary>
    public static class TacticalPoints
    {
        public static readonly int BlockMask = ~((1 << 9) | (1 << 8) | (1 << 14) | (1 << 2) | (1 << 5));

        public sealed class Result
        {
            public readonly List<TacticalPoint> points = new List<TacticalPoint>();
            public readonly List<Vector3> rejectedPositions = new List<Vector3>();
            public readonly Dictionary<string, int> rejected = new Dictionary<string, int>();
            public int candidates, valid;
            public void Reject(string rule, Vector3 at) { int n; rejected.TryGetValue(rule, out n); rejected[rule] = n + 1; rejectedPositions.Add(at); }
            public string RejectionText { get { return rejected.Count == 0 ? "-" : string.Join(", ", rejected.Select(kv => kv.Key + " " + kv.Value).ToArray()); } }
        }

        public static Result Generate(FinalProfile p, int seed, GeneratorHealth generator, int agentTypeId)
        {
            var result = new Result();
            if (generator == null) return result;
            var rng = new System.Random(seed ^ 0x7AC7);
            var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            var attackPoints = generator.AttackPoints.Where(t => t != null).Select(t => t.position).ToList();
            Vector3 center = generator.transform.position;
            if (attackPoints.Count > 0) center.y = attackPoints.Average(ap => ap.y);   // pivot generator bisa di atas tanah; pakai tinggi attack point

            // arah tiap lane = rata-rata posisi spawn point lane itu
            var lanes = Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None)
                .Where(s => s.group == "Enemy").GroupBy(s => s.lane)
                .Select(g => new KeyValuePair<string, Vector3>(g.Key.ToString(), g.Aggregate(Vector3.zero, (a, s) => a + s.transform.position) / g.Count()))
                .ToList();

            Vector3 goal = center;
            NavMeshHit gh;
            if (attackPoints.Count > 0 && NavMesh.SamplePosition(attackPoints[0], out gh, 2f, filter)) goal = gh.position;

            var scored = new List<TacticalPoint>();
            for (int i = 0; i < p.tacticalCandidates; i++)
            {
                result.candidates++;
                double a = rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(p.tacticalMinRadius, p.tacticalMaxRadius, Mathf.Sqrt((float)rng.NextDouble()));
                Vector3 c = center + new Vector3(Mathf.Cos((float)a), 0f, Mathf.Sin((float)a)) * r;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(c, out hit, 2.5f, filter)) { result.Reject("NotNavMesh", c); continue; }
                Vector3 pos = hit.position;
                if (Mathf.Abs(pos.y - center.y) > 3f) { result.Reject("Height", pos); continue; }
                if (attackPoints.Any(ap => Flat(ap - pos).magnitude < p.tacticalAttackPointClear)) { result.Reject("NearAttackPoint", pos); continue; }
                if (generator.Body != null && Flat(generator.Body.ClosestPoint(pos) - pos).magnitude < 1.8f) { result.Reject("NearAttackPoint", pos); continue; }
                if (!NavMesh.CalculatePath(pos, goal, filter, path) || path.status != NavMeshPathStatus.PathComplete) { result.Reject("NoPath", pos); continue; }
                scored.Add(Evaluate(p, pos, generator, r, lanes));
                result.valid++;
            }

            foreach (var t in scored.OrderByDescending(t => t.score))
            {
                if (result.points.Count >= p.tacticalPointCount) break;
                if (result.points.Any(o => Flat(o.position - t.position).magnitude < p.tacticalSpacing)) { result.Reject("Spacing", t.position); continue; }
                result.points.Add(t);
            }
            return result;
        }

        static TacticalPoint Evaluate(FinalProfile p, Vector3 pos, GeneratorHealth generator, float radius, List<KeyValuePair<string, Vector3>> lanes)
        {
            var t = new TacticalPoint { position = pos, distance = radius };
            Vector3 eye = pos + Vector3.up * 1.5f;
            float best = -1f, sum = 0f;
            foreach (var lane in lanes)
            {
                Vector3 dir = Flat(lane.Value - pos);
                if (dir.sqrMagnitude < 0.01f) continue;
                RaycastHit h;
                float clear = Physics.Raycast(eye, dir.normalized, out h, p.tacticalSightRange, BlockMask, QueryTriggerInteraction.Ignore) ? h.distance : p.tacticalSightRange;
                float v = clear / p.tacticalSightRange;
                sum += v;
                if (v > best) { best = v; t.bestLane = lane.Key; }
            }
            t.laneView = lanes.Count > 0 ? sum / lanes.Count : 0f;

            int blocked = 0;
            for (int k = 0; k < 8; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                if (Physics.Raycast(pos + Vector3.up * 0.8f, d, 1.6f, BlockMask, QueryTriggerInteraction.Ignore)) blocked++;
            }
            float coverRatio = blocked / 8f;
            t.cover = 1f - Mathf.Abs(coverRatio - 0.375f) / 0.625f;          // ideal: sebagian sisi tertutup, bukan terjepit
            // pandangan ke badan generator: lolos bila sinar tidak terhalang, atau yang kena adalah generator / sesuatu yang menempel di badannya
            Bounds gb = generator.Body != null ? generator.Body.bounds : new Bounds(generator.transform.position, Vector3.one * 2f);
            Vector3 toGen = gb.center - eye;
            RaycastHit gh;
            gb.Expand(1.5f);
            t.seesGenerator = !Physics.Raycast(eye, toGen.normalized, out gh, toGen.magnitude, BlockMask, QueryTriggerInteraction.Ignore)
                || gb.Contains(gh.point) || gh.collider.GetComponentInParent<GeneratorHealth>() != null;
            t.score = 0.35f * t.laneView + 0.2f * t.cover + 0.2f * (t.seesGenerator ? 1f : 0f) + 0.25f * (1f - radius / p.tacticalMaxRadius);
            return t;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
