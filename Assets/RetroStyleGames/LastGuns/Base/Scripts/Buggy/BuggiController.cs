using UnityEngine;

namespace Gadd420
{
    public class BuggiController : MonoBehaviour
    {
        public enum DriveType { FWD, RWD, AWD }
        public enum WheelVisualRotationMode { UseWheelColliderPose, ManualRPM }
        public enum ManualWheelSpinAxis { X, Y, Z }

        [Header("Drive")]
        public DriveType driveType = DriveType.RWD;

        [Range(0f, 1f)]
        public float awdFrontTorqueSplit = 0.5f; 

        [Header("Input")]
        public bool useInputManagerIfPresent = true;
        public Input_Manager inputManager; 

        [Header("Units")]
        public bool useKmph = false;

        [Header("Engine (speed in MPH or KMPH)")]
        public float maxSpeed = 160f;
        public float maxReverseSpeed = 20f;

        [Header("Torque")]
        public bool isAutomatic = true;
        public float firstGearTorque = 1500f;
        public float topGearTorque = 800f;

        [Header("Gears")]
        public int numGears = 6;

        [Header("Engine Braking")]
        public bool useEngineBraking = true;
        public float engineBrakingDrag = 0.25f;

        [Header("Steering")]
        public float minSteerAngle = 6f;
        public float maxSteerAngle = 28f;
        public float speedForMinSteer = 90f; 

        [Header("Brakes")]
        public float brakeTorque = 2500f;
        public float handbrakeTorque = 4000f;

        [Header("Wheels (0 FL, 1 FR, 2 RL, 3 RR)")]
        public WheelCollider[] wheelColliders = new WheelCollider[4];
        public Transform[] wheelVisuals = new Transform[4];

        [Header("Wheel Visuals")]
        public WheelVisualRotationMode wheelVisualRotationMode = WheelVisualRotationMode.UseWheelColliderPose;
        public ManualWheelSpinAxis manualSpinAxis = ManualWheelSpinAxis.X;

        [Header("Debug")]
        public float currentSpeed; 
        [HideInInspector] public bool isCrashed;

        Rigidbody rb;

        // internal
        float maxSpeedMs;
        float maxReverseSpeedMs;
        float speedForMinSteerMs;

        float currentSpeedMs;
        float forwardSpeedMs;
        float ogDrag;

        float ogTorque;
        float torqueDifference;
        int currentGear = 1;
        float currentGearPercent;
        float rpm;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ogDrag = rb.linearDamping;

            if (useInputManagerIfPresent && !inputManager)
                inputManager = GetComponent<Input_Manager>();

            ogTorque = firstGearTorque;

            if (!isAutomatic)
            {
                numGears = Mathf.Max(1, numGears);
                torqueDifference = (firstGearTorque - topGearTorque) / Mathf.Max(1, (numGears - 1));
            }
            else
            {
                numGears = 1;
            }

            float unitsToMs = useKmph ? (1f / 3.6f) : (1f / 2.237f);
            maxSpeedMs = maxSpeed * unitsToMs;
            maxReverseSpeedMs = maxReverseSpeed * unitsToMs;
            speedForMinSteerMs = speedForMinSteer * unitsToMs;
        }

        void Update()
        {
            currentSpeedMs = rb.linearVelocity.magnitude;
            forwardSpeedMs = Vector3.Dot(rb.linearVelocity, transform.forward);

            float msToUnits = useKmph ? 3.6f : 2.237f;
            currentSpeed = currentSpeedMs * msToUnits;

            UpdateWheelVisuals();
        }

        void FixedUpdate()
        {
            if (isCrashed)
            {
                ApplyBrakes(1f, 1f);
                ApplyMotorTorque(0f);
                return;
            }

            ReadInput(out float steer, out float throttle, out float brake, out float handbrake);

            float steerAngle = CalculateSteerAngle(steer);
            ApplySteering(steerAngle);

            float engineTorque = CalculateEngineTorque(throttle);
            ApplyDrive(throttle, engineTorque, brake, handbrake);

            EngineBraking(throttle);
        }

        void ReadInput(out float steer, out float throttle, out float brake, out float handbrake)
        {
            if (useInputManagerIfPresent && inputManager)
            {
                steer = Mathf.Clamp(inputManager.HzInput, -1f, 1f);
                throttle = Mathf.Clamp(inputManager.VInput, -1f, 1f);
                brake = Mathf.Clamp01(inputManager.FrontBreakInput);
            }
            else
            {
                steer = Mathf.Clamp(Input_Compat.GetHorizontal(), -1f, 1f);
                throttle = Mathf.Clamp(Input_Compat.GetVertical(), -1f, 1f);
                brake = Input_Compat.GetBrakeHeld() ? 1f : 0f;
            }

            handbrake = Input_Compat.GetHandbrakeHeld() ? 1f : 0f;
        }

        float CalculateSteerAngle(float steerInput)
        {
            float t = (speedForMinSteerMs <= 0.01f) ? 1f : Mathf.Clamp01(currentSpeedMs / speedForMinSteerMs);
            float angle = Mathf.Lerp(maxSteerAngle, minSteerAngle, t) * steerInput;
            return angle;
        }

        float CalculateEngineTorque(float throttleInput)
        {
            if (Mathf.Abs(throttleInput) < 0.001f) return 0f;

     
            if (throttleInput > 0f && currentSpeedMs > maxSpeedMs) return 0f;

        
            if (throttleInput < 0f && Mathf.Abs(forwardSpeedMs) > maxReverseSpeedMs) return 0f;

           
            if (isAutomatic)
            {
                float torqueLerp = Mathf.Lerp(ogTorque, topGearTorque, (maxSpeedMs <= 0.01f) ? 1f : (currentSpeedMs / maxSpeedMs));
                firstGearTorque = torqueLerp;
            }
            else
            {
                if (currentGear == 0) firstGearTorque = ogTorque;
                else if (currentGear == numGears - 1) firstGearTorque = topGearTorque;
                else firstGearTorque = ogTorque - torqueDifference * currentGear;

                UpdateManualGear();
            }

            return firstGearTorque * throttleInput;
        }

        void UpdateManualGear()
        {
            float speedPercent = (maxSpeedMs <= 0.01f) ? 0f : Mathf.Abs(currentSpeedMs / maxSpeedMs);
            float gearPercentage = 1f / (float)numGears;

            float upperGearMax = gearPercentage * (currentGear + 1);
            float downGearMax = gearPercentage * currentGear;

            if (currentGear > 0 && speedPercent < downGearMax) currentGear--;
            if (speedPercent > upperGearMax && currentGear < (numGears - 1)) currentGear++;

            float targetGearFactor = Mathf.InverseLerp(
                gearPercentage * currentGear,
                gearPercentage * (currentGear + 1),
                speedPercent
            );

            currentGearPercent = Mathf.Lerp(currentGearPercent, targetGearFactor, Time.fixedDeltaTime * 5f);
            float gearNumFactor = currentGear / (float)numGears;
            rpm = Mathf.Lerp(gearNumFactor, 1f, currentGearPercent);
        }

        void ApplySteering(float steerAngle)
        {
    
            if (wheelColliders[0]) wheelColliders[0].steerAngle = steerAngle;
            if (wheelColliders[1]) wheelColliders[1].steerAngle = steerAngle;
        }

        void ApplyDrive(float throttleInput, float engineTorqueSigned, float brakeInput, float handbrakeInput)
        {
            // brake logic (service brake)
            ApplyBrakes(brakeInput, handbrakeInput);

            // if braking hard, don't push motor
            if (brakeInput > 0.01f)
            {
                ApplyMotorTorque(0f);
                return;
            }

            if (throttleInput < -0.01f && forwardSpeedMs > 1f)
            {
                ApplyBrakes(1f, handbrakeInput);
                ApplyMotorTorque(0f);
                return;
            }

            ApplyMotorTorque(engineTorqueSigned);
        }

        void ApplyMotorTorque(float torque)
        {
            float fl = 0f, fr = 0f, rl = 0f, rr = 0f;

            if (driveType == DriveType.FWD)
            {
                fl = torque * 0.5f;
                fr = torque * 0.5f;
            }
            else if (driveType == DriveType.RWD)
            {
                rl = torque * 0.5f;
                rr = torque * 0.5f;
            }
            else // AWD
            {
                float front = torque * Mathf.Clamp01(awdFrontTorqueSplit);
                float rear = torque - front;

                fl = front * 0.5f;
                fr = front * 0.5f;
                rl = rear * 0.5f;
                rr = rear * 0.5f;
            }

            if (wheelColliders[0]) wheelColliders[0].motorTorque = fl;
            if (wheelColliders[1]) wheelColliders[1].motorTorque = fr;
            if (wheelColliders[2]) wheelColliders[2].motorTorque = rl;
            if (wheelColliders[3]) wheelColliders[3].motorTorque = rr;
        }

        void ApplyBrakes(float brakeInput, float handbrakeInput)
        {
            float bt = brakeTorque * Mathf.Clamp01(brakeInput);

            // service brake on all wheels
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i]) wheelColliders[i].brakeTorque = bt;
            }

            // handbrake only on rear axle: RL, RR
            float hbt = handbrakeTorque * Mathf.Clamp01(handbrakeInput);
            if (wheelColliders[2]) wheelColliders[2].brakeTorque = Mathf.Max(wheelColliders[2].brakeTorque, hbt);
            if (wheelColliders[3]) wheelColliders[3].brakeTorque = Mathf.Max(wheelColliders[3].brakeTorque, hbt);
        }

        void EngineBraking(float throttleInput)
        {
            if (useEngineBraking && Mathf.Abs(throttleInput) < 0.001f && IsAnyWheelGrounded())
                rb.linearDamping = engineBrakingDrag;
            else
                rb.linearDamping = ogDrag;
        }

        bool IsAnyWheelGrounded()
        {
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] && wheelColliders[i].isGrounded) return true;
            }
            return false;
        }

        void UpdateWheelVisuals()
        {
            for (int i = 0; i < 4; i++)
            {
                var wc = wheelColliders[i];
                var wv = wheelVisuals[i];
                if (!wc || !wv) continue;

                wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
                wv.position = pos;

                if (wheelVisualRotationMode == WheelVisualRotationMode.UseWheelColliderPose)
                {
                    wv.rotation = rot;
                }
                else
                {
                    float wheelRPM = wc.rpm;
                    if (rb.linearVelocity.sqrMagnitude > 0.0001f)
                    {
                        Vector3 axis = (manualSpinAxis == ManualWheelSpinAxis.X) ? Vector3.right :
                                       (manualSpinAxis == ManualWheelSpinAxis.Y) ? Vector3.up : Vector3.forward;

                        wv.Rotate(axis * wheelRPM * 6f * Time.deltaTime);
                    }
                }
            }
        }
    }
}