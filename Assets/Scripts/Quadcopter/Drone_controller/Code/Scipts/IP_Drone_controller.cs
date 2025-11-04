// IP_Drone_controller.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace IndiePixel
{
    [RequireComponent(typeof(IP_Drone_inputs))]
    public class IP_Drone_controller : IP_Base_Rigidbody
    {
        #region Variables
        [Header("Control Properties")]
        [SerializeField] private float minMaxPitch = 30f;
        [SerializeField] private float minMaxRoll = 30f;
        [SerializeField] private float yawPower = 4f;
        [SerializeField] private float lerpSpeed = 2f;

        [Header("Stabilization Properties")]
        [Tooltip("How strongly the drone brakes against horizontal movement when there is no input.")]
        [SerializeField] private float horizontalDamping = 2f;
        
        // --- ADD THIS LINE ---
        [Tooltip("How strongly the drone brakes against vertical movement when throttle is neutral.")]
        [SerializeField] private float verticalDamping = 2f;
        // --- END ADDED LINE ---

        [Tooltip("How small the input needs to be to be considered 'zero'.")]
        [SerializeField] private float inputDeadzone = 0.1f;

        private IP_Drone_inputs input;
        private List<IEngine> engines = new List<IEngine>();
        private float finalPitch;
        private float finalRoll;
        private float yaw;
        private float finalYaw;
        
        private Target targetComponent;
        #endregion

        #region Main Methods
        // Start is called before the first frame update
        void Start()
        {
            input = GetComponent<IP_Drone_inputs>();
            engines = GetComponentsInChildren<IEngine>().ToList<IEngine>();
            targetComponent = GetComponent<Target>();

            yaw = rb.rotation.eulerAngles.y;
            finalYaw = yaw;
        }
        #endregion

        #region Custom Methods
        protected override void HandlePhysics()
        {
            if (targetComponent == null || !targetComponent.IsAlive)
            {
                return;
            }
            
            HandleEngines();
            HandleControls();
            HandleStabilization();
        }

        protected virtual void HandleEngines()
        {
            foreach (IEngine engine in engines)
            {
                engine.UpdateEngine(rb, input);
            }
        }

        protected virtual void HandleControls()
        {
            float pitch = input.Cyclic.y * minMaxPitch;
            float roll = -input.Cyclic.x * minMaxRoll;
            yaw += input.Pedals * yawPower; 

            finalPitch = Mathf.Lerp(finalPitch, pitch, Time.deltaTime * lerpSpeed);
            finalRoll = Mathf.Lerp(finalRoll, roll, Time.deltaTime * lerpSpeed);
            finalYaw = Mathf.Lerp(finalYaw, yaw, Time.deltaTime * lerpSpeed);

            Quaternion targetRotation = Quaternion.Euler(0, finalYaw, 0) * Quaternion.Euler(finalPitch, 0, finalRoll);

            rb.MoveRotation(targetRotation);
        }
        
        // --- MODIFY THIS METHOD ---
        protected virtual void HandleStabilization()
        {
            // Check if there is no pitch/roll input
            if (input.Cyclic.magnitude < inputDeadzone)
            {
                // Get the current horizontal velocity (ignoring vertical)
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

                // Apply an opposing force to "brake" the drone
                rb.AddForce(-horizontalVelocity * horizontalDamping, ForceMode.Acceleration);
            }

            // --- ADD THIS BLOCK ---
            // Check if there is no throttle input
            if (Mathf.Abs(input.Throttle) < inputDeadzone)
            {
                // Get the current vertical-only velocity
                Vector3 verticalVelocity = new Vector3(0, rb.linearVelocity.y, 0);

                // Apply an opposing force to "brake" vertical momentum
                rb.AddForce(-verticalVelocity * verticalDamping, ForceMode.Acceleration);
            }
            // --- END ADDED BLOCK ---
        }
        // --- END MODIFICATION ---
        #endregion
    }
}