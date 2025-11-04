// AIController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HomingMissile;
using System.Linq;

public class AIController : MonoBehaviour
{
    // --- AI State Machine ---
    private enum AIState
    {
        Climbing,
        Engaging,
        ReturningToHome,
        LoiteringAtHome
    }
    private AIState currentState = AIState.Climbing;
    
    [Header("AI Mission")]
    [Tooltip("The altitude the AI will climb to before starting its patrol.")]
    [SerializeField]
    private float cruiseAltitude = 1500f;
    [Tooltip("How close to the cruise altitude (in meters) is 'good enough'.")]
    [SerializeField]
    private float altitudeHoldThreshold = 100f;
    [Tooltip("Priority list of waypoints (empty Transforms) to fly towards.")]
    [SerializeField]
    private List<Transform> patrolWaypoints;
    [Tooltip("The waypoint (empty Transform) for the home base / runway start.")]
    [SerializeField]
    private Transform homeBaseWaypoint;
    [Tooltip("How close the AI needs to get to a waypoint to switch to the next one.")]
    [SerializeField]
    private float waypointArrivalThreshold = 500f;
    [Tooltip("Radius of the loiter circle at home base.")]
    [SerializeField]
    private float loiterRadius = 1000f;

    // --- References (Core Concepts) ---
    [SerializeField]
    Plane plane;
    [SerializeField]
    float steeringSpeed;
    [SerializeField]
    float minSpeed;
    [SerializeField]
    float maxSpeed;
    [SerializeField]
    float recoverSpeedMin;
    [SerializeField]
    float recoverSpeedMax;
    [SerializeField]
    LayerMask groundCollisionMask;
    [SerializeField]
    float groundCollisionDistance;
    [SerializeField]
    float groundAvoidanceAngle;
    [SerializeField]
    float groundAvoidanceMinSpeed;
    [SerializeField]
    float groundAvoidanceMaxSpeed;
    [SerializeField]
    float pitchUpThreshold;
    [SerializeField]
    float fineSteeringAngle;
    [SerializeField]
    float rollFactor;
    [SerializeField]
    float yawFactor;
    
    [Header("AI Weapon Settings")]
    [SerializeField]
    bool canUseMissiles;
    [SerializeField]
    float missileFiringCooldown;

    // --- REMOVED: missileMinRange, missileMaxRange, missileMaxFireAngle ---
    // The AI will now get this info from Plane.cs

    [Header("AI Reaction Settings")]
    [SerializeField]
    float minMissileDodgeDistance;
    [SerializeField]
    float reactionDelayMin;
    [SerializeField]
    float reactionDelayMax;
    [SerializeField]
    float reactionDelayDistance;

    // --- Private State ---
    Target selfTarget;
    Vector3 lastInput;
    bool isRecoveringSpeed;
    float missileCooldownTimer;
    struct ControlInput { public float time; public Vector3 targetPosition; }
    Queue<ControlInput> inputQueue;
    bool dodging;
    Vector3 lastDodgePoint;
    List<Vector3> dodgeOffsets;
    const float dodgeUpdateInterval = 0.25f;
    float dodgeTimer;
    private AdvancedMissileController advancedMissileController;
    private Target currentEngagementTarget;
    private int currentWaypointIndex = 0;

    void Start()
    {
        selfTarget = plane.GetComponent<Target>();
        advancedMissileController = plane.GetComponent<AdvancedMissileController>();
        dodgeOffsets = new List<Vector3>();
        inputQueue = new Queue<ControlInput>();
    }
    
    Vector3 GetTargetPosition()
    {
        switch (currentState)
        {
            case AIState.Climbing:
                if (plane.Rigidbody.position.y >= cruiseAltitude - altitudeHoldThreshold)
                {
                    currentState = AIState.Engaging;
                    Debug.Log("AI: Reached cruise altitude. Commencing engagement.");
                    goto case AIState.Engaging;
                }
                else
                {
                    if (patrolWaypoints.Count > 0 && patrolWaypoints[0] != null)
                    {
                        Vector3 firstWaypointDir = (patrolWaypoints[0].position - plane.Rigidbody.position).normalized;
                        firstWaypointDir.y = 0;
                        Vector3 climbTargetPos = plane.Rigidbody.position + (firstWaypointDir * 10000f);
                        climbTargetPos.y = cruiseAltitude;
                        return climbTargetPos;
                    }
                    Vector3 straightClimb = plane.transform.position + (plane.transform.forward * 10000f);
                    straightClimb.y = cruiseAltitude;
                    return straightClimb;
                }

            case AIState.Engaging:
                if (currentEngagementTarget != null)
                {
                    Vector3 targetPos = currentEngagementTarget.Position;
                    targetPos.y = Mathf.Max(targetPos.y, cruiseAltitude);
                    return targetPos;
                }
                
                if (patrolWaypoints.Count > 0 && patrolWaypoints[currentWaypointIndex] != null)
                {
                    Vector3 patrolTarget = patrolWaypoints[currentWaypointIndex].position;
                    patrolTarget.y = cruiseAltitude;
                    float distToWaypoint = Vector3.Distance(new Vector2(plane.Rigidbody.position.x, plane.Rigidbody.position.z), new Vector2(patrolTarget.x, patrolTarget.z));
                    if (distToWaypoint < waypointArrivalThreshold)
                    {
                        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
                    }
                    return patrolTarget;
                }
                Vector3 straightPatrol = plane.transform.position + (plane.transform.forward * 10000f);
                straightPatrol.y = cruiseAltitude;
                return straightPatrol;

            case AIState.ReturningToHome:
                if (homeBaseWaypoint == null) return plane.Rigidbody.position + plane.transform.forward * 2000;
                Vector3 homeTarget = homeBaseWaypoint.position;
                if (Vector3.Distance(plane.Rigidbody.position, homeTarget) < waypointArrivalThreshold)
                {
                    currentState = AIState.LoiteringAtHome;
                }
                return homeTarget;

            case AIState.LoiteringAtHome:
                if (homeBaseWaypoint == null) return plane.Rigidbody.position + plane.transform.forward * 2000;
                Vector3 offset = (plane.Rigidbody.position - homeBaseWaypoint.position).normalized * loiterRadius;
                Vector3 loiterPoint = homeBaseWaypoint.position + (Quaternion.Euler(0, 15, 0) * offset);
                return loiterPoint;
        }

        return plane.Rigidbody.position + plane.transform.forward * 1000; // Fallback
    }

    void FindBestTarget()
    {
        var allKnownTargets = plane.TrackedTargetsInfo.Values.Select(info => info.target);
        float closestDist = float.MaxValue;
        Target bestTarget = null;

        foreach (var target in allKnownTargets)
        {
            if (target != null && target.IsAlive)
            {
                float dist = Vector3.Distance(plane.Rigidbody.position, target.Position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = target;
                }
            }
        }

        currentEngagementTarget = bestTarget;

        if (currentEngagementTarget == null && (currentState == AIState.Engaging || currentState == AIState.Climbing))
        {
            currentState = AIState.ReturningToHome;
            Debug.Log("AI: All targets destroyed. Returning to base.");
        }
        else if (currentEngagementTarget != null && currentState == AIState.ReturningToHome)
        {
            currentState = AIState.Engaging;
            Debug.Log("AI: New target acquired. Re-engaging.");
        }
    }

    // --- *** THIS IS THE MODIFIED METHOD *** ---
    void CalculateWeapons(float dt)
    {
        missileCooldownTimer = Mathf.Max(0, missileCooldownTimer - dt);

        if (currentEngagementTarget == null || !canUseMissiles)
        {
            if (plane.BayDoorsOpen) plane.ToggleBayDoors(); // Close doors if no target
            return;
        }

        // --- NEW FIRING LOGIC ---
        // Ask the Plane.cs script if our current target is locked
        bool goodShot = false;
        if (plane.TrackedTargetsInfo.ContainsKey(currentEngagementTarget))
        {
            // 'isLocked' is calculated by Plane.cs using your Lock Settings!
            goodShot = plane.TrackedTargetsInfo[currentEngagementTarget].isLocked;
        }
        // --- END OF NEW LOGIC ---
        
        if (goodShot && missileCooldownTimer == 0)
        {
            // We have a shot AND cooldown is ready
            if (!plane.BayDoorsOpen)
            {
                plane.ToggleBayDoors();
                // We can't fire this frame. Set a short timer to let the doors open.
                missileCooldownTimer = 1.0f; // This is now our "door opening" time
            }
            else
            {
                // Doors are open, fire away!
                advancedMissileController.FireAtTarget(currentEngagementTarget.gameObject);
                missileCooldownTimer = missileFiringCooldown; // Reset for the next missile
                plane.ToggleBayDoors(); // Close doors after firing
            }
        }
        else if (!goodShot && plane.BayDoorsOpen)
        {
            // No shot, and doors are open. Close them.
            plane.ToggleBayDoors();
        }
    }

    // --- This is the corrected steering logic from last time ---
    Vector3 CalculateSteering(float dt, Vector3 targetPosition)
    {
        var error = targetPosition - plane.Rigidbody.position;
        error = Quaternion.Inverse(plane.Rigidbody.rotation) * error;
        
        var errorDir = error.normalized;
        var pitchError = new Vector3(0, error.y, error.z).normalized;
        var yawError = new Vector3(error.x, 0, error.z).normalized; // Horizontal error

        var targetInput = new Vector3();

        // 1. Calculate Pitch
        var pitch = Vector3.SignedAngle(Vector3.forward, pitchError, Vector3.right);
        if (-pitch < pitchUpThreshold) pitch += 360f;
        targetInput.x = Mathf.Clamp(pitch, -1, 1);

        // 2. NEW ROLL/YAW LOGIC (Fixes the tilt)
        var yaw = Vector3.SignedAngle(Vector3.forward, yawError, Vector3.up);
        
        if (Mathf.Abs(yaw) < fineSteeringAngle) 
        {
            targetInput.y = Mathf.Clamp(yaw * yawFactor, -1, 1); // Use yaw
            targetInput.z = 0; // Level the wings
        } 
        else 
        {
            targetInput.y = 0; // Don't use rudder
            targetInput.z = Mathf.Clamp(-yaw * rollFactor, -1, 1); // Bank into the turn
        }
        
        var input = Vector3.MoveTowards(lastInput, targetInput, steeringSpeed * dt);
        lastInput = input;

        return input;
    }

    void SteerToTarget(float dt, Vector3 planePosition)
    {
        bool foundTarget = false;
        Vector3 steering = Vector3.zero;
        Vector3 targetPosition = Vector3.zero;
        var delay = reactionDelayMax;

        if (Vector3.Distance(planePosition, plane.Rigidbody.position) < reactionDelayDistance)
        {
            delay = reactionDelayMin;
        }

        while (inputQueue.Count > 0)
        {
            var input = inputQueue.Peek();
            if (input.time + delay <= Time.time)
            {
                targetPosition = input.targetPosition;
                inputQueue.Dequeue();
                foundTarget = true;
            }
            else
            {
                break;
            }
        }

        if (foundTarget)
        {
            steering = CalculateSteering(dt, targetPosition);
        }
        plane.SetControlInput(steering);
    }
    
    // --- All your other original methods, unchanged ---
    Vector3 AvoidGround() {
        var roll = plane.Rigidbody.rotation.eulerAngles.z;
        if (roll > 180f) roll -= 360f;
        return new Vector3(-1, 0, Mathf.Clamp(-roll * rollFactor, -1, 1));
    }

    Vector3 RecoverSpeed() {
        var roll = plane.Rigidbody.rotation.eulerAngles.z;
        var pitch = plane.Rigidbody.rotation.eulerAngles.x;
        if (roll > 180f) roll -= 360f;
        if (pitch > 180f) pitch -= 360f;
        return new Vector3(Mathf.Clamp(-pitch, -1, 1), 0, Mathf.Clamp(-roll * rollFactor, -1, 1));
    }

    Vector3 GetMissileDodgePosition(float dt, Rigidbody missileRb) {
        dodgeTimer = Mathf.Max(0, dodgeTimer - dt);
        var missilePos = missileRb.position;
        var dist = Mathf.Max(minMissileDodgeDistance, Vector3.Distance(missilePos, plane.Rigidbody.position));
        if (dodgeTimer == 0) {
            var missileForward = missileRb.rotation * Vector3.forward;
            dodgeOffsets.Clear();
            dodgeOffsets.Add(new Vector3(0, dist, 0));
            dodgeOffsets.Add(new Vector3(0, -dist, 0));
            dodgeOffsets.Add(Vector3.Cross(missileForward, Vector3.up) * dist);
            dodgeOffsets.Add(Vector3.Cross(missileForward, Vector3.up) * -dist);
            dodgeTimer = dodgeUpdateInterval;
        }
        float min = float.PositiveInfinity;
        Vector3 minDodge = missilePos + dodgeOffsets[0];
        foreach (var offset in dodgeOffsets) {
            var dodgePosition = missilePos + offset;
            var offsetDist = Vector3.Distance(dodgePosition, lastDodgePoint);
            if (offsetDist < min) {
                minDodge = dodgePosition;
                min = offsetDist;
            }
        }
        lastDodgePoint = minDodge;
        return minDodge;
    }

    float CalculateThrottle(float minSpeed, float maxSpeed) {
        float input = 0;
        if (plane.LocalVelocity.z < minSpeed) {
            input = 1;
        } else if (plane.LocalVelocity.z > maxSpeed) {
            input = -1;
        }
        return input;
    }
    
    void FixedUpdate()
    {
        if (plane.Dead) return;
        var dt = Time.fixedDeltaTime;
        Vector3 steering = Vector3.zero;
        float throttle;
        bool emergency = false;
        Vector3 targetPosition = Vector3.zero;

        var velocityRot = Quaternion.LookRotation(plane.Rigidbody.linearVelocity.normalized);
        var ray = new Ray(plane.Rigidbody.position, velocityRot * Quaternion.Euler(groundAvoidanceAngle, 0, 0) * Vector3.forward);

        if (Physics.Raycast(ray, groundCollisionDistance + plane.LocalVelocity.z, groundCollisionMask.value))
        {
            steering = AvoidGround();
            throttle = CalculateThrottle(groundAvoidanceMinSpeed, groundAvoidanceMaxSpeed);
            emergency = true;
            targetPosition = plane.Rigidbody.position + (plane.transform.forward * 1000f);
        }
        else
        {
            var incomingMissileRb = selfTarget.GetIncomingMissile();
            if (incomingMissileRb != null)
            {
                if (dodging == false) {
                    dodging = true;
                    lastDodgePoint = plane.Rigidbody.position;
                    dodgeTimer = 0;
                }
                targetPosition = GetMissileDodgePosition(dt, incomingMissileRb);
                steering = CalculateSteering(dt, targetPosition);
                throttle = CalculateThrottle(minSpeed, maxSpeed);
                emergency = true;
            }
            else if (plane.LocalVelocity.z < recoverSpeedMin || isRecoveringSpeed)
            {
                dodging = false;
                isRecoveringSpeed = plane.LocalVelocity.z < recoverSpeedMax;
                steering = RecoverSpeed();
                throttle = 1;
                emergency = true;
                targetPosition = plane.Rigidbody.position + (plane.transform.forward * 1000f);
            }
            else
            {
                emergency = false;
                dodging = false;
                isRecoveringSpeed = false;
                
                FindBestTarget();
                targetPosition = GetTargetPosition();
                throttle = CalculateThrottle(minSpeed, maxSpeed);
            }
        }
        
        inputQueue.Enqueue(new ControlInput
        {
            time = Time.time,
            targetPosition = targetPosition,
        });
        
        plane.SetThrottleInput(throttle);

        if (emergency)
        {
            if (isRecoveringSpeed) { steering.x = Mathf.Clamp(steering.x, -0.5f, 0.5f); }
            plane.SetControlInput(steering);
        }
        else
        {
            SteerToTarget(dt, targetPosition);
        }
        
        if (currentState == AIState.Engaging || currentState == AIState.Climbing)
        {
            CalculateWeapons(dt);
        }
    }
}