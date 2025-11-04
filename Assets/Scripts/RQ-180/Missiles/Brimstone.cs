// Brimstone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HomingMissile; // --- MODIFIED: Using your project's namespace

// 1. CHANGED MonoBehaviour to BaseMissile
public class Brimstone : BaseMissile
{
    // --- State Machine for Flight Profile ---
    private enum FlightPhase
    {
        Drop,
        ClearPlatform, // <-- ADD THIS NEW STATE
        Loft,       // Climbing to the attack point above the target
        Transition, // Pitching over toward target while maintaining momentum
        Dive        // High-speed terminal dive onto the target
    }
    private FlightPhase currentPhase;

    [Header("Basic Settings")]
    public int damage = 75;
    public bool fully_active = false;
    public int timebeforeactivition = 20;
    public int timebeforebursting = 40;
    public int timebeforedestruction = 450;
    public int timealive;
    // 2. DELETED target and shooter
    public Rigidbody projectilerb;
    public bool isactive = false;

    [Header("Audio & Effects")]
    public AudioSource launch_sound;
    public AudioSource thrust_sound;
    public GameObject smoke_obj;
    public ParticleSystem smoke;
    public GameObject smoke_position;
    public GameObject destroy_effect;

    [Header("Brimstone Flight Profile")]
    [Tooltip("Direction to move away from the drone (e.g., 1 for right, -1 for left).")]
    [SerializeField] private float lateralClearanceDirection = 1f;
    [Tooltip("How long (in seconds) to spend clearing the drone.")]
    [SerializeField] private float clearanceDuration = 1.5f;
    [Tooltip("Speed during the clearance phase.")]
    [SerializeField] private float clearanceSpeed = 50f;
    // --- MODIFIED: Removed dropPhaseSpeed, it's no longer needed ---
    [SerializeField] private float loftSpeed = 70f;          // Speed during climb
    [SerializeField] private float transitionSpeed = 90f;   // Speed during pitch-over
    [SerializeField] private float diveSpeed = 300f;         // Terminal dive speed (high speed!)
    // [SerializeField] private float diveAcceleration = 600f;  // --- DELETED: Step 2.A ---
    [SerializeField] private float rotateSpeed = 95f;       // Turn rate in degrees/sec
    [SerializeField] private float transitionRotateSpeed = 60f; // Slower rotation during pitch-over for realism
    [SerializeField] private float loftAltitude = 700f;      // How high above target to climb
    [SerializeField] private float loftHorizontalOffset = 250f; // Creates the "cone"
    [SerializeField] private float loftArrivalThreshold = 50f; // Distance to loft point before transitioning
    // [SerializeField] private float speedTransitionRate = 30f;  // --- DELETED: Step 2.A ---
    [SerializeField] private float minTransitionTime = 2f;   // Minimum time to spend in transition phase

    [Header("Explosion Settings")]
    [SerializeField] private float proximityDistance = 0f;
    [SerializeField] private float explosionRadius = 50f;
    public LayerMask collisionMask; // Assign your 'Target' layer here
    private bool hasExploded = false;

    [Header("Advanced Tracking")]
    [SerializeField] private float maxTimePrediction = 1.5f; // Lead prediction time
    [SerializeField] private float minDistancePredict = 10f;
    [SerializeField] private float maxDistancePredict = 150f;

    // --- Private Helper Variables ---
    private Rigidbody targetRb;
    private Vector3 currentGoalPosition; // This will now be set ONCE for the loft
    private float currentSpeed;
    private float targetSpeed; // Already exists
    private float transitionStartTime; // Already exists
    private Vector3 clearanceGoalPosition;
    private float clearanceTimer;
    // --- MODIFIED: Removed dropPhaseFrames ---

    // --- NEW: Step 2.A ---
    private float speedTransitionTimer = 1.0f; // Start as "completed"
    private float speedOnTransitionStart;
    [SerializeField] private float speedTransitionDuration = 2.0f; // Default duration
    // --- END NEW ---


    private void Start()
    {
        projectilerb = this.GetComponent<Rigidbody>();
    }

    // --- Explosion with Splash Damage (from Maverick) ---
    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        isactive = false;
        fully_active = false;

        // --- NEW: Notify target that this missile is gone ---
        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
            {
                targetComponent.NotifyMissileLaunched(this.projectilerb, false);
            }
        }

        // --- NEW: List to track targets damaged by this explosion ---
        List<Target> damagedTargets = new List<Target>();

        // --- Splash Damage Logic ---
        var hits = Physics.OverlapSphere(transform.position, explosionRadius, collisionMask.value);
        foreach (var hit in hits)
        {
            // --- MODIFIED: Changed to 'Target' ---
            Target targetComponent = null; 
            if (hit.attachedRigidbody != null)
            {
                targetComponent = hit.attachedRigidbody.GetComponent<Target>();
            }
            else
            {
                targetComponent = hit.GetComponent<Target>();
            }

            // --- MODIFIED: Check if we already hit this target ---
            if (targetComponent != null && !damagedTargets.Contains(targetComponent))
            {
                targetComponent.ApplyDamage(damage);
                damagedTargets.Add(targetComponent); // Add to list
            }
        }
        // --- End Splash Damage ---

        if (smoke != null)
        {
            smoke.transform.SetParent(null);
            smoke.Stop();
            Destroy(smoke.gameObject, 5f);
        }

        if (projectilerb != null)
        {
            projectilerb.linearVelocity = Vector3.zero;
            projectilerb.angularVelocity = Vector3.zero;
        }
        
        if (thrust_sound != null) thrust_sound.Stop();

        call_destroy_effects();
        Destroy(this.gameObject);
    }
    
    public void call_destroy_effects()
    {
        if (destroy_effect != null)
        {
            Instantiate(destroy_effect, transform.position, transform.rotation);
        }
    }

    public void DestroyMe()
    {
        Explode(); // All end-of-life calls go to Explode
    }

    public void setmissile()
    {
        timealive = 0;
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }
    
    // --- Launch with initial velocity from drone ---
    // 3. ADDED 'override' keyword
    public override void usemissile(Vector3 initialVelocity)
    {
        if (launch_sound != null) launch_sound.Play();

        projectilerb.isKinematic = false;
        projectilerb.useGravity = true; // Gravity is active during the Drop phase
        projectilerb.linearVelocity = initialVelocity; // Inherit drone's velocity

        isactive = true;
        setmissile();

        // --- NEW: Notify target that this missile is incoming ---
        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
            {
                targetComponent.NotifyMissileLaunched(this.projectilerb, true);
            }
        }

        currentPhase = FlightPhase.Drop;
        // --- MODIFIED: Speed is set by physics, not a variable ---
        currentSpeed = initialVelocity.magnitude; 
        targetSpeed = currentSpeed;
    }

    // --- MODIFIED: Collision Detection (from Maverick) ---
    private void OnTriggerEnter(Collider other)
    {
        if (!isactive || hasExploded) return;

        // If we hit the shooter *before* the missile is armed, ignore it.
        Rigidbody hitRb = other.attachedRigidbody;
        if (hitRb != null && hitRb.gameObject == shooter && !fully_active)
        {
            return;
        }

        // For any other hit (ground, scenery, armed-hit-on-shooter, or any-hit-on-target),
        // trigger the explosion and splash damage.
        Explode();
    }

    // --- Main Update Loop with State Machine ---
    void FixedUpdate()
    {
        if (!isactive || hasExploded) return;

        // Check for lost target
        if (target == null || !target.activeInHierarchy)
        {
            Explode();
            return;
        }

        timealive++;

        // Lifetime check
        if (timealive >= timebeforedestruction)
        {
            Explode();
            return;
        }
        
        // Arming timer
        if (timealive == timebeforeactivition)
        {
            fully_active = true;
            if (thrust_sound != null) thrust_sound.Play();
        }

        // Proximity fuze
        if (proximityDistance > 0 && currentPhase == FlightPhase.Dive && fully_active)
        {
            if (Vector3.Distance(transform.position, target.transform.position) < proximityDistance)
            {
                Explode();
                return;
            }
        }

        // --- MODIFIED: Step 2.C: Replaced linear ramp with S-Curve ---
        // --- DELETED Mathf.MoveTowards block ---
        /*
        if (currentPhase != FlightPhase.Drop)
        {
            // ... (old MoveTowards logic) ...
        }
        */
        
        // --- NEW: S-Curve Speed Logic ---
        if (speedTransitionTimer < 1.0f)
        {
            speedTransitionTimer += Time.fixedDeltaTime / speedTransitionDuration;
            float t = Mathf.SmoothStep(0.0f, 1.0f, speedTransitionTimer);
            currentSpeed = Mathf.Lerp(speedOnTransitionStart, targetSpeed, t);
        }
        // --- END NEW ---


        // State Machine Logic
        switch (currentPhase)
        {
            case FlightPhase.Drop:
                HandleDropPhase();
                break;
            
            // --- ADD THIS NEW CASE ---
            case FlightPhase.ClearPlatform:
                HandleClearPlatformPhase();
                break;
            // --- END OF NEW CASE ---
            
            case FlightPhase.Loft:
                HandleLoftPhase();
                break;
            
            case FlightPhase.Transition:
                HandleTransitionPhase();
                break;
            
            case FlightPhase.Dive:
                HandleDivePhase();
                break;
        }
    }
    
    // --- Calculates the fixed loft goal point ---
    private void CalculateLoftGoalPosition()
    {
        if (target == null) return;

        Vector3 targetPosition = target.transform.position;
        Vector3 missilePosition = transform.position;

        Vector3 dirToMissile = missilePosition - targetPosition;
        Vector3 horizontalDirToMissile = new Vector3(dirToMissile.x, 0, dirToMissile.z);

        if (horizontalDirToMissile.sqrMagnitude < 0.1f)
        {
            if (shooter != null)
            {
                horizontalDirToMissile = new Vector3(shooter.transform.forward.x, 0, shooter.transform.forward.z).normalized;
            }
            else
            {
                horizontalDirToMissile = Vector3.forward;
            }
        }
        else
        {
            horizontalDirToMissile.Normalize();
        }

        Vector3 horizontalOffset = horizontalDirToMissile * loftHorizontalOffset;
        currentGoalPosition = targetPosition + (Vector3.up * loftAltitude) + horizontalOffset;
        
        Debug.Log($"Brimstone: New loft goal calculated: {currentGoalPosition}");
    }


    // --- MODIFIED: DROP PHASE ---
    // Now just waits for the timer, letting physics (gravity + initial velocity) control the drop.
    private void HandleDropPhase()
    {
        // Wait for motor ignition timer
        if (timealive >= timebeforebursting)
        {
            // Ignite engine
            projectilerb.useGravity = false;
            
            // --- MODIFIED TRANSITION ---
            // SetTargetSpeed(loftSpeed, 3.0f); // <-- OLD
            SetTargetSpeed(clearanceSpeed, 1.0f); // <-- NEW: Ramp to clearance speed
            currentPhase = FlightPhase.ClearPlatform; // <-- NEW: Go to clearance phase
            
            // Calculate the lateral clearance goal
            // We use the 'shooter' (the drone) to find its "right" direction
            Vector3 lateralDir = shooter != null ? shooter.transform.right : transform.right;
            Vector3 offset = lateralDir * lateralClearanceDirection * clearanceSpeed * clearanceDuration;
            
            clearanceGoalPosition = transform.position + offset;
            clearanceTimer = 0f;

            Debug.Log($"Brimstone: Drop complete. Transitioning to ClearPlatform.");

            // Spawn smoke effect
            if (smoke_obj != null && smoke_position != null)
            {
                GameObject smokeInstance = Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation);
                smokeInstance.transform.SetParent(this.transform);
                smoke = smokeInstance.GetComponent<ParticleSystem>();
                if (smoke != null) smoke.Play();
            }
        }
    }

    // --- NEW METHOD ---
    private void HandleClearPlatformPhase()
    {
        // Keep flying towards the lateral goal
        RotateTowardsGoal(clearanceGoalPosition, rotateSpeed);
        MoveForward(); // MoveForward uses currentSpeed, which is ramping

        clearanceTimer += Time.fixedDeltaTime;

        // Check if we've been in this phase long enough
        if (clearanceTimer >= clearanceDuration)
        {
            // --- Transition to Loft ---
            SetTargetSpeed(loftSpeed, 3.0f); // Now ramp up to loft speed
            currentPhase = FlightPhase.Loft;

            // Calculate the original loft "cone rim" point
            CalculateLoftGoalPosition();

            Debug.Log($"Brimstone: Clearance complete. Transitioning to Loft.");
        }
    }

    // --- LOFT PHASE: Climb to attack altitude above target ---
    private void HandleLoftPhase()
    {
        RotateTowardsGoal(currentGoalPosition, rotateSpeed);
        MoveForward();

        float distanceToLoftPoint = Vector3.Distance(transform.position, currentGoalPosition);
        if (distanceToLoftPoint < loftArrivalThreshold)
        {
            // --- MODIFIED: Step 2.D ---
            // targetSpeed = transitionSpeed; // --- OLD
            SetTargetSpeed(transitionSpeed, 1.5f); // 1.5-second ramp
            // --- END MODIFIED ---
            
            currentPhase = FlightPhase.Transition;
            transitionStartTime = Time.time;
            
            Debug.Log("Brimstone: Reached loft point. Beginning pitch-over transition.");
        }
    }

    // --- TRANSITION PHASE: Pitch over toward target ---
    private void HandleTransitionPhase()
    {
        float timeInTransition = Time.time - transitionStartTime;
        
        Vector3 predictedTarget = GetPredictedTargetPosition();
        currentGoalPosition = predictedTarget;

        RotateTowardsGoal(currentGoalPosition, transitionRotateSpeed);
        MoveForward();

        Vector3 directionToTarget = (currentGoalPosition - transform.position).normalized;
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

        if (timeInTransition >= minTransitionTime && angleToTarget < 25f)
        {
            // --- MODIFIED: Step 2.D ---
            // targetSpeed = diveSpeed; // --- OLD
            SetTargetSpeed(diveSpeed, 2.0f); // 2-second ramp to dive speed
            // --- END MODIFIED ---
            
            currentPhase = FlightPhase.Dive;
            
            Debug.Log($"BS: Trans complete {timeInTransition:F2}s. Ramping to {diveSpeed} m/s.");
        }
    }

    // --- DIVE PHASE: High-speed terminal attack ---
    private void HandleDivePhase()
    {
        Vector3 predictedTarget = GetPredictedTargetPosition();
        currentGoalPosition = predictedTarget;

        RotateTowardsGoal(currentGoalPosition, rotateSpeed);
        MoveForward();
    }

    // --- Calculate predicted target position with lead ---
    private Vector3 GetPredictedTargetPosition()
    {
        if (targetRb == null)
        {
            return target.transform.position;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        float leadTimePercentage = Mathf.InverseLerp(minDistancePredict, maxDistancePredict, distanceToTarget);
        float predictionTime = Mathf.Lerp(0, maxTimePrediction, leadTimePercentage);

        return targetRb.position + (targetRb.linearVelocity * predictionTime);
    }

    // --- Smooth rotation toward goal ---
    private void RotateTowardsGoal(Vector3 goalPosition, float rotationSpeed)
    {
        Vector3 heading = goalPosition - transform.position;

        if (heading != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(heading);
            
            projectilerb.MoveRotation(Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            ));
        }
    }

    // --- Apply forward velocity ---
    private void MoveForward()
    {
        projectilerb.linearVelocity = transform.forward * currentSpeed;
    }

    // --- NEW: Step 2.B ---
    private void SetTargetSpeed(float newSpeed, float duration)
    {
        // Don't restart if we are already transitioning to this speed
        if (Mathf.Approximately(targetSpeed, newSpeed)) return; 
        
        targetSpeed = newSpeed;
        speedOnTransitionStart = currentSpeed;
        speedTransitionDuration = duration;
        speedTransitionTimer = 0.0f; // Start the transition!
    }
    // --- END NEW ---
}