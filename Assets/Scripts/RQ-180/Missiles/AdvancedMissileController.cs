// AdvancedMissileController.cs
using System.Collections.Generic;
using UnityEngine;
using HomingMissile;
using System.Linq;

[System.Serializable]
public class MissilePod
{
    public string podName;
    public MissileType missileType;
    public List<BaseMissile> missilesInPod; // <-- MODIFIED
    public List<TargetClassDefinition> validTargetClasses;

    // [HideInInspector]
    public int currentMissileIndex = 0;
}
public class AdvancedMissileController : MonoBehaviour
{
    // --- ADD THIS LINE ---
    [Header("Camera")]
    [SerializeField]
    private GameObject missileCameraPrefab; // Drag your MissileCamPrefab here
    // --- END OF ADD ---

    public List<MissilePod> missilePods;
    private Plane plane;

    void Start()
    {
        plane = GetComponent<Plane>();
    }

    // --- RIPPLE FIRE LOGIC UPDATED ---
    public void FireMissileFromPod(int podIndex)
    {
        if (plane == null || !plane.BayDoorsOpen) return;
        if (podIndex < 0 || podIndex >= missilePods.Count) return;

        MissilePod pod = missilePods[podIndex];
        
        // 1. Get all valid locked targets (same as before)
        List<Target> lockedTargets = plane.GetLockedTargets();
        List<Target> validLockedTargets = lockedTargets
            .Where(t => t.targetClass != null && pod.validTargetClasses.Contains(t.targetClass))
            .ToList();

        if (validLockedTargets.Count == 0)
        {
            Debug.Log($"Fire command for '{pod.podName}' received, but no valid targets are locked.");
            return;
        }

        // --- NEW LOGIC: Sort targets to prevent cross-pathing ---

        // 2. Separate targets into "Left" and "Right" lists
        List<Target> leftTargets = new List<Target>();
        List<Target> rightTargets = new List<Target>();

        foreach (var target in validLockedTargets)
        {
            // Get target's position relative to the plane.
            // localPos.x < 0 is Left, localPos.x > 0 is Right.
            Vector3 localPos = plane.transform.InverseTransformPoint(target.Position);
            if (localPos.x < 0)
            {
                leftTargets.Add(target);
            }
            else
            {
                rightTargets.Add(target);
            }
        }

        // 3. Sort each list so the *outermost* targets are first
        // Sorts left list from most-left (-100) to least-left (-10)
        leftTargets.Sort((a, b) => 
            plane.transform.InverseTransformPoint(a.Position).x.CompareTo(
            plane.transform.InverseTransformPoint(b.Position).x)
        );
        // Sorts right list from most-right (100) to least-right (10)
        rightTargets.Sort((a, b) => 
            plane.transform.InverseTransformPoint(b.Position).x.CompareTo(
            plane.transform.InverseTransformPoint(a.Position).x)
        );

        // 4. Create the final, sorted list based on your L/R/L/R missile order
        List<Target> sortedTargets = new List<Target>();
        int leftIdx = 0;
        int rightIdx = 0;

        // Loop as many times as we have targets to shoot
        for (int i = 0; i < validLockedTargets.Count; i++)
        {
            // Check which missile we are about to fire.
            // Your pod list is L/R/L/R, so even indexes (0, 2, 4) are LEFT.
            int currentMissileNum = pod.currentMissileIndex + i;
            bool isLeftMissile = (currentMissileNum % 2 == 0);

            if (isLeftMissile)
            {
                // This is a LEFT missile. Try to grab a LEFT target.
                if (leftIdx < leftTargets.Count)
                {
                    sortedTargets.Add(leftTargets[leftIdx++]);
                }
                // If no LEFT targets are left, grab a RIGHT target
                else if (rightIdx < rightTargets.Count)
                {
                    sortedTargets.Add(rightTargets[rightIdx++]);
                }
            }
            else // This is a RIGHT missile
            {
                // Try to grab a RIGHT target.
                if (rightIdx < rightTargets.Count)
                {
                    sortedTargets.Add(rightTargets[rightIdx++]);
                }
                // If no RIGHT targets are left, grab a LEFT target
                else if (leftIdx < leftTargets.Count)
                {
                    sortedTargets.Add(leftTargets[leftIdx++]);
                }
            }
        }
        
        // --- END OF NEW SORTING LOGIC ---


        // 5. Fire missiles, now using the *sorted* target list
        // foreach (var targetToShoot in validLockedTargets) // <-- OLD
        foreach (var targetToShoot in sortedTargets) // <-- NEW
        {
            // --- FIXED: Check if the target is already being engaged by a missile ---
            // (This check remains the same)
            if (targetToShoot.GetIncomingMissile() != null)
            {
                Debug.Log($"Skipping fire on {targetToShoot.Name}, as it already has an incoming missile.");
                continue; // Skip to the next target in the list
            }

            if (pod.currentMissileIndex < pod.missilesInPod.Count)
            {
                // Get the next missile in your L/R/L/R sequence
                BaseMissile missileToLaunch = pod.missilesInPod[pod.currentMissileIndex];
                
                Debug.Log($"Firing {pod.podName} #{pod.currentMissileIndex + 1} at {targetToShoot.Name}");

                missileToLaunch.target = targetToShoot.gameObject;
                missileToLaunch.shooter = this.gameObject;
                missileToLaunch.transform.SetParent(null);
                missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

                // --- ADD THIS BLOCK ---
                SpawnMissileCamera(missileToLaunch.GetComponent<Rigidbody>());
                // --- END OF ADD ---

                pod.currentMissileIndex++;
            }
            else
            {
                Debug.Log($"Pod '{pod.podName}' is empty. Cannot fire at remaining targets.");
                break;
            }
        }
    }
    
    // This AI method remains unchanged, as it's designed to fire at one specific target.
    public void FireAtTarget(GameObject specificTarget)
    {
        if (plane != null && !plane.BayDoorsOpen) return;
        if (specificTarget == null) return;

        foreach (MissilePod pod in missilePods)
        {
            if (pod.currentMissileIndex < pod.missilesInPod.Count)
            {
                BaseMissile missileToLaunch = pod.missilesInPod[pod.currentMissileIndex]; // <-- MODIFIED

                missileToLaunch.target = specificTarget;
                missileToLaunch.shooter = this.gameObject;
                missileToLaunch.transform.SetParent(null);
                missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

                // --- ADD THIS BLOCK ---
                SpawnMissileCamera(missileToLaunch.GetComponent<Rigidbody>());
                // --- END OF ADD ---

                pod.currentMissileIndex++;
                break; 
            }
        }
    }

    // --- ADD THIS NEW METHOD ---
    private void SpawnMissileCamera(Rigidbody missileRigidbody)
    {
        if (missileCameraPrefab == null)
        {
            Debug.LogWarning("MissileCameraPrefab is not assigned in AdvancedMissileController.");
            return;
        }

        if (MissileCameraManager.Instance == null)
        {
            Debug.LogError("MissileCameraManager is not in the scene!");
            return;
        }
        
        // Create the camera and get its components
        GameObject camGO = Instantiate(missileCameraPrefab);
        MissileCamera missileCam = camGO.GetComponent<MissileCamera>();
        Camera newCamera = camGO.GetComponent<Camera>();

        // Tell the camera what to follow
        missileCam.SetTarget(missileRigidbody);

        // Register it with the manager (it will be disabled by default)
        MissileCameraManager.Instance.RegisterMissileCamera(newCamera);
    }
    // --- END OF ADD ---
}