using UnityEngine;

namespace Outpost.PCG
{
    /// <summary>
    /// Visual debugging di Scene View (dan Game View bila tombol Gizmos aktif), ditempel ke root arena PCG.
    /// OnDrawGizmos: area yang dipakai generator selalu terlihat.
    ///   putih  = batas arena (area kandidat obstacle)
    ///   merah  = spawn zone zombie di tiap gerbang
    ///   hijau  = safe zone player (playerClearRadius)
    ///   kuning = jarak minimum spawn zombie dari player (minEnemySpawnDistance)
    ///   oranye = area lapang generator (generatorClearRadius)
    ///   cyan   = cincin supply cache (supplyMinRadius..supplyMaxRadius)
    /// OnDrawGizmosSelected (pilih objek "PCG_Arena" di Hierarchy): semua kandidat, hijau diterima, merah ditolak.
    /// </summary>
    public sealed class PcgGizmos : MonoBehaviour
    {
        public PcgArena arena;
        static bool logged;

        void OnDrawGizmos()
        {
            var a = arena;
            if (a == null || a.profile == null) return;
            var p = a.profile;
            if (!logged) { logged = true; Debug.Log("[PCG] Gizmos arena digambar di Scene View (aktifkan tombol Gizmos bila tidak terlihat)."); }
            float y = a.groundY + 0.1f;

            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(a.Center.x, y, a.Center.z), new Vector3((a.half - p.wallMargin) * 2f, 0.05f, (a.half - p.wallMargin) * 2f));

            foreach (var g in a.gates)
            {
                Vector3 c = g.Inner(1.5f + (p.spawnZoneDepth - 1.5f) * 0.5f);
                Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.25f);
                var m = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(new Vector3(c.x, y + 1f, c.z), Quaternion.LookRotation(g.inward, Vector3.up), Vector3.one);
                Vector3 size = new Vector3(Mathf.Max(0.5f, g.halfWidth * 2f - 2f), 2f, p.spawnZoneDepth - 1.5f);
                Gizmos.DrawCube(Vector3.zero, size);
                Gizmos.color = new Color(1f, 0.2f, 0.15f, 1f);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = m;
            }

            Circle(a.playerSpawn, p.playerClearRadius, new Color(0.2f, 1f, 0.35f, 1f), y);
            Circle(a.playerSpawn, p.minEnemySpawnDistance, new Color(1f, 0.9f, 0.2f, 0.9f), y);
            Circle(a.generatorPos, p.generatorClearRadius, new Color(1f, 0.55f, 0.1f, 0.9f), y);
            Circle(a.generatorPos, p.supplyMinRadius, new Color(0.2f, 0.9f, 1f, 0.6f), y);
            Circle(a.generatorPos, p.supplyMaxRadius, new Color(0.2f, 0.9f, 1f, 0.6f), y);

            Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.35f);
            Gizmos.DrawSphere(a.playerSpawn + Vector3.up * 0.5f, 0.5f);
        }

        void OnDrawGizmosSelected()
        {
            if (arena == null) return;
            foreach (var m in arena.markers)
            {
                Gizmos.color = m.rule == PcgRule.Accepted ? new Color(0.3f, 1f, 0.35f, 0.9f) : new Color(1f, 0.25f, 0.2f, 0.7f);
                Gizmos.DrawSphere(m.position + Vector3.up * 0.25f, m.rule == PcgRule.Accepted ? 0.3f : 0.18f);
            }
        }

        static void Circle(Vector3 center, float radius, Color color, float y)
        {
            Gizmos.color = color;
            const int segments = 48;
            Vector3 prev = new Vector3(center.x + radius, y, center.z);
            for (int i = 1; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                Vector3 next = new Vector3(center.x + Mathf.Cos(t) * radius, y, center.z + Mathf.Sin(t) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
