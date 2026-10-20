using UnityEngine;

namespace Outpost.Env
{
    public class EnvBlink : MonoBehaviour
    {
        public Renderer target;
        public Light lightTarget;
        public float period = 1.4f;
        void Update()
        {
            bool on = (Time.time % period) < period * 0.25f;
            if (target != null) target.enabled = on;
            if (lightTarget != null) lightTarget.enabled = on;
        }
    }
}
