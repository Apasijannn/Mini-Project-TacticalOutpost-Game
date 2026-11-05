using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Outpost.UI
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public Camera View { get; private set; }
        public ManualEntry[] entries;
        public Vector3[] spawns, targets;
        public int PreviewCount => actors.Count;
        sealed class Actor { public NavMeshAgent agent; public Animator animator; public float nextAttack, nextMove; }
        readonly List<Actor> actors = new List<Actor>();
        float startedAt;

        IEnumerator Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (View == null) View = root.GetComponentInChildren<Camera>(true);
            if (View == null) yield break;
            var listener = View.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            var environment = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
            if (!environment.IsValid() || !environment.isLoaded)
            {
                var load = SceneManager.LoadSceneAsync(IndustrialGameSession.EnvironmentScene, LoadSceneMode.Additive);
                if (load == null) yield break;
                yield return load;
                environment = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
            }
            foreach (var root in environment.GetRootGameObjects())
            {
                foreach (var walk in root.GetComponentsInChildren<SoloWalkthroughController>(true)) walk.gameObject.SetActive(false);
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var audio in root.GetComponentsInChildren<AudioListener>(true)) audio.enabled = false;
            }
            SceneManager.SetActiveScene(environment);
            View.tag = "MainCamera";
            View.cullingMask = ~((1 << 8) | (1 << 14) | (1 << 30));
            View.clearFlags = CameraClearFlags.Skybox;
            View.fieldOfView = 54f;
            View.nearClipPlane = .2f;
            View.farClipPlane = 500f;
            View.allowHDR = true;
            View.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            View.enabled = true;
            if (listener != null) listener.enabled = true;
            startedAt = Time.unscaledTime;
            SpawnPreview();
            Ready = true;
            AnimateView();
        }

        void SpawnPreview()
        {
            if (entries == null || spawns == null || spawns.Length == 0 || targets == null || targets.Length == 0) return;
            var normals = System.Array.FindAll(entries, e => e.enemy != null && e.enemy.kind == Outpost.Enemy.EnemyKind.Normal && e.visual != null);
            if (normals.Length == 0) return;
            for (int i = 0; i < 30; i++)
            {
                var entry = normals[i % normals.Length];
                if (!NavMesh.SamplePosition(spawns[i % spawns.Length], out var hit, 1.5f, NavMesh.AllAreas)) continue;
                var root = new GameObject("Menu zombie " + (i + 1)); root.transform.SetParent(transform, false); root.transform.position = hit.position;
                var visual = Instantiate(entry.visual, root.transform); visual.SetActive(true);
                entry.ApplyTint(visual);
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 9;
                var agent = root.AddComponent<NavMeshAgent>(); agent.speed = entry.enemy.walkSpeed; agent.acceleration = 8;
                agent.radius = .32f; agent.height = 1.8f; agent.stoppingDistance = .7f; agent.avoidancePriority = 30 + i;
                var actor = new Actor { agent = agent, animator = visual.GetComponentInChildren<Animator>(), nextMove = Time.time + 15 + i };
                if (agent.isOnNavMesh) agent.SetDestination(targets[i % targets.Length]);
                actors.Add(actor);
            }
        }
        void Update()
        {
            foreach (var actor in actors)
            {
                var agent = actor.agent; if (!agent.isOnNavMesh) continue;
                if (actor.animator != null) actor.animator.SetFloat("Speed", agent.velocity.magnitude / Mathf.Max(.1f, agent.speed) * 1.2f);
                if (!agent.pathPending && agent.remainingDistance < 1f)
                {
                    if (Time.time >= actor.nextAttack) { if (actor.animator != null) actor.animator.SetTrigger("Attack"); actor.nextAttack = Time.time + 2; }
                    if (Time.time >= actor.nextMove) { agent.SetDestination(targets[Random.Range(0, targets.Length)]); actor.nextMove = Time.time + Random.Range(6, 14); }
                }
            }
        }
        void LateUpdate() { if (Ready && View != null) AnimateView(); }
        void AnimateView()
        {
            float t = Time.unscaledTime - startedAt;
            Vector3 position = new Vector3(-67f + Mathf.Sin(t * .055f) * 3f,
                33f + Mathf.Sin(t * .11f) * .55f, -73f + Mathf.Sin(t * .075f) * 2f);
            Vector3 target = new Vector3(-10f + Mathf.Sin(t * .06f) * 1.5f, 5.5f, 5f);
            View.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        }
    }
}
