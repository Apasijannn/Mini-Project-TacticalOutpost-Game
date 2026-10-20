#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Outpost.Env;
using Outpost.Player;

// Membangun prefab Player first person (model Insurgent Urban + AK47) dan menaruhnya di PlayerSpawn.
public static partial class OutpostBuilder
{
    const string CharRoot = "Assets/ArtStore3D/Insurgent (Urban)";
    const string PlayerPrefabPath = "Assets/Outpost/Prefabs/Player_FPS.prefab";

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    // Arahkan tulang supaya segmen (bone -> child) menunjuk ke arah dunia tertentu.
    static void AimBone(Transform bone, Transform child, Vector3 worldDir)
    {
        if (bone == null || child == null) return;
        Vector3 cur = (child.position - bone.position).normalized;
        bone.rotation = Quaternion.FromToRotation(cur, worldDir.normalized) * bone.rotation;
    }

    // Pose "siaga senapan": lengan turun dan menekuk ke depan (hanya terlihat sebagai bayangan player).
    static void PoseBody(Transform body, Transform root)
    {
        Vector3 fwd = root.forward, right = root.right, up = Vector3.up;
        foreach (string side in new[] { "L", "R" })
        {
            var sh = FindDeep(body, "Shoulder_" + side); var el = FindDeep(body, "Elbow_" + side); var wr = FindDeep(body, "Wrist_" + side);
            if (sh == null || el == null || wr == null) continue;
            float sgn = Mathf.Sign(Vector3.Dot(wr.position - sh.position, right));   // +1 bila lengan ada di sisi kanan model
            AimBone(sh, el, sgn * right * 0.22f - up * 0.9f + fwd * 0.3f);
            // lengan bawah: tangan kanan di pelatuk (agak ke dalam), tangan kiri memegang handguard (lebih ke depan)
            Vector3 fore = side == "L" ? (sgn * right * -0.05f - up * 0.05f + fwd * 1f) : (sgn * right * -0.35f - up * 0.25f + fwd * 0.85f);
            AimBone(el, wr, fore);
        }
    }

    static Material EnsureTracerMat()
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/M_tracer.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject BuildPlayerPrefab()
    {
        Log("konversi material karakter: " + ConvertMaterialsIn("Assets/ArtStore3D"));
        AssetDatabase.SaveAssets();
        var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharRoot + "/Prefab/InsurgentUrban.prefab");
        var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharRoot + "/Prefab/Ak47.prefab");
        if (charPrefab == null || gunPrefab == null) { Log("PREFAB KARAKTER/AK47 TIDAK DITEMUKAN di " + CharRoot); return null; }

        var root = new GameObject("Player");
        root.layer = LPlayer;
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.04f;

        // badan: hanya melempar bayangan (tidak terlihat dari kamera orang pertama)
        var body = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab, root.transform);
        body.name = "Body";
        body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
        var anim = body.GetComponent<Animator>(); if (anim != null) anim.enabled = false;
        PoseBody(body.transform, root.transform);
        // badan terlihat dari kamera orang pertama (lengan memegang senjata); topi & masker (kepala) hanya melempar bayangan
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            bool head = r.name == "Hat" || r.name == "Mask";
            r.shadowCastingMode = head ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            r.receiveShadows = !head;
        }
        SetLayerRecursive(body, LPlayer);

        // kamera
        var camRoot = new GameObject("CameraRoot"); camRoot.transform.SetParent(root.transform, false);
        camRoot.transform.localPosition = new Vector3(0, 1.62f, 0);
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; camGo.transform.SetParent(camRoot.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.03f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // komponen gameplay
        var ctrl = root.AddComponent<FpsController>(); ctrl.cameraRoot = camRoot.transform; ctrl.playerCamera = cam;
        var hp = root.AddComponent<PlayerHealth>();
        // 2 senjata: AK-47 (1), pistol (2)
        WireFootstepAudio(ctrl);
        var mgr = BuildWeapons(camGo.transform, ctrl);
        var hud = root.AddComponent<FpsHud>(); hud.manager = mgr; hud.health = hp; hud.controller = ctrl;
        WireHudSkin(hud);
        var fb = root.AddComponent<FpsBody>(); fb.body = body.transform; fb.cameraTransform = camGo.transform; fb.manager = mgr; fb.controller = ctrl; fb.cameraRoot = camRoot.transform;

        EnsureFolder("Assets/Outpost/Prefabs");
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
        Log("prefab player disimpan: " + PlayerPrefabPath);
        return saved;
    }

    static void BuildPlayer()
    {
        var old = GameObject.Find("Main Camera");
        if (old != null) Object.DestroyImmediate(old);
        var prefab = BuildPlayerPrefab();
        if (prefab == null) return;
        Vector3 pos = new Vector3(0, 0.05f, 0); float yaw = 270f;
        foreach (var s in Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None))
            if (s.group == "Player") { pos = s.transform.position + new Vector3(0, 0.05f, 0); yaw = s.transform.eulerAngles.y; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.position = pos; inst.transform.rotation = Quaternion.Euler(0, yaw, 0);
        inst.tag = "Player";
        Log("player ditempatkan di " + pos);
    }
}
#endif
