using UnityEngine;

namespace Outpost.Env
{
    public class EnvFlicker : MonoBehaviour
    {
        public Light target;
        public float baseIntensity = 2f;
        public float amount = 0.4f;
        public float speed = 9f;
        float seed;
        void Start() { seed = Random.value * 100f; if (target == null) target = GetComponent<Light>(); }
        void Update()
        {
            if (target == null) return;
            target.intensity = baseIntensity * (1f + (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount);
        }
    }
}
