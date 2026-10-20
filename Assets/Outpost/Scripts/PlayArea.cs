using UnityEngine;

namespace Outpost.Env
{
    /// <summary>Area bermain di dalam pagar (bidang XZ). Spawn musuh divalidasi terhadap area ini supaya tidak pernah muncul di luar pagar.</summary>
    public class PlayArea : MonoBehaviour
    {
        public Vector2 min = new Vector2(-61f, -34f);   // x, z
        public Vector2 max = new Vector2(63f, 32f);

        public static PlayArea Instance { get; private set; }

        void Awake() { Instance = this; }
        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            return p.x >= min.x + margin && p.x <= max.x - margin && p.z >= min.y + margin && p.z <= max.y - margin;
        }

        public Vector3 Clamp(Vector3 p, float margin = 1f)
        {
            p.x = Mathf.Clamp(p.x, min.x + margin, max.x - margin);
            p.z = Mathf.Clamp(p.z, min.y + margin, max.y - margin);
            return p;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 c = new Vector3((min.x + max.x) * 0.5f, 1f, (min.y + max.y) * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(max.x - min.x, 2f, max.y - min.y));
        }
    }
}
