using UnityEngine;

namespace Gadd420
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuggiAntiRollBar : MonoBehaviour
    {
        [Header("Axles")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;

        [Header("Forces")]
        public float frontAntiRoll = 5000f;
        public float rearAntiRoll = 5000f;

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            ApplyAntiRoll(frontLeft, frontRight, frontAntiRoll);
            ApplyAntiRoll(rearLeft, rearRight, rearAntiRoll);
        }

        void ApplyAntiRoll(WheelCollider left, WheelCollider right, float antiRollForce)
        {
            if (!left || !right) return;

            float travelL = 1f;
            float travelR = 1f;

            WheelHit hit;

            bool groundedL = left.GetGroundHit(out hit);
            if (groundedL)
            {
                travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;
            }

            bool groundedR = right.GetGroundHit(out hit);
            if (groundedR)
            {
                travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;
            }

            float antiRoll = (travelL - travelR) * antiRollForce;

            if (groundedL)
                rb.AddForceAtPosition(left.transform.up * -antiRoll, left.transform.position);

            if (groundedR)
                rb.AddForceAtPosition(right.transform.up * antiRoll, right.transform.position);
        }
    }
}