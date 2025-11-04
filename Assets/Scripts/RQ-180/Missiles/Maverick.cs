// Maverick.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// The "using HomingMissile;" namespace is no longer needed here.

// 1. REMOVED the "namespace HomingMissile" wrapper
// 2. CHANGED MonoBehaviour to BaseMissile
public class Maverick : BaseMissile
{
    [Header("Basic Settings")]
    // --- MODIFIED: Default values from your inspector screenshot ---
    public int speed = 400;
    public int downspeed = 30; // This variable wasn't used in the original script, but is included.
    public int damage = 100;
    public bool fully_active = false;
    public int timebeforeactivition = 20;
    public int timebeforebursting = 40;
    public int timebeforedestruction = 600;
    public int timealive;
    // 3. DELETED target and shooter (they're in BaseMissile)
    public Rigidbody projectilerb;
    public bool isactive = false;
    public Vector3 sleepposition;

    [Header("Audio & Effects")]
    public AudioSource launch_sound;
    public AudioSource thrust_sound;
    public GameObject smoke_obj;
    public ParticleSystem smoke;
    public GameObject smoke_position;
    public GameObject destroy_effect;

    [Header("Tracking")]
    // --- MODIFIED: Default values from your inspector screenshot ---
    [SerializeField] private float rotateSpeed = 150f;
    [SerializeField] private float maxDistancePredict = 100f;
    [SerializeField] private float minDistancePredict = 5f;
    [SerializeField] private float maxTimePrediction = 5f;
    private Vector3 standardPrediction, deviatedPrediction;

    [Header("Deviation")]
    // --- MODIFIED: Default values from your inspector screenshot ---
    [SerializeField] private float deviationAmount = 0f;
    [SerializeField] private float deviationSpeed = 2f;

    // --- NEW: Fields for Proximity and Splash Damage ---
    [Header("Proximity & Splash")]
    [Tooltip("How close the missile needs to be to the target to explode without a direct hit.")]
    [SerializeField] private float proximityFuseDistance = 5f;
    [Tooltip("The radius of the explosion's splash damage.")]
    [SerializeField] private float splashRadius = 15f;
    [Tooltip("Set this to the layer(s) that your targets are on (e.g., 'Targets').")]
    [SerializeField] private LayerMask splashDamageLayer;

    private Rigidbody targetRb;
    private bool isExploding = false; // --- NEW: Flag to prevent multiple explosions

    private void Start()
    {
        projectilerb = this.GetComponent<Rigidbody>();
    }

    public void call_destroy_effects()
    {
        if (destroy_effect != null)
        {
            Instantiate(destroy_effect, transform.position, transform.rotation);
        }
    }

    public void setmissile()
    {
        timealive = 0;
        transform.Rotate(0, 0, 0);
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    // --- NEW: Centralized explosion logic for splash damage ---
    private void Explode()
    {
        if (isExploding) return; // Ensure this only runs once
        isExploding = true;
        isactive = false; // Stop all updates
        fully_active = false;

        // --- Notify target that this missile is gone ---
        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
            {
                targetComponent.NotifyMissileLaunched(this.projectilerb, false);
            }
        }
        
        // --- Splash Damage Logic ---
        List<Target> damagedTargets = new List<Target>();
        if (splashDamageLayer.value != 0)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, splashRadius, splashDamageLayer);
            foreach (var col in colliders)
            {
                Target targetComponent = col.attachedRigidbody != null ? col.attachedRigidbody.GetComponent<Target>() : null;
                if (targetComponent != null && !damagedTargets.Contains(targetComponent))
                {
                    targetComponent.ApplyDamage(damage);
                    damagedTargets.Add(targetComponent);
                }
            }
        }
        
        // --- Cleanup and Effects (Moved from DestroyMe) ---
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

        if (thrust_sound != null)
        {
            thrust_sound.Stop();
        }

        call_destroy_effects(); // This will now be called correctly
        Destroy(this.gameObject);
    }

    public void DestroyMe()
    {
        // This method is now just a wrapper that calls the main Explode logic
        Explode(); 
    }

    // 4. ADDED 'override' keyword
    public override void usemissile(Vector3 initialVelocity)
    {
        if (launch_sound != null)
        {
            launch_sound.Play();
        }

        if (projectilerb != null)
        {
            projectilerb.isKinematic = false;
            projectilerb.useGravity = true;
            projectilerb.linearVelocity = initialVelocity; // Retains drone's velocity
        }

        isactive = true;
        setmissile();

        if (target != null)
        {
            Target targetComponent = target.GetComponent<Target>();
            if (targetComponent != null)
            {
                targetComponent.NotifyMissileLaunched(this.projectilerb, true);
            }
        }
    }

    // --- MODIFIED: OnTriggerEnter now calls Explode() ---
    private void OnTriggerEnter(Collider other)
    {
        if (!isactive || isExploding) return;

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

    void FixedUpdate()
    {
        if (!isactive || isExploding) return; // --- MODIFIED: Added isExploding check

        if (target == null || !target.activeInHierarchy)
        {
            DestroyMe(); // Target was destroyed or deactivated
            return;
        }

        timealive++;

        if (timealive == timebeforeactivition)
        {
            fully_active = true;
            if (thrust_sound != null) thrust_sound.Play();
        }

        // Wait until the missile's engine bursts
        if (timealive < timebeforebursting)
        {
            return;
        }

        if (timealive == timebeforebursting)
        {
            if (projectilerb != null)
            {
                projectilerb.useGravity = false;
            }

            if (smoke_obj != null && smoke_position != null)
            {
                GameObject smokeInstance = Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation);
                smokeInstance.transform.SetParent(this.transform);
                smoke = smokeInstance.GetComponent<ParticleSystem>();
                if (smoke != null) smoke.Play();
            }
        }

        if (timealive >= timebeforedestruction)
        {
            DestroyMe(); // Reached end of life
            return;
        }

        // --- NEW: Proximity Fuse Check ---
        // This check runs every physics frame after the missile is armed and bursting.
        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        if (distanceToTarget <= proximityFuseDistance)
        {
            Explode();
            return; // Stop processing, we've hit
        }

        // --- Original Homing Logic ---
        float leadTimePercentage = Mathf.InverseLerp(minDistancePredict, maxDistancePredict, distanceToTarget);

        PredictMovement(leadTimePercentage);
        AddDeviation(leadTimePercentage);
        RotateTowardsTarget();

        projectilerb.linearVelocity = transform.forward * speed;
    }

    private void PredictMovement(float leadTimePercentage)
    {
        float predictionTime = Mathf.Lerp(0, maxTimePrediction, leadTimePercentage);
        if (targetRb != null)
        {
            standardPrediction = targetRb.position + targetRb.linearVelocity * predictionTime;
        }
        else
        {
            standardPrediction = target.transform.position;
        }
    }

    private void AddDeviation(float leadTimePercentage)
    {
        Vector3 deviation = new Vector3(Mathf.Cos(Time.time * deviationSpeed), 0, 0);
        Vector3 predictionOffset = transform.TransformDirection(deviation) * deviationAmount * leadTimePercentage;
        deviatedPrediction = standardPrediction + predictionOffset;
    }

    private void RotateTowardsTarget()
    {
        Vector3 heading = deviatedPrediction - transform.position;
        if (heading != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(heading);
            projectilerb.MoveRotation(Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            ));
        }
    }
}