using UnityEngine;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Debug visualization dengan Gizmos (modul §89). Ditempel ke root dungeon. Tampil di Scene View
    /// (dan Game View bila tombol Gizmos di Game View aktif).
    ///   Putih  = Leaf BSP          Hijau = Room               Cyan  = corridor L (from → corner → to)
    ///   Kuning = critical path     Bola hijau = Start         Bola merah = Goal
    ///   Merah  = enemy, ungu = boss, biru muda = item, kuning-hijau = power relay, coklat = props (cell terblokir)
    /// </summary>
    public sealed class DungeonGizmos : MonoBehaviour
    {
        public DungeonWorld world;
        public bool showLeaves = true, showRooms = true, showCorridors = true, showPath = true, showSpawns = true;

        void OnDrawGizmos()
        {
            if (world == null || world.layout == null) return;
            var L = world.layout; float t = world.tile; const float y = 0.25f;

            if (showLeaves)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.55f);
                foreach (var leaf in L.leafNodes) Rect(leaf.x, leaf.y, leaf.width, leaf.height, y + 0.3f);
            }
            if (showRooms)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.4f, 0.9f);
                foreach (var r in L.rooms) Rect(r.x, r.y, r.width, r.height, y);
            }
            if (showCorridors)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                foreach (var c in L.corridors)
                {
                    Gizmos.DrawLine(world.CellToWorld(c[0]) + Vector3.up * y, world.CellToWorld(c[1]) + Vector3.up * y);
                    Gizmos.DrawLine(world.CellToWorld(c[1]) + Vector3.up * y, world.CellToWorld(c[2]) + Vector3.up * y);
                }
            }
            if (showPath && L.criticalPath.Count > 1)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
                for (int i = 1; i < L.criticalPath.Count; i++)
                    Gizmos.DrawLine(world.CellToWorld(L.criticalPath[i - 1]) + Vector3.up * (y + 0.1f), world.CellToWorld(L.criticalPath[i]) + Vector3.up * (y + 0.1f));
            }
            Gizmos.color = new Color(0.2f, 1f, 0.3f); Gizmos.DrawSphere(world.CellToWorld(L.startPosition) + Vector3.up, t * 0.25f);
            Gizmos.color = new Color(1f, 0.2f, 0.15f); Gizmos.DrawSphere(world.CellToWorld(L.goalPosition) + Vector3.up * 4f, t * 0.3f);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.5f);
            Circle(world.CellToWorld(L.startPosition), world.profile.safeRadiusCells * t);

            if (!showSpawns) return;
            foreach (var q in L.placements)
            {
                Vector3 p = world.CellToWorld(q.cell) + Vector3.up * 0.6f;
                switch (q.kind)
                {
                    case PlaceKind.Enemy: Gizmos.color = new Color(1f, 0.2f, 0.2f); Gizmos.DrawCube(p, Vector3.one * t * 0.25f); break;
                    case PlaceKind.Boss: Gizmos.color = new Color(0.75f, 0.1f, 1f); Gizmos.DrawCube(p, Vector3.one * t * 0.45f); break;
                    case PlaceKind.Relay: Gizmos.color = new Color(0.8f, 1f, 0.2f); Gizmos.DrawCube(p + Vector3.up, new Vector3(0.4f, 2f, 0.4f) * t * 0.3f); break;
                    case PlaceKind.Item: Gizmos.color = new Color(0.3f, 0.85f, 1f); Gizmos.DrawWireCube(p, Vector3.one * t * 0.25f); break;
                    default: Gizmos.color = new Color(0.65f, 0.4f, 0.2f); Gizmos.DrawWireCube(p, new Vector3(t, 0.2f, t) * 0.9f); break;
                }
            }
        }

        void Rect(int x, int y, int w, int h, float height)
        {
            Vector3 a = world.VertexToWorld(x, y) + Vector3.up * height, b = world.VertexToWorld(x + w, y) + Vector3.up * height;
            Vector3 c = world.VertexToWorld(x + w, y + h) + Vector3.up * height, d = world.VertexToWorld(x, y + h) + Vector3.up * height;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }

        static void Circle(Vector3 c, float r)
        {
            Vector3 prev = c + new Vector3(r, 0.2f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, next); prev = next;
            }
        }
    }
}
