using UnityEngine;

namespace Outpost.Env
{
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
}
