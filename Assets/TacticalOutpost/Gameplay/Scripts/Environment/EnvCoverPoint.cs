using UnityEngine;

namespace Outpost.Env
{
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
}
