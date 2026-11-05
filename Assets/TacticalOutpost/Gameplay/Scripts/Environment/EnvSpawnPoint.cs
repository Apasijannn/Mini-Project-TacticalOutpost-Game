using UnityEngine;

namespace Outpost.Env
{
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
}
