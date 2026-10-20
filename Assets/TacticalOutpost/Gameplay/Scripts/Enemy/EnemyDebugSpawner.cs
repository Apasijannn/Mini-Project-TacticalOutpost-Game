using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Outpost.Env;
using Outpost.Player;

namespace Outpost.Enemy
{
    /// <summary>Alat uji: munculkan musuh dengan tombol. F1 zombie, F2 boss, F3 lima zombie, F4 clear enemies, [ ] ganti wave uji.</summary>
    public class EnemyDebugSpawner : MonoBehaviour
    {
        public EnemyCatalog catalog;
        public int debugWave = 1;
        public bool showHelp = true;
        public float minDistanceFromPlayer = 15f;

        void Update()
        {
            var kb = Keyboard.current; if (kb == null || catalog == null) return;
            if (kb.f1Key.wasPressedThisFrame) Spawn(EnemyKind.Normal);
            if (kb.f2Key.wasPressedThisFrame) Spawn(EnemyKind.Boss);
            if (kb.f3Key.wasPressedThisFrame) for (int i = 0; i < 5; i++) Spawn(EnemyKind.Normal);
            if (kb.f4Key.wasPressedThisFrame) KillAll();
            if (kb.leftBracketKey.wasPressedThisFrame) debugWave = Mathf.Max(1, debugWave - 1);
            if (kb.rightBracketKey.wasPressedThisFrame) debugWave = Mathf.Min(3, debugWave + 1);
        }

        public GameObject Spawn(EnemyKind kind)
        {
            var v = catalog.Pick(kind, kind == EnemyKind.Boss ? Mathf.Max(3, debugWave) : debugWave);
            if (v == null) { Debug.LogWarning("[EnemySpawner] tidak ada varian untuk " + kind + " wave " + debugWave); return null; }
            return SpawnAt(v.prefab, PickSpawnPoint());
        }

        public static GameObject SpawnAt(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return null;
            var agent = prefab.GetComponent<NavMeshAgent>();
            if (agent == null) return null;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var area = PlayArea.Instance;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(pos, out hit, 1.5f, filter) ||
                (area != null && !area.Contains(hit.position)) || Mathf.Abs(hit.position.y - pos.y) > 1.2f)
            {
                Debug.LogError("[EnemySpawner] Spawn di luar permukaan navigasi: " + pos);
                return null;
            }
            return Instantiate(prefab, hit.position, Quaternion.identity);
        }

        Vector3 PickSpawnPoint()
        {
            var pts = FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None);
            var hp = PlayerHealth.Instance;
            Vector3 pp = hp != null ? hp.transform.position : Vector3.zero;
            for (int tries = 0; tries < 30 && pts.Length > 0; tries++)
            {
                var p = pts[Random.Range(0, pts.Length)];
                if (p.group != "Enemy") continue;
                Vector3 c = p.transform.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                if (PlayArea.Instance != null && !PlayArea.Instance.Contains(c, 1.5f)) continue;
                if ((c - pp).magnitude >= minDistanceFromPlayer) return c;
            }
            return pp + new Vector3(-20f, 0f, 0f);
        }

        public static void KillAll()
        {
            var list = new System.Collections.Generic.List<EnemyHealth>(EnemyHealth.Alive);
            foreach (var e in list) if (e != null) e.Kill();
        }

        void OnGUI()
        {
            if (!showHelp) return;
            GUI.Label(new Rect(12, 10, 640, 24), "[DEBUG] F1 zombie | F2 boss | F3 x5 | F4 clear enemies | [ ] test wave = " + debugWave + " | alive: " + EnemyHealth.Alive.Count);
        }
    }
}
