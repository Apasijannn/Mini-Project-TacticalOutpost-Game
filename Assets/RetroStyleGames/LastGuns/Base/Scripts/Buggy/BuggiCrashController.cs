using UnityEngine;

namespace Gadd420
{
    public class BuggiCrashController : MonoBehaviour
    {
        public string[] crashTag;
        public float decelerationSpeedForCrash = 8f;

        Rigidbody rb;
        BuggiController car;

        float rbSpeed;
        float lateRbSpeed;
        float counter;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            car = GetComponent<BuggiController>();
        }

        void Update()
        {
            if (!rb || !car) return;

            if (counter == 0f) rbSpeed = rb.linearVelocity.magnitude;

            counter += Time.deltaTime;

            if (counter >= 0.05f)
            {
                lateRbSpeed = rb.linearVelocity.magnitude;

                if (rbSpeed - lateRbSpeed > decelerationSpeedForCrash)
                    car.isCrashed = true;

                counter = 0f;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!car) return;

            for (int i = 0; i < crashTag.Length; i++)
            {
                if (other.CompareTag(crashTag[i]))
                    car.isCrashed = true;
            }
        }
    }
}