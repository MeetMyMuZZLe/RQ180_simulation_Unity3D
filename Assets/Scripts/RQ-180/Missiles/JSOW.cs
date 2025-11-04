// JSOW.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HomingMissile; // Using your project's namespace

// 1. CHANGED MonoBehaviour to BaseMissile
public class JSOW : BaseMissile
{
    // --- State Machine for Flight Profile ---
    private enum FlightPhase
    {
        Drop,       
        Glide,      
        LevelOff,   
        Cruise,     
        InitiateDive, 
        Dive        
    }
    private FlightPhase currentPhase;

    [Header("Basic Settings")]
    public int damage = 100;
    public bool fully_active = false;
    public int timebeforeactivition = 20;
    public int timebeforebursting = 60;
    public int timebeforedestruction = 15000;
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

    [Header("Explosion Settings")]
    [SerializeField] private float proximityDistance = 0f;
    [SerializeField] private float explosionRadius = 20f;
    public LayerMask collisionMask; 
    private bool hasExploded = false;

    [Header("Wing Deployment")]
    [Tooltip("Time in frames before wings begin to deploy")]
    public int timebeforewingdeployment = 40;
    public Transform leftWing;
    public Transform rightWing;
    public float leftWingDeployAngle = 90f;
    public float rightWingDeployAngle = -90f;
    public float wingDeploySpeed = 5f;
    
    private float leftWingCurrentAngle = 0f;
    private float rightWingCurrentAngle = 0f;
    private bool wingsDeployed = false;

    [Header("JSOW Flight Profile")]
    [SerializeField] private float glideSpeed = 100f;
    [SerializeField] private float cruiseSpeed = 100f;
    [SerializeField] private float diveSpeed = 200f;
    // [SerializeField] private float diveAcceleration = 100f; // --- DELETED: Step 2.A ---
    [SerializeField] private float cruiseActivationAltitude = 200f;
    [SerializeField] private float diveDistance = 750f;
    [SerializeField] private float glidePitchAngle = 45f;
    [SerializeField] private float altitudeTransitionRange = 75f;
    
    [Header("Autopilot")]
    [SerializeField] private float rotateSpeed = 60f;
    [SerializeField] private float diveRotateSpeed = 25f;
    // [SerializeField] private float speedTransitionRate = 30f; // --- DELETED: Step 2.A ---
    [SerializeField] private float maxBankAngle = 80f;
    [SerializeField] private float levelOffRotateSpeed = 20f;
    [SerializeField] private float initiateDiveRotateSpeed = 10f;
    [SerializeField] private float levelOffAngleThreshold = 2f;
    [SerializeField] private float diveAngleThreshold = 3f;

    [Header("Advanced Tracking")]
    [SerializeField] private float maxTimePrediction = 1.5f;
    [SerializeField] private float minDistancePredict = 10f;
    [SerializeField] private float maxDistancePredict = 150f;

    // --- Private Helper Variables ---
    private Rigidbody targetRb;
    private Vector3 currentGoalPosition;
    private float currentSpeed;
    private float targetSpeed;

    // --- NEW: Step 2.A ---
    private float speedTransitionTimer = 1.0f; // Start as "completed"
    private float speedOnTransitionStart;
    private float speedTransitionDuration = 2.0f; // Default duration
    // --- END NEW ---

    private void Start()
    {
        projectilerb = this.GetComponent<Rigidbody>();
        
        if (leftWing != null)
            leftWing.localRotation = Quaternion.Euler(0f, leftWingCurrentAngle, 0f);
        if (rightWing != null)
            rightWing.localRotation = Quaternion.Euler(0f, rightWingCurrentAngle, 0f);
        
        currentGoalPosition = transform.position + transform.forward * 2000f;
    }

    // --- Standardized Explosion ---
    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        isactive = false;
        fully_active = false;

        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
                targetComponent.NotifyMissileLaunched(this.projectilerb, false);
        }

        List<Target> damagedTargets = new List<Target>();
        var hits = Physics.OverlapSphere(transform.position, explosionRadius, collisionMask.value);
        foreach (var hit in hits)
        {
            Target targetComponent = hit.attachedRigidbody != null ? 
                hit.attachedRigidbody.GetComponent<Target>() : 
                hit.GetComponent<Target>();

            if (targetComponent != null && !damagedTargets.Contains(targetComponent))
            {
                targetComponent.ApplyDamage(damage);
                damagedTargets.Add(targetComponent);
            }
        }
        
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
            Instantiate(destroy_effect, transform.position, transform.rotation);
    }

    public void DestroyMe()
    {
        Explode();
    }

    public void setmissile()
    {
        timealive = 0;
        if (target != null)
            targetRb = target.GetComponent<Rigidbody>();
    }
    
    // --- Standardized Launch ---
    // 3. ADDED 'override' keyword
    public override void usemissile(Vector3 initialVelocity)
    {
        if (launch_sound != null) launch_sound.Play();

        projectilerb.isKinematic = false;
        projectilerb.useGravity = true;
        projectilerb.linearVelocity = initialVelocity;

        isactive = true;
        setmissile();

        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
                targetComponent.NotifyMissileLaunched(this.projectilerb, true);
        }

        currentPhase = FlightPhase.Drop;
        currentSpeed = initialVelocity.magnitude; 
        targetSpeed = currentSpeed;
    }

    // --- Standardized Collision ---
    private void OnTriggerEnter(Collider other)
    {
        if (!isactive || hasExploded) return;

        Rigidbody hitRb = other.attachedRigidbody;
        if (hitRb != null && hitRb.gameObject == shooter && !fully_active)
            return;

        Explode();
    }

    // --- Main Update Loop ---
    void FixedUpdate()
    {
        if (!isactive || hasExploded) return;

        if (target == null || !target.activeInHierarchy)
        {
            Explode();
            return;
        }

        timealive++;

        if (timealive >= timebeforedestruction)
        {
            Explode();
            return;
        }
        
        if (timealive == timebeforeactivition)
        {
            fully_active = true;
            if (thrust_sound != null) thrust_sound.Play();
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        if (proximityDistance > 0 && currentPhase == FlightPhase.Dive && fully_active)
        {
            if (distanceToTarget < proximityDistance)
            {
                Explode();
                return;
            }
        }

        // --- Wing deployment logic moved ---
        // Start/continue animation as long as time is met
        if (timealive >= timebeforewingdeployment)
        {
            AnimateWingDeployment(); 
        }
        
        // --- NEW: Step 2.C: S-Curve Speed Logic (replaces UpdateSpeed()) ---
        if (speedTransitionTimer < 1.0f)
        {
            speedTransitionTimer += Time.fixedDeltaTime / speedTransitionDuration;
            float t = Mathf.SmoothStep(0.0f, 1.0f, speedTransitionTimer);
            currentSpeed = Mathf.Lerp(speedOnTransitionStart, targetSpeed, t);
        }
        // --- END NEW ---
        
        if (currentPhase != FlightPhase.Drop)
        {
            // UpdateSpeed(); // --- DELETED: Step 2.C ---
            UpdateGuidance(distanceToTarget);
        }
        else
        {
            // We are in the Drop Phase
            HandleDropPhase(); // This checks for timebeforebursting to switch phase
        }
    }

    // --- Main State Machine Logic ---
    private void UpdateGuidance(float distanceToTarget)
    {
        // --- NEW: Calculate horizontal distance by ignoring the Y-axis ---
        Vector3 offsetToTarget = target.transform.position - transform.position;
        offsetToTarget.y = 0; // Flatten the vector to be horizontal
        float horizontalDistanceToTarget = offsetToTarget.magnitude;
        // --- END NEW ---

        float currentAltitude = transform.position.y;
        float altitudeError = currentAltitude - cruiseActivationAltitude;
        float currentRotateSpeed = rotateSpeed;

        Vector3 horizontalForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        if (horizontalForward.sqrMagnitude < 0.01f) horizontalForward = Vector3.forward; // Safety check
        float currentPitchAngle = Vector3.Angle(transform.forward, horizontalForward) * -Mathf.Sign(transform.forward.y);


        // --- State machine logic ---
        switch (currentPhase)
        {
            case FlightPhase.Glide:
                if (!wingsDeployed)
                {
                    SetTargetSpeed(glideSpeed, 3.0f);
                    currentRotateSpeed = 0f;
                    Quaternion straightGlide = Quaternion.Euler(glidePitchAngle, 0, 0);
                    currentGoalPosition = transform.position + (transform.rotation * straightGlide * Vector3.forward) * 2000f;
                }
                else
                {
                    SetTargetSpeed(glideSpeed, 1.0f);
                    currentRotateSpeed = rotateSpeed;

                    Vector3 horizontalDir = target.transform.position - transform.position;
                    horizontalDir.y = 0;
                    Quaternion targetYaw = Quaternion.LookRotation(horizontalDir.normalized);
                    Quaternion finalRotation = targetYaw * Quaternion.Euler(glidePitchAngle, 0, 0);
                    currentGoalPosition = transform.position + (finalRotation * Vector3.forward) * 2000f;
                }

                if (altitudeError <= altitudeTransitionRange)
                {
                    currentPhase = FlightPhase.LevelOff;
                    Debug.Log($"JSOW: Nearing cruise alt at {currentAltitude:F1}m. Beginning smooth transition to {cruiseActivationAltitude}m.");
                }
                break;

            case FlightPhase.LevelOff:
                SetTargetSpeed(glideSpeed, 1.0f);
                currentRotateSpeed = levelOffRotateSpeed;

                currentGoalPosition = target.transform.position;
                currentGoalPosition.y = cruiseActivationAltitude;

                if (Mathf.Abs(currentPitchAngle) < levelOffAngleThreshold)
                {
                    currentPhase = FlightPhase.Cruise;
                    SetTargetSpeed(cruiseSpeed, 2.0f);
                    Debug.Log($"JSOW: LevelOff complete at {currentAltitude:F1}m. Entering Cruise phase.");
                }
                break;

            case FlightPhase.Cruise:
                SetTargetSpeed(cruiseSpeed, 1.0f);
                currentRotateSpeed = rotateSpeed;

                currentGoalPosition = target.transform.position;
                currentGoalPosition.y = cruiseActivationAltitude;

                // --- MODIFIED: Now uses horizontalDistanceToTarget instead of distanceToTarget ---
                if (horizontalDistanceToTarget <= diveDistance)
                {
                    currentPhase = FlightPhase.InitiateDive;
                    Debug.Log($"JSOW: Dive distance reached at {currentAltitude:F1}m. Initiating dive.");
                }
                break;

            case FlightPhase.InitiateDive:
                SetTargetSpeed(cruiseSpeed, 0.5f);
                currentRotateSpeed = initiateDiveRotateSpeed;

                currentGoalPosition = GetPredictedTargetPosition();

                if (currentPitchAngle < -diveAngleThreshold)
                {
                    currentPhase = FlightPhase.Dive;
                    SetTargetSpeed(diveSpeed, 2.5f);
                    Debug.Log("JSOW: InitiateDive complete. Terminal dive.");
                }
                break;

            case FlightPhase.Dive:
                SetTargetSpeed(diveSpeed, 1.0f);
                currentRotateSpeed = diveRotateSpeed;
                currentGoalPosition = GetPredictedTargetPosition();
                break;
        }

        // --- Autopilot: Steer and Move ---
        Vector3 heading = currentGoalPosition - transform.position;
        if (heading.sqrMagnitude > 0.01f && currentRotateSpeed > 0f)
        {
            RotateTowardsGoal(heading, currentRotateSpeed);
        }
        MoveForward();
    }

    // --- DROP PHASE ---
    private void HandleDropPhase()
    {
        // This function now *only* handles the transition from Drop to Glide
        if (timealive >= timebeforebursting)
        {
            projectilerb.useGravity = false;
            
            // --- MODIFIED: Step 2.D ---
            // targetSpeed = glideSpeed; // --- OLD
            // currentSpeed = projectilerb.linearVelocity.magnitude; // --- OLD
            SetTargetSpeed(glideSpeed, 3.0f); // 3-second ramp to glide speed
            // --- END MODIFIED ---
            
            currentPhase = FlightPhase.Glide;
            
            float currentAltitude = transform.position.y;
            Debug.Log($"JSOW: Drop complete at {currentAltitude:F1}m. Igniting motor, entering Glide phase.");

            if (smoke_obj != null && smoke_position != null)
            {
                GameObject smokeInstance = Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation);
                smokeInstance.transform.SetParent(this.transform);
                smoke = smokeInstance.GetComponent<ParticleSystem>();
                if (smoke != null) smoke.Play();
            }
        }
    }

    // --- DELETED: UpdateSpeed() method (Step 2.C) ---
    /*
    private void UpdateSpeed()
    {
        // ... (old MoveTowards logic) ...
    }
    */

    // --- Calculate predicted target position ---
    private Vector3 GetPredictedTargetPosition()
    {
        if (targetRb == null)
            return target.transform.position;

        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        float leadTimePercentage = Mathf.InverseLerp(minDistancePredict, maxDistancePredict, distanceToTarget);
        float predictionTime = Mathf.Lerp(0, maxTimePrediction, leadTimePercentage);

        return targetRb.position + (targetRb.linearVelocity * predictionTime);
    }

    // --- Bank-to-Turn rotation logic ---
    private void RotateTowardsGoal(Vector3 heading, float rotationSpeed)
    {
        Quaternion lookRotation = Quaternion.LookRotation(heading);
        Vector3 localHeading = transform.InverseTransformDirection(heading.normalized);
        float horizontalError = localHeading.x; 
        float targetBankAngle = Mathf.Clamp(-horizontalError * 90f, -maxBankAngle, maxBankAngle);
        Quaternion finalTargetRotation = lookRotation * Quaternion.Euler(0, 0, targetBankAngle);
        
        projectilerb.MoveRotation(Quaternion.RotateTowards(
            transform.rotation,
            finalTargetRotation,
            rotationSpeed * Time.fixedDeltaTime
        ));
    }

    // --- Apply forward velocity ---
    private void MoveForward()
    {
        projectilerb.linearVelocity = transform.forward * currentSpeed;
    }

    // --- Wing animation logic (unchanged) ---
    private void AnimateWingDeployment()
    {
        if (wingsDeployed) return;
        bool leftComplete = false;
        bool rightComplete = false;

        if (leftWing != null)
        {
            leftWingCurrentAngle = Mathf.MoveTowards(
                leftWingCurrentAngle,
                leftWingDeployAngle,
                wingDeploySpeed * Time.fixedDeltaTime * 60f
            );
            leftWing.localRotation = Quaternion.Euler(0f, leftWingCurrentAngle, 0f);
            leftComplete = Mathf.Approximately(leftWingCurrentAngle, leftWingDeployAngle);
        }
        else { leftComplete = true; }

        if (rightWing != null)
        {
            rightWingCurrentAngle = Mathf.MoveTowards(
                rightWingCurrentAngle,
                rightWingDeployAngle,
                wingDeploySpeed * Time.fixedDeltaTime * 60f
            );
            rightWing.localRotation = Quaternion.Euler(0f, rightWingCurrentAngle, 0f);
            rightComplete = Mathf.Approximately(rightWingCurrentAngle, rightWingDeployAngle);
        }
        else { rightComplete = true; }

        if (leftComplete && rightComplete)
        {
            wingsDeployed = true;
        }
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