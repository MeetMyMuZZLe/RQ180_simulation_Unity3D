// AI_Drone_IdlePatrol.cs
using System.Collections.Generic;
using UnityEngine;

namespace IndiePixel
{
    [RequireComponent(typeof(IP_Drone_inputs), typeof(Target))]
    public class AI_Drone_IdlePatrol : MonoBehaviour
    {
        [Header("Patrol Area")]
        [Tooltip("An empty GameObject marking the center of the patrol zone.")]
        [SerializeField] private Transform outpostCenter;
        [Tooltip("How far (in meters) the drone will stray from the center.")]
        [SerializeField] private float patrolRadius = 200f;
        [Tooltip("The altitude the drone will try to maintain.")]
        [SerializeField] private float targetAltitude = 150f;
        [SerializeField] private bool useStartingAltitude = false;

        [Header("Movement Tuning")]
        [Tooltip("How close the drone needs to get to its random point before stopping.")]
        [SerializeField] private float destinationReachedThreshold = 10f;
        [Tooltip("Min/Max seconds the drone will wait before picking a new spot.")]
        [SerializeField] private Vector2 loiterTimeRange = new Vector2(3f, 8f);
        [SerializeField] private float altitudeSensitivity = 1f;
        [Tooltip("How fast the drone turns. Keep this value low!")]
        [SerializeField] private float yawSensitivity = 0.05f;
        [Tooltip("The angle (in degrees) where the drone stops turning to prevent 'hunting'.")]
        [SerializeField] private float yawStopThreshold = 2f;
        
        // --- Private State ---
        private enum AIState { Flying, Loitering }
        private AIState currentState = AIState.Loitering;

        private IP_Drone_inputs input;
        private Target targetComponent;
        private bool isDead = false;
        
        private Vector3 randomDestination;
        private float loiterTimer = 0f;

        void Awake()
        {
            input = GetComponent<IP_Drone_inputs>();
            targetComponent = GetComponent<Target>();

            if (useStartingAltitude)
            {
                targetAltitude = transform.position.y;
            }
        }

        void Start()
        {
            // Start by picking a new spot
            PickNewDestination();
            currentState = AIState.Flying;
        }

        void PickNewDestination()
        {
            if (outpostCenter == null) return;

            // Pick a random spot inside a 2D circle and set the altitude
            Vector2 randomCirclePos = Random.insideUnitCircle * patrolRadius;
            randomDestination = new Vector3(
                outpostCenter.position.x + randomCirclePos.x,
                targetAltitude,
                outpostCenter.position.z + randomCirclePos.y
            );

            // Start the loiter timer (we'll use it when we arrive)
            loiterTimer = Random.Range(loiterTimeRange.x, loiterTimeRange.y);
        }

        void Update()
        {
            // --- 1. DEATH CHECK ---
            if (input == null || targetComponent == null || !targetComponent.IsAlive)
            {
                if (!isDead)
                {
                    isDead = true;
                    Debug.Log($"[AI_Drone: {gameObject.name}] State: DEAD. Halting all inputs.");
                }
                
                input.Cyclic = Vector2.zero;
                input.Pedals = 0f;
                input.Throttle = 0f;
                return;
            }

            // --- 2. CHECK FOR OUPTOST CENTER ---
            if (outpostCenter == null)
            {
                if (Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning($"[AI_Drone: {gameObject.name}] No 'Outpost Center' transform assigned! Holding position.");
                }
                HoldPosition(targetAltitude); // Just hover if no center is set
                return;
            }

            // --- 3. STATE MACHINE ---
            switch (currentState)
            {
                case AIState.Flying:
                    // Fly towards our randomDestination
                    FlyToTarget(randomDestination);

                    // Check if we've arrived
                    float distance2D = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z), 
                        new Vector2(randomDestination.x, randomDestination.z)
                    );

                    if (distance2D < destinationReachedThreshold)
                    {
                        // We've arrived. Switch to loitering.
                        currentState = AIState.Loitering;
                    }
                    break;

                case AIState.Loitering:
                    // Hover in place
                    HoldPosition(randomDestination.y); // Hover at the destination altitude

                    // Count down our wait timer
                    loiterTimer -= Time.deltaTime;
                    if (loiterTimer <= 0)
                    {
                        // Time's up. Pick a new spot and start flying.
                        PickNewDestination();
                        currentState = AIState.Flying;
                    }
                    break;
            }

            // --- 4. DEBUG LOG ---
            if (Time.frameCount % 60 == 0) // Log once per second
            {
                Debug.Log($"[AI: {gameObject.name}] State: {currentState} | Tgt: {randomDestination.x:F0},{randomDestination.z:F0} | Pedals: {input.Pedals:F2} | Cyclic: {input.Cyclic.y:F2}");
            }
        }

        // This function moves the drone towards a 3D point
        void FlyToTarget(Vector3 targetPosition)
        {
            // Altitude Input
            float altitudeError = targetPosition.y - transform.position.y;
            float throttleInput = Mathf.Clamp(altitudeError * altitudeSensitivity, -1f, 1f);
            
            // Yaw Input
            Vector3 directionToTarget = targetPosition - transform.position;
            directionToTarget.y = 0; 
            float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget.normalized, Vector3.up);
            float yawInput = Mathf.Clamp(angleToTarget * yawSensitivity, -1f, 1f);

            // Proportional Pitch (Forward) Input
            float pitchInput = Mathf.InverseLerp(15f, 0f, Mathf.Abs(angleToTarget)); // Using 15° as the threshold
            
            // Apply all inputs
            input.Throttle = throttleInput;
            input.Cyclic = new Vector2(0, pitchInput);
            
            if (Mathf.Abs(angleToTarget) < yawStopThreshold)
            {
                input.Pedals = 0f; // Stop turning if we're close
            }
            else
            {
                input.Pedals = yawInput;
            }
        }

        // This function just makes the drone hover at a specific altitude
        void HoldPosition(float altitude)
        {
            float altitudeError = altitude - transform.position.y;
            float throttleInput = Mathf.Clamp(altitudeError * altitudeSensitivity, -1f, 1f);
            input.Throttle = throttleInput;

            input.Pedals = 0f;
            input.Cyclic = Vector2.zero;
        }
    }
}