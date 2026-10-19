using UnityEngine;

// Komponen penanda environment Tactical Outpost.
// Sengaja diberi namespace Outpost.Env dan prefix "Env" agar tidak bentrok dengan script AI tim (CoverPoint, SpawnPoint, dst).
namespace Outpost.Env
{
    public enum CoverHeight { Low, High }
    public enum LaneId { None, North, Main, South, Base }

    public enum EnvCategory
    {
        Ground, Building, Container, FuelTank, Barrel, Crate, Pallet, Pipe, Fence, Wall,
        Sandbag, Barrier, Vehicle, Tower, Light, Generator, Helipad, Rubble, Other
    }

    /// <summary>Menandai objek blockout agar nanti bisa ditukar dengan prefab aset final.</summary>
    public class EnvSlot : MonoBehaviour
    {
        public EnvCategory category;
        public string variant;
        public Vector3 size;
    }

    /// <summary>Titik cover. Arah proteksi = transform.forward (dari titik berdiri menuju objek pelindung).</summary>
    public class EnvCoverPoint : MonoBehaviour
    {
        public CoverHeight height = CoverHeight.Low;
        public LaneId lane = LaneId.None;
        public string face;

        public Vector3 ProtectDir { get { return transform.forward; } }

        void OnDrawGizmos()
        {
            Gizmos.color = height == CoverHeight.High ? new Color(0.2f, 1f, 0.3f) : new Color(0.2f, 0.85f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.3f, 0.3f);
            Vector3 a = transform.position + Vector3.up * 0.3f;
            Vector3 b = a + transform.forward * 1.2f;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, b - transform.forward * 0.3f + transform.right * 0.15f);
            Gizmos.DrawLine(b, b - transform.forward * 0.3f - transform.right * 0.15f);
        }
    }

    public class EnvSpawnPoint : MonoBehaviour
    {
        public LaneId lane = LaneId.Main;
        public string group = "Enemy";

        void OnDrawGizmos()
        {
            Gizmos.color = group == "Player" ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.25f, 0.2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }

    public class EnvGeneratorSlot : MonoBehaviour
    {
        public string id = "A";
        public float maxHp = 300f;
        public float repairRadius = 2.5f;
        public Transform[] attackPoints;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, repairRadius);
            if (attackPoints == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.1f);
            foreach (var t in attackPoints)
                if (t != null) Gizmos.DrawWireSphere(t.position + Vector3.up * 0.2f, 0.25f);
        }
    }

    /// <summary>Jalur pendekatan musuh. Waypoint = child transform berurutan.</summary>
    public class EnvLane : MonoBehaviour
    {
        public LaneId lane = LaneId.Main;
        public Color gizmoColor = Color.yellow;

        public Vector3[] Points()
        {
            var pts = new Vector3[transform.childCount];
            for (int i = 0; i < pts.Length; i++) pts[i] = transform.GetChild(i).position;
            return pts;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 p = transform.GetChild(i).position + Vector3.up * 0.2f;
                Gizmos.DrawSphere(p, 0.2f);
                if (i > 0) Gizmos.DrawLine(transform.GetChild(i - 1).position + Vector3.up * 0.2f, p);
            }
        }
    }

    public class EnvSpin : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 30f;
        void Update() { transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self); }
    }

    /// <summary>Lampu sorot yang menyapu ke kiri-kanan.</summary>
    public class EnvSearchlight : MonoBehaviour
    {
        public float sweepAngle = 40f;
        public float speed = 0.35f;
        public float phase;
        Quaternion baseRot;
        void Start() { baseRot = transform.localRotation; }
        void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * sweepAngle;
            transform.localRotation = baseRot * Quaternion.Euler(0f, a, 0f);
        }
    }

    public class EnvFlicker : MonoBehaviour
    {
        public Light target;
        public float baseIntensity = 2f;
        public float amount = 0.4f;
        public float speed = 9f;
        float seed;
        void Start() { seed = Random.value * 100f; if (target == null) target = GetComponent<Light>(); }
        void Update()
        {
            if (target == null) return;
            target.intensity = baseIntensity * (1f + (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount);
        }
    }

    public class EnvBlink : MonoBehaviour
    {
        public Renderer target;
        public Light lightTarget;
        public float period = 1.4f;
        void Update()
        {
            bool on = (Time.time % period) < period * 0.25f;
            if (target != null) target.enabled = on;
            if (lightTarget != null) lightTarget.enabled = on;
        }
    }
}
