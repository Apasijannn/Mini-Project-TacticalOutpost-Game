using UnityEngine;

namespace Outpost.Env
{
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
}
