using System.Collections;
using Outpost.Enemy;
using Outpost.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Outpost
{
    public sealed class GeneratorDestruction : MonoBehaviour
    {
        [System.Serializable] public sealed class Breakable { public string path; public GameObject debris; }
        public GameObject explosion, fire, debris;
        public AudioClip generatorBlastSound, buildingBlastSound, tankBlastSound;
        public string generatorPath;
        public string[] pipePaths;
        public string buildingPath;
        public GameObject buildingDebris;
        public Vector3 buildingCenter;
        public Vector3[] blastSites;
        public Breakable[] tanks, containmentWalls;
        public bool Playing { get; private set; }
        public bool Finished { get; private set; }
        public Camera View { get; private set; }
        public int BlastsTriggered { get; private set; }
        public int DebrisCount { get; private set; }
        public int BuildingDebrisCount { get; private set; }
        public int TanksDestroyed { get; private set; }
        public int WallsDestroyed { get; private set; }
        public int SecondaryDebrisCount { get; private set; }
        public float TankBurstTime { get; private set; }
        public IEnumerator Play(GeneratorHealth generator)
        {
            if (Playing || Finished) yield break;
            Playing = true; Time.timeScale = 1f;
            Outpost.UI.MinimapController.CloseLargeMap();
            foreach (var debug in FindObjectsByType<EnemyDebugSpawner>(FindObjectsSortMode.None)) debug.enabled = false;
            foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                brain.enabled = false;
                var agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
            foreach (var hud in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) hud.gameObject.SetActive(false);
            var player = PlayerHealth.Instance;
            var oldView = player != null ? player.GetComponent<FpsController>().playerCamera : Camera.main;
            View = new GameObject("Blackout drone", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            View.transform.SetParent(transform, false);
            if (oldView != null)
            {
                View.CopyFrom(oldView); oldView.enabled = false;
                var listener = oldView.GetComponent<AudioListener>(); if (listener != null) listener.enabled = false;
            }
            View.tag = "MainCamera"; View.enabled = true; View.targetTexture = null;
            View.cullingMask = ~((1 << 8) | (1 << 14) | (1 << 30));
            View.fieldOfView = 53; View.nearClipPlane = .3f; View.farClipPlane = 500;
            View.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Vector3 center = generator.transform.position;
            var environment = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
            Transform source = Resolve(environment, generatorPath);
            float elapsed = 0; float shake = 0;
            bool primaryTriggered = false, tanksTriggered = false, buildingTriggered = false;
            while (elapsed < 12f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (primaryTriggered && elapsed >= 1.7f && !buildingTriggered && buildingDebris != null)
                {
                    buildingTriggered = true;
                    var building = Resolve(environment, buildingPath);
                    PlayBlast(buildingBlastSound, buildingCenter, .4f);
                    float floor = building != null ? building.GetComponent<MeshRenderer>().bounds.min.y + .4f : 1f;
                    Blast(buildingCenter + Vector3.up * 5.5f, 3f, new Vector3(buildingCenter.x, floor, buildingCenter.z)); shake = .8f;
                    BuildingDebrisCount = Break(building, buildingDebris, buildingCenter, 260);
                }
                if (elapsed >= 2.2f && !tanksTriggered)
                {
                    tanksTriggered = true; TankBurstTime = Time.unscaledTime;
                    PlayBlast(tankBlastSound, center, .25f);
                    foreach (var position in blastSites) Blast(position, 2.3f, new Vector3(position.x, generator.Body.bounds.min.y + .4f, position.z));
                    var positions = new HashSet<Vector3Int>();
                    foreach (var tank in tanks)
                    {
                        var target = Resolve(environment, tank.path); if (target == null) continue;
                        var position = target.GetComponent<MeshRenderer>().bounds.center;
                        if (positions.Add(Vector3Int.RoundToInt(position * 100))) SecondaryDebrisCount += Break(target, tank.debris, position, 100, 45, true);
                        else target.gameObject.SetActive(false);
                        TanksDestroyed++;
                    }
                    foreach (var wall in containmentWalls)
                    {
                        var target = Resolve(environment, wall.path); if (target == null) continue;
                        SecondaryDebrisCount += Break(target, wall.debris, center, 90, 15, true); WallsDestroyed++;
                    }
                    shake = .8f;
                }
                if (elapsed >= 1.2f && !primaryTriggered)
                {
                    primaryTriggered = true;
                    PlayBlast(generatorBlastSound, center, .55f);
                    Blast(center + Vector3.up * 4f, 3.5f, new Vector3(center.x, generator.Body != null ? generator.Body.bounds.min.y + .4f : 1f, center.z));
                    shake = 1f;
                    {
                        if (pipePaths != null)
                            foreach (string path in pipePaths)
                            {
                                Transform pipe = Resolve(environment, path);
                                if (pipe == null || pipe.GetComponent<MeshFilter>() == null) continue;
                                var piece = new GameObject("Ruptured pipe", typeof(MeshFilter), typeof(MeshRenderer)); piece.transform.SetParent(transform, false);
                                piece.transform.SetPositionAndRotation(pipe.position, pipe.rotation); piece.transform.localScale = pipe.lossyScale;
                                piece.GetComponent<MeshFilter>().sharedMesh = pipe.GetComponent<MeshFilter>().sharedMesh;
                                piece.GetComponent<MeshRenderer>().sharedMaterials = pipe.GetComponent<MeshRenderer>().sharedMaterials;
                                var collider = piece.AddComponent<BoxCollider>(); var bounds = piece.GetComponent<MeshFilter>().sharedMesh.bounds;
                                collider.center = bounds.center; collider.size = Vector3.Max(bounds.size, Vector3.one * .05f);
                                var body = piece.AddComponent<Rigidbody>(); body.mass = 12;
                                body.AddExplosionForce(140, pipe.GetComponent<MeshRenderer>().bounds.center - Vector3.up * 2, 8, 2, ForceMode.Impulse);
                                body.AddTorque(Random.insideUnitSphere * 30, ForceMode.Impulse);
                                pipe.gameObject.SetActive(false);
                            }
                        if (source != null && debris != null)
                        {
                            DebrisCount = Break(source, debris, center, 340);
                        }
                    }
                }
                float angle = (-135 + elapsed * 7.5f) * Mathf.Deg2Rad;
                Vector3 cameraTarget = Vector3.Lerp(center, buildingCenter, .32f);
                Vector3 positionView = cameraTarget + new Vector3(Mathf.Sin(angle) * 62, 34 + Mathf.Sin(elapsed * .2f), Mathf.Cos(angle) * 62);
                shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 1.8f);
                Vector3 jitter = new Vector3(Mathf.Sin(elapsed * 53), Mathf.Cos(elapsed * 67), Mathf.Sin(elapsed * 47)) * shake * .5f;
                View.transform.SetPositionAndRotation(positionView + jitter, Quaternion.LookRotation(cameraTarget - positionView) * Quaternion.Euler(jitter * 1.8f));
                yield return null;
            }
            Playing = false; Finished = true;
        }
        static Transform Resolve(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects()) if (path != null && path.StartsWith(root.name + "/")) return root.transform.Find(path.Substring(root.name.Length + 1));
            return null;
        }
        int Break(Transform source, GameObject prefab, Vector3 center, float force, float torque = 90, bool scatter = false)
        {
            if (source == null) return 0;
            var broken = Instantiate(prefab, source.position, source.rotation, transform); broken.transform.localScale = source.lossyScale;
            int count = 0;
            foreach (Transform piece in broken.transform)
            {
                var mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                var collider = piece.gameObject.AddComponent<BoxCollider>(); collider.center = mesh.bounds.center; collider.size = Vector3.Max(mesh.bounds.size, Vector3.one * .05f);
                var body = piece.gameObject.AddComponent<Rigidbody>(); body.mass = 25; body.linearDamping = .35f;
                body.AddExplosionForce(force, center - Vector3.up * 3, 25, 4, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse); count++;
                if (scatter)
                {
                    Vector3 outward = piece.position - center; outward.y = 0;
                    if (outward.sqrMagnitude < .01f) outward = Random.onUnitSphere;
                    body.AddForce(outward.normalized * 2.8f + Vector3.up * 1.2f, ForceMode.VelocityChange);
                }
            }
            source.gameObject.SetActive(false); return count;
        }
        void PlayBlast(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;
            var sound = new GameObject(clip.name, typeof(AudioSource)).GetComponent<AudioSource>();
            sound.transform.SetParent(transform, false); sound.transform.position = position;
            sound.playOnAwake = false; sound.clip = clip; sound.volume = volume;
            sound.spatialBlend = .65f; sound.rolloffMode = AudioRolloffMode.Linear;
            sound.minDistance = 30; sound.maxDistance = 200; sound.dopplerLevel = 0; sound.spread = 120; sound.priority = 40;
            sound.Play(); Destroy(sound.gameObject, clip.length + .2f);
        }
        void Blast(Vector3 position, float scale, Vector3 firePosition)
        {
            if (explosion != null)
            {
                var effect = Instantiate(explosion, position, Quaternion.identity, transform); effect.transform.localScale *= scale;
                StartEffect(effect);
                Destroy(effect, 12);
            }
            if (fire != null)
            {
                var flame = Instantiate(fire, firePosition, Quaternion.identity, transform); flame.transform.localScale *= scale * 1.7f;
                foreach (var t in flame.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Distortion")) t.gameObject.SetActive(false);
                StartEffect(flame);
            }
            var flash = new GameObject("Explosion flash", typeof(Light)).GetComponent<Light>();
            flash.transform.SetParent(transform, false); flash.transform.position = position + Vector3.up * 3;
            flash.color = new Color(1f, .52f, .13f); flash.range = 28; flash.intensity = 40; flash.type = LightType.Point;
            StartCoroutine(FadeFlash(flash)); BlastsTriggered++;
        }
        static void StartEffect(GameObject effect)
        {
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>()) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.useUnscaledTime = true;
                particles.Play(false);
            }
        }
        IEnumerator FadeFlash(Light light)
        {
            float elapsed = 0;
            while (elapsed < .7f) { elapsed += Time.unscaledDeltaTime; light.intensity = Mathf.Lerp(40, 0, elapsed / .7f); yield return null; }
            Destroy(light.gameObject);
        }
    }
}
