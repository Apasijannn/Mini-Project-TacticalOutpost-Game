using UnityEngine;

namespace Outpost.Env
{
    public class EnvSearchlight : MonoBehaviour
    {
        public float sweepAngle = 40f;
        public float speed = 0.35f;
        public float phase;
        Quaternion baseRot;
        void Start() { baseRot = transform.localRotation; }
        void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * sweepAngle;
            transform.localRotation = baseRot * Quaternion.Euler(0f, a, 0f);
        }
    }
}
