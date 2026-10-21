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
        public bool DebugMode { get; private set; }
        GUIStyle debugLabel;

        void Update()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.f8Key.wasPressedThisFrame) DebugMode = !DebugMode;
            if (!DebugMode || Time.timeScale <= 0f || catalog == null) return;
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
            if (Event.current.type != EventType.Repaint) return;
            if (!DebugMode)
            {
                if (showHelp) GUI.Box(new Rect(Screen.width - 175, 8, 165, 25), "F8  DEBUG: OFF");
                return;
            }
            if (debugLabel == null) debugLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            int bosses = 0, offMesh = 0, blocked = 0;
            var camera = Camera.main;
            foreach (var enemy in EnemyHealth.Alive)
            {
                if (enemy == null || enemy.IsDead) continue;
                bool boss = enemy.config != null && enemy.config.kind == EnemyKind.Boss;
                if (boss) bosses++;
                var agent = enemy.GetComponent<NavMeshAgent>();
                bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
                string path = !onMesh ? "OFF NAVMESH" : agent.pathPending ? "Calculating" :
                    !agent.hasPath ? "No path" : agent.pathStatus.ToString();
                if (!onMesh) offMesh++;
                else if (agent.hasPath && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) blocked++;
                if (camera == null) continue;
                Color color = !onMesh ? Color.magenta : boss ? new Color(1f, .4f, .35f) : Color.yellow;
                if (onMesh && agent.hasPath)
                {
                    var corners = agent.path.corners;
                    for (int i = 1; i < corners.Length; i++) DrawPath(camera, corners[i - 1], corners[i], color);
                }
                var point = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * ((agent != null ? agent.height : 2f) + .3f));
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                var brain = enemy.GetComponent<EnemyBrain>();
                string state = brain != null ? brain.CurrentState.ToString() : "No AI";
                string target = brain != null ? brain.DebugTarget : "None";
                string speed = onMesh ? agent.velocity.magnitude.ToString("F1") : "0.0";
                var rect = new Rect(Mathf.Clamp(point.x - 155, 0, Mathf.Max(0, Screen.width - 310)),
                    Mathf.Clamp(Screen.height - point.y - 68, 0, Mathf.Max(0, Screen.height - 68)), 310, 68);
                GUI.Box(rect, GUIContent.none);
                debugLabel.normal.textColor = color;
                GUI.Label(new Rect(rect.x + 5, rect.y + 2, 300, 64),
                    (boss ? "BOSS" : "ZOMBIE") + "  HP " + enemy.Health.ToString("F0") + "/" + enemy.MaxHealth.ToString("F0") +
                    "\n" + state + " -> " + target + "\n" + path + " | " + speed + " m/s", debugLabel);
            }
            GUI.Box(new Rect(Screen.width - 405, 8, 395, 105), GUIContent.none);
            debugLabel.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 395, 12, 375, 100),
                "F8  DEBUG: ON | FPS " + (1f / Mathf.Max(.001f, Time.unscaledDeltaTime)).ToString("F0") +
                "\nAlive: " + EnemyHealth.Alive.Count + " | Bosses: " + bosses + " | Off NavMesh: " + offMesh + " | Bad paths: " + blocked +
                "\nF1 zombie | F2 boss | F3 x5 | F4 clear" +
                "\n[ / ] Debug spawn wave: " + debugWave + " | Magenta = off NavMesh", debugLabel);
        }

        static void DrawPath(Camera camera, Vector3 start, Vector3 end, Color color)
        {
            var a = camera.WorldToScreenPoint(start + Vector3.up * .15f);
            var b = camera.WorldToScreenPoint(end + Vector3.up * .15f);
            if (a.z <= 0 || b.z <= 0) return;
            Vector2 from = new Vector2(a.x, Screen.height - a.y), to = new Vector2(b.x, Screen.height - b.y);
            Vector2 delta = to - from;
            var matrix = GUI.matrix;
            var previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y, delta.magnitude, 2), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
    }
}
