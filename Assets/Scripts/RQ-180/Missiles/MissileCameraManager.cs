// MissileCameraManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MissileCameraManager : MonoBehaviour
{
    // --- Singleton Setup ---
    public static MissileCameraManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    // --- End Singleton ---

    private List<Camera> activeCameras = new List<Camera>();
    private GameObject planeHUD;
    private int currentCameraIndex = 0;

    // The PlayerController will call this at the start
    public void RegisterPlaneCamera(Camera planeCam, GameObject hud)
    {
        if (planeCam != null && !activeCameras.Contains(planeCam))
        {
            activeCameras.Insert(0, planeCam); // Ensure plane cam is always index 0
        }
        planeHUD = hud;
        
        // Start by activating the plane camera and UI
        ActivateCamera(0); 
    }

    // The AdvancedMissileController will call this when firing
    public void RegisterMissileCamera(Camera missileCam)
    {
        if (missileCam != null)
        {
            activeCameras.Add(missileCam);
        }
    }

    // The MissileCamera calls this when it's destroyed
    public void UnregisterMissileCamera(Camera missileCam)
    {
        if (missileCam == null) return;
        
        // If the camera we are about to remove is the one we're looking at,
        // switch back to the plane camera first.
        if (activeCameras.Count > 0 && activeCameras[currentCameraIndex] == missileCam)
        {
            ActivatePlaneCamera();
        }
        
        activeCameras.Remove(missileCam);
    }

    // Called by PlayerController (D-pad Left)
    public void CycleToNextCamera()
    {
        if (activeCameras.Count == 0) return;

        // Move to the next index, loop back to 0 if at the end
        currentCameraIndex = (currentCameraIndex + 1) % activeCameras.Count;
        
        ActivateCamera(currentCameraIndex);
    }

    // Called by PlayerController (R-Stick)
    public void ActivatePlaneCamera()
    {
        if (activeCameras.Count == 0) return;
        
        currentCameraIndex = 0; // Plane camera is always index 0
        ActivateCamera(currentCameraIndex);
    }

    private void ActivateCamera(int index)
    {
        // Disable all cameras
        foreach (var cam in activeCameras)
        {
            if(cam != null) cam.enabled = false;
        }

        // Enable just the one we want
        if (index < activeCameras.Count && activeCameras[index] != null)
        {
            activeCameras[index].enabled = true;
        }

        // Show UI ONLY if it's the plane camera (index 0)
        if (planeHUD != null)
        {
            planeHUD.SetActive(index == 0);
        }
    }
}