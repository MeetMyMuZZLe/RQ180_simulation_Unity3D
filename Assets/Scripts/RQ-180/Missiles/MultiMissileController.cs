// // MultiMissileController.cs
// using System.Collections.Generic;
// using UnityEngine;
// using HomingMissile; // Use your namespace to access the missile scripts
// using System.Linq;

// // --- Define the 3 Type-Safe Pods ---
// // These will show up in the Inspector, and you can
// // only drag the correct missile prefab into each list.

// [System.Serializable]
// public class MaverickPod
// {
//     public string podName = "Maverick Pod";
//     public List<Maverick> missilesInPod;
//     public List<TargetClassDefinition> validTargetClasses;
//     [HideInInspector]
//     public int currentMissileIndex = 0;
// }

// [System.Serializable]
// public class BrimstonePod
// {
//     public string podName = "Brimstone Pod";
//     public List<Brimstone> missilesInPod;
//     public List<TargetClassDefinition> validTargetClasses;
//     [HideInInspector]
//     public int currentMissileIndex = 0;
// }

// [System.Serializable]
// public class JSOWPod
// {
//     public string podName = "JSOW Pod";
//     public List<JSOW> missilesInPod;
//     public List<TargetClassDefinition> validTargetClasses;
//     [HideInInspector]
//     public int currentMissileIndex = 0;
// }


// // --- The Main Controller ---
// // Attach this to your drone in place of AdvancedMissileController.cs
// public class MultiMissileController : MonoBehaviour
// {
//     [Header("Missile Pods")]
//     public MaverickPod maverickPod;
//     public BrimstonePod brimstonePod;
//     public JSOWPod jsowPod;
    
//     private Plane plane;

//     void Start()
//     {
//         plane = GetComponent<Plane>();
//     }

//     // --- Player-Controlled Ripple Fire ---
//     // This is called by your PlayerController.
//     // podIndex 0 = Maverick, 1 = Brimstone, 2 = JSOW
//     public void FireMissileFromPod(int podIndex)
//     {
//         if (plane == null || !plane.BayDoorsOpen) return;
        
//         List<Target> lockedTargets = plane.GetLockedTargets();

//         switch (podIndex)
//         {
//             // --- CASE 0: FIRE MAVERICK POD ---
//             case 0:
//                 List<Target> validMavTargets = lockedTargets
//                     .Where(t => t.targetClass != null && maverickPod.validTargetClasses.Contains(t.targetClass))
//                     .ToList();

//                 if (validMavTargets.Count == 0)
//                 {
//                     Debug.Log($"Fire command for '{maverickPod.podName}', but no valid targets are locked.");
//                     return;
//                 }

//                 foreach (var targetToShoot in validMavTargets)
//                 {
//                     if (targetToShoot.GetIncomingMissile() != null)
//                     {
//                         Debug.Log($"Skipping fire on {targetToShoot.Name}, as it already has an incoming missile.");
//                         continue; 
//                     }

//                     if (maverickPod.currentMissileIndex < maverickPod.missilesInPod.Count)
//                     {
//                         // Get the specific missile type
//                         Maverick missileToLaunch = maverickPod.missilesInPod[maverickPod.currentMissileIndex];
                        
//                         Debug.Log($"Firing {maverickPod.podName} #{maverickPod.currentMissileIndex + 1} at {targetToShoot.Name}");

//                         // All your missile scripts share the same launch method and fields
//                         missileToLaunch.target = targetToShoot.gameObject;
//                         missileToLaunch.shooter = this.gameObject;
//                         missileToLaunch.transform.SetParent(null);
//                         missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//                         maverickPod.currentMissileIndex++;
//                     }
//                     else
//                     {
//                         Debug.Log($"Pod '{maverickPod.podName}' is empty.");
//                         break;
//                     }
//                 }
//                 break;
            
//             // --- CASE 1: FIRE BRIMSTONE POD ---
//             case 1:
//                 List<Target> validBrimTargets = lockedTargets
//                     .Where(t => t.targetClass != null && brimstonePod.validTargetClasses.Contains(t.targetClass))
//                     .ToList();
                
//                 if (validBrimTargets.Count == 0)
//                 {
//                     Debug.Log($"Fire command for '{brimstonePod.podName}', but no valid targets are locked.");
//                     return;
//                 }

//                 foreach (var targetToShoot in validBrimTargets)
//                 {
//                     if (targetToShoot.GetIncomingMissile() != null) continue; 

//                     if (brimstonePod.currentMissileIndex < brimstonePod.missilesInPod.Count)
//                     {
//                         // Get the specific missile type
//                         Brimstone missileToLaunch = brimstonePod.missilesInPod[brimstonePod.currentMissileIndex];
                        
//                         Debug.Log($"Firing {brimstonePod.podName} #{brimstonePod.currentMissileIndex + 1} at {targetToShoot.Name}");

//                         missileToLaunch.target = targetToShoot.gameObject;
//                         missileToLaunch.shooter = this.gameObject;
//                         missileToLaunch.transform.SetParent(null);
//                         missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//                         brimstonePod.currentMissileIndex++;
//                     }
//                     else
//                     {
//                         Debug.Log($"Pod '{brimstonePod.podName}' is empty.");
//                         break;
//                     }
//                 }
//                 break;

//             // --- CASE 2: FIRE JSOW POD ---
//             case 2:
//                 List<Target> validJSOWTargets = lockedTargets
//                     .Where(t => t.targetClass != null && jsowPod.validTargetClasses.Contains(t.targetClass))
//                     .ToList();

//                 if (validJSOWTargets.Count == 0)
//                 {
//                     Debug.Log($"Fire command for '{jsowPod.podName}', but no valid targets are locked.");
//                     return;
//                 }
                
//                 foreach (var targetToShoot in validJSOWTargets)
//                 {
//                     if (targetToShoot.GetIncomingMissile() != null) continue;

//                     if (jsowPod.currentMissileIndex < jsowPod.missilesInPod.Count)
//                     {
//                         // Get the specific missile type
//                         JSOW missileToLaunch = jsowPod.missilesInPod[jsowPod.currentMissileIndex];
                        
//                         Debug.Log($"Firing {jsowPod.podName} #{jsowPod.currentMissileIndex + 1} at {targetToShoot.Name}");

//                         missileToLaunch.target = targetToShoot.gameObject;
//                         missileToLaunch.shooter = this.gameObject;
//                         missileToLaunch.transform.SetParent(null);
//                         missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//                         jsowPod.currentMissileIndex++;
//                     }
//                     else
//                     {
//                         Debug.Log($"Pod '{jsowPod.podName}' is empty.");
//                         break;
//                     }
//                 }
//                 break;
//         }
//     }
    
//     // --- AI-Controlled Firing ---
//     // This method now intelligently checks which pod can fire at the specific target.
//     public void FireAtTarget(GameObject specificTarget)
//     {
//         if (plane != null && !plane.BayDoorsOpen) return;
//         if (specificTarget == null) return;

//         Target targetComponent = specificTarget.GetComponent<Target>();
//         if (targetComponent == null || targetComponent.targetClass == null)
//         {
//             Debug.LogWarning("AI tried to fire at a target with no Target component or TargetClass.");
//             return;
//         }

//         TargetClassDefinition targetClass = targetComponent.targetClass;

//         // AI will try to find the first available missile that is valid for this target.
//         // It prioritizes Maverick > Brimstone > JSOW.
        
//         // 1. Try to fire a Maverick
//         if (maverickPod.validTargetClasses.Contains(targetClass) && maverickPod.currentMissileIndex < maverickPod.missilesInPod.Count)
//         {
//             Maverick missileToLaunch = maverickPod.missilesInPod[maverickPod.currentMissileIndex];
            
//             missileToLaunch.target = specificTarget;
//             missileToLaunch.shooter = this.gameObject;
//             missileToLaunch.transform.SetParent(null);
//             missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//             maverickPod.currentMissileIndex++;
//         }
//         // 2. Else, try to fire a Brimstone
//         else if (brimstonePod.validTargetClasses.Contains(targetClass) && brimstonePod.currentMissileIndex < brimstonePod.missilesInPod.Count)
//         {
//             Brimstone missileToLaunch = brimstonePod.missilesInPod[brimstonePod.currentMissileIndex];
            
//             missileToLaunch.target = specificTarget;
//             missileToLaunch.shooter = this.gameObject;
//             missileToLaunch.transform.SetParent(null);
//             missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//             brimstonePod.currentMissileIndex++;
//         }
//         // 3. Else, try to fire a JSOW
//         else if (jsowPod.validTargetClasses.Contains(targetClass) && jsowPod.currentMissileIndex < jsowPod.missilesInPod.Count)
//         {
//             JSOW missileToLaunch = jsowPod.missilesInPod[jsowPod.currentMissileIndex];
            
//             missileToLaunch.target = specificTarget;
//             missileToLaunch.shooter = this.gameObject;
//             missileToLaunch.transform.SetParent(null);
//             missileToLaunch.usemissile(plane.Rigidbody.linearVelocity);

//             jsowPod.currentMissileIndex++;
//         }
//         else
//         {
//             Debug.Log($"AI: No valid missile pods available for target {specificTarget.name} of class {targetClass.name}.");
//         }
//     }
// }