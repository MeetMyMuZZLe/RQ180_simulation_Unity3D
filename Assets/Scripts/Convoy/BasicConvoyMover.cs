// BasicConvoyMover.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(Target))]
public class BasicConvoyMover : MonoBehaviour
{
    [Header("Convoy Role")]
    [Tooltip("Leave this EMPTY for the LEAD vehicle. Assign the tank in front of this one for all FOLLOWERS.")]
    public Transform targetToFollow;

    [Header("Movement")]
    public float speed = 10f;
    public float turnSpeed = 2.0f;
    
    [Header("Ground Snapping")]
    [Tooltip("How high above the ground the tank's pivot point should be (e.g., 0.1).")]
    public float groundOffset = 0.1f; 
    [Tooltip("How far down to check for the ground. Should be a small, positive number like 5 or 10.")]
    public float groundCheckDistance = 10f;
    [Tooltip("CRITICAL: Set this to your 'Terrain' layer, or whatever your ground is.")]
    public LayerMask groundLayer; 

    [Header("Follower Settings")]
    [Tooltip("How far (in meters) to stay behind the target.")]
    public float followDistance = 15f;

    [Header("Leader Settings")]
    [Tooltip("Drag your 'ConvoyPath' parent object here. (Only used by the LEADER).")]
    public Transform convoyPath;
    [Tooltip("How close to get to a waypoint to switch to the next one.")]
    public float waypointThreshold = 5f;

    // --- Private ---
    private Rigidbody rb;
    private Target targetComponent;
    private List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        targetComponent = GetComponent<Target>();
        rb.isKinematic = true;
    }

    void Start()
    {
        // This is the "Snap to Ground" magic for the waypoints
        if (targetToFollow == null && convoyPath != null)
        {
            foreach (Transform child in convoyPath)
            {
                if (Physics.Raycast(child.position + (Vector3.up * 500f), Vector3.down, out RaycastHit hit, 1000f, groundLayer))
                {
                    waypoints.Add(hit.point);
                }
                else
                {
                    waypoints.Add(child.position);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (targetComponent == null || !targetComponent.IsAlive) return;

        // --- 1. Determine Target Position ---
        Vector3 targetPos;
        bool shouldMove = true;

        if (targetToFollow != null)
        {
            // --- Follower Logic ---
            float distance = Vector3.Distance(transform.position, targetToFollow.position);
            if (distance <= followDistance)
            {
                shouldMove = false; // Stop if we're close enough
            }
            targetPos = targetToFollow.position;
        }
        else if (waypoints.Count > 0)
        {
            // --- Leader Logic ---
            targetPos = waypoints[currentWaypointIndex];
            if (Vector3.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetPos.x, targetPos.z)) < waypointThreshold)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
                targetPos = waypoints[currentWaypointIndex];
            }
        }
        else
        {
            // No waypoints and not a follower, just stay put but snap to ground
            shouldMove = false; 
            targetPos = transform.position + transform.forward; // A dummy target in front
        }

        // --- 2. Calculate Desired Rotation (Yaw) ---
        Vector3 directionToTarget = targetPos - transform.position;
        directionToTarget.y = 0;
        
        Quaternion targetYawRotation = transform.rotation; 
        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            targetYawRotation = Quaternion.LookRotation(directionToTarget.normalized);
        }
        
        // --- 3. Interpolate the Yaw (Left/Right) Turn ---
        Quaternion newYaw = Quaternion.Slerp(rb.rotation, targetYawRotation, turnSpeed * Time.fixedDeltaTime);

        // --- 4. Calculate Movement Vector (This is the FIX) ---
        // We get the forward direction from our *newly calculated yaw*
        Vector3 moveDirection = newYaw * Vector3.forward;
        Vector3 moveVelocity = shouldMove ? moveDirection * speed : Vector3.zero;
        Vector3 nextPosition = rb.position + moveVelocity * Time.fixedDeltaTime;

        // --- 5. Ground Snapping and Slope Alignment ---
        Vector3 finalGroundedPos = nextPosition;
        Quaternion finalGroundedRot = newYaw; // Use the new yaw as the base

        // Raycast down from *slightly in front* of the tank's next position
        Vector3 raycastOrigin = nextPosition + (moveDirection * 0.1f) + (Vector3.up * (groundCheckDistance / 2f));
        
        if (Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit groundHit, groundCheckDistance, groundLayer))
        {
            // We found the ground! Snap our position to it.
            finalGroundedPos = groundHit.point + (groundHit.normal * groundOffset);

            // Now, combine the yaw with the slope of the terrain
            finalGroundedRot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(moveDirection, groundHit.normal).normalized, // Project our new forward vector onto the slope
                groundHit.normal // The "up" vector is the ground's normal
            );
        }
        
        // --- 6. Move the Rigidbody ---
        rb.MovePosition(finalGroundedPos);
        rb.MoveRotation(finalGroundedRot);
    }
}