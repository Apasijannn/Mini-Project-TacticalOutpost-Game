using System;
using Outpost.Enemy;
using Outpost.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Outpost.UI
{
    [Serializable]
    public sealed class ManualEntry
    {
        public string id, description;
        public GameObject visual;
        public EnemyConfig enemy;
        public FpsWeapon weapon;
        public bool inCurrentWaves;
        public string Title => enemy != null ? enemy.displayName : weapon != null ? weapon.displayName : id;
        public void ApplyTint(GameObject model)
        {
            if (enemy == null || enemy.tint == Color.white) return;
            var tint = new MaterialPropertyBlock(); tint.SetColor("_BaseColor", enemy.tint);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(tint);
        }
        public string Stats => enemy != null
            ? $"HEALTH   {enemy.maxHealth:0} HP\nPLAYER DAMAGE   {enemy.attackDamage:0.#} / hit\nGENERATOR DAMAGE   {enemy.attackDamage * enemy.generatorDamageMultiplier:0.#} / hit\nATTACK INTERVAL   {enemy.attackCooldown:0.##} s\nATTACK REACH   {enemy.attackRange:0.#} m\nWALK / RUN   {enemy.walkSpeed:0.#} / {enemy.runSpeed:0.#} m/s\nHEADSHOT DAMAGE   x{enemy.headshotMultiplier:0.#}" +
                (enemy.kind == EnemyKind.Boss ? $"\nENRAGE   below {enemy.enrageHealthFraction * 100:0}% HP\nENRAGED RUN   {enemy.runSpeed * enemy.enrageSpeedMultiplier:0.#} m/s" : "")
            : weapon != null ? $"DAMAGE   {weapon.damage:0.#} / body hit\nFIRE MODE   {(weapon.automatic ? "Automatic" : "Semi-automatic")}\nFIRE RATE   {weapon.roundsPerMinute:0} RPM\nMAGAZINE   {weapon.magazineSize} rounds\nSTARTING RESERVE   {weapon.reserveAmmo} rounds\nRELOAD   {weapon.reloadTime:0.##} s\nRANGE   {weapon.range:0} m\nHIP / ADS SPREAD   {weapon.hipSpread:0.##} / {weapon.adsSpread:0.##} deg" : "";
    }

    // Visual prefabs contain no combat components; inspection also works while the game is paused.
    public sealed class FieldManualViewer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Transform Model { get; private set; }
        public bool IsDragging { get; private set; }
        public float IdleDelay = 3f;
        Camera view;
        GameObject studio;
        RenderTexture texture;
        float lastInput, yaw, pitch;

        void Awake()
        {
            studio = new GameObject("Field manual studio");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(studio, gameObject.scene);
            studio.transform.position = new Vector3(0, -2000, 0);
            view = new GameObject("Inspection camera", typeof(Camera)).GetComponent<Camera>();
            view.transform.SetParent(studio.transform, false);
            view.cullingMask = 1 << 30; view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.035f, .05f, .052f); view.fieldOfView = 34;
            view.nearClipPlane = .01f; view.farClipPlane = 20; view.allowHDR = true;
            texture = new RenderTexture(768, 640, 24) { name = "Field manual inspection", antiAliasing = 2 };
            texture.Create(); view.targetTexture = texture;
            GetComponent<UnityEngine.UI.RawImage>().texture = texture;
            AddLight(new Vector3(-2, 3, -3), new Color(1f, .88f, .72f), 18f);
            AddLight(new Vector3(2, 1, -1), new Color(.5f, .7f, 1f), 10f);
            AddLight(new Vector3(0, 2, 2), new Color(1f, .5f, .17f), 22f);
        }
        void AddLight(Vector3 position, Color color, float intensity)
        {
            var light = new GameObject("Studio light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(studio.transform, false); light.transform.localPosition = position;
            light.type = LightType.Point; light.range = 8; light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << 30; light.shadows = LightShadows.None;
        }
        void OnEnable() { if (studio != null) studio.SetActive(true); IsDragging = false; }
        void OnDisable() { if (studio != null) studio.SetActive(false); IsDragging = false; }
        void OnDestroy()
        {
            if (studio != null) Destroy(studio);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
        public void Show(ManualEntry entry)
        {
            if (Model != null) { Model.gameObject.SetActive(false); Destroy(Model.gameObject); }
            if (entry == null || entry.visual == null) return;
            Model = new GameObject("Turntable").transform; Model.SetParent(studio.transform, false);
            var model = Instantiate(entry.visual, Model); model.SetActive(true);
            entry.ApplyTint(model);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            foreach (var animator in model.GetComponentsInChildren<Animator>())
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.applyRootMotion = false; animator.Rebind(); animator.Update(0);
            }
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = 2.1f / Mathf.Max(.001f, size);
            model.transform.localPosition = -(bounds.center - Model.position) * scale;
            model.transform.localScale *= scale;
            yaw = entry.weapon != null ? -45 : 175; pitch = entry.weapon != null ? -10 : 0;
            Model.localRotation = Quaternion.Euler(pitch, yaw, 0);
            view.transform.localPosition = new Vector3(0, .05f, -4.8f);
            view.transform.localRotation = Quaternion.identity;
            lastInput = Time.unscaledTime; IsDragging = false;
        }
        void Update()
        {
            if (Model == null) return;
            if (!IsDragging && Time.unscaledTime - lastInput > IdleDelay) yaw += 16f * Time.unscaledDeltaTime;
            Model.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }
        public void OnBeginDrag(PointerEventData e) { if(e.button == PointerEventData.InputButton.Left) IsDragging = true; }
        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging) return;
            float scale = GetComponentInParent<Canvas>().scaleFactor;
            yaw -= e.delta.x * .45f / scale; pitch = Mathf.Clamp(pitch + e.delta.y * .2f / scale, -25, 25);
            lastInput = Time.unscaledTime;
        }
        public void OnEndDrag(PointerEventData e) { IsDragging = false; lastInput = Time.unscaledTime; }
    }
}
