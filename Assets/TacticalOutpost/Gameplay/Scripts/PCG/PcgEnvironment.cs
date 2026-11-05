using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost.PCG
{
    /// <summary>
    /// Isi scene khusus mode PCG (Assets/Scenes/PCG/TacticalOutpost_PCG.unity) yang bisa diedit manual:
    ///  - Atmosphere: cahaya bulan (directional light), Global Volume (post-processing), langit/fog di Lighting settings scene.
    ///  - Templates: generator + lampu merah, menara floodlight, lampu jalan. Arena menyalin objek ini, jadi edit di sini
    ///    (warna/terang lampu, material, tambah child) langsung terbawa ke arena. Root Templates dimatikan saat Play.
    ///  - Static Objects: objek tambahan yang selalu ada di arena. Ikut di-bake ke NavMesh; obstacle PCG menghindarinya.
    /// Bagian procedural (pagar + gerbang, obstacle, props, spawn point, supply, komposisi zombie) tetap dibangun saat Play.
    /// </summary>
    public sealed class PcgEnvironment : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/PCG/TacticalOutpost_PCG.unity";

        public PcgProfile profile;
        public Transform atmosphere;
        public Transform templatesRoot;
        public GameObject generatorTemplate, towerTemplate, lampTemplate;
        public Transform staticObjects;

        public static PcgEnvironment FindIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var env = root.GetComponentInChildren<PcgEnvironment>(true);
                if (env != null) return env;
            }
            return null;
        }

#if UNITY_EDITOR
        float cachedHalf = -1f;

        /// <summary>Di Scene View (mode edit): perkiraan batas arena dan posisi generator, panduan menaruh Static Objects.</summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            var p = profile != null ? profile : Resources.Load<PcgProfile>(PcgProfile.ResourcePath);
            if (p == null || p.fenceSpan == null) return;
            if (cachedHalf < 0f)
            {
                float span = 0f;
                foreach (var mf in p.fenceSpan.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null)
                    {
                        Vector3 sz = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                        span = Mathf.Max(span, Mathf.Max(sz.x, sz.z));
                    }
                cachedHalf = p.spansPerSide * Mathf.Max(1f, span) * 0.5f;
            }
            float h = cachedHalf;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(h * 2f, 0.1f, h * 2f));
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.15f);
            Gizmos.DrawCube(new Vector3(0f, 0.02f, 0f), new Vector3(h * 2f, 0.02f, h * 2f));
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(Vector3.zero, p.generatorClearRadius);
        }
#endif
    }
}
