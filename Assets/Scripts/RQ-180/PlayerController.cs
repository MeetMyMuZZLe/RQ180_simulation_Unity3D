// PlayerController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour {
    [SerializeField]
    new Camera camera; // This is your main plane camera
    [SerializeField]
    Plane plane;
    [SerializeField]
    PlaneHUD planeHUD; // This is your main UI

    Vector3 controlInput;
    PlaneCamera planeCamera;
    AIController aiController;
    private AdvancedMissileController advancedMissileController; // Reference to the new controller

    void Start() {
        planeCamera = GetComponent<PlaneCamera>();
        SetPlane(plane);

        // --- ADD THIS BLOCK ---
        // Register the main plane camera and HUD with the manager.
        if (MissileCameraManager.Instance != null)
        {
            MissileCameraManager.Instance.RegisterPlaneCamera(camera, planeHUD.gameObject);
        }
        else
        {
            Debug.LogError("MissileCameraManager is not in the scene! Camera switching will not work.");
        }
        // --- END OF ADD ---
    }

    void SetPlane(Plane plane) {
        this.plane = plane;
        if (plane != null)
        {
            aiController = plane.GetComponent<AIController>();
            // Get the new missile controller from the plane
            advancedMissileController = plane.GetComponent<AdvancedMissileController>();
        }

        if (planeHUD != null) {
            planeHUD.SetPlane(plane);
            planeHUD.SetCamera(camera);
        }
        planeCamera.SetPlane(plane);
    }
    public void OnToggleHelp(InputAction.CallbackContext context) {
        if (plane == null) return;
        if (context.phase == InputActionPhase.Performed) {
            planeHUD.ToggleHelpDialogs();
        }
    }

    public void SetThrottleInput(InputAction.CallbackContext context) {
        if (plane == null) return;
        if (aiController.enabled) return;
        plane.SetThrottleInput(context.ReadValue<float>());
    }

    public void OnRollPitchInput(InputAction.CallbackContext context) {
        if (plane == null) return;
        var input = context.ReadValue<Vector2>();
        controlInput = new Vector3(input.y, controlInput.y, -input.x);
    }

    public void OnYawInput(InputAction.CallbackContext context) {
        if (plane == null) return;
        var input = context.ReadValue<float>();
        controlInput = new Vector3(controlInput.x, input, controlInput.z);
    }

    public void OnCameraInput(InputAction.CallbackContext context) {
       if (plane == null) return;
       var input = context.ReadValue<Vector2>();
       planeCamera.SetInput(input);
    }

    public void OnLandingGearInput(InputAction.CallbackContext context) {
        if (plane == null) return;
        if (context.phase == InputActionPhase.Performed) {
            plane.ToggleLandingGear();
        }
    }
    
    public void OnToggleBayDoors(InputAction.CallbackContext context) {
        if (plane == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            plane.ToggleBayDoors();
        }
    }

    public void OnToggleFlaps(InputAction.CallbackContext context) {
        if (plane == null) return;

        if (context.phase == InputActionPhase.Performed) {
            plane.ToggleFlaps();
        }
    }
    
    // --- New Input Handlers for Firing Pods ---
    public void OnFirePod1(InputAction.CallbackContext context)
    {
        if (plane == null || advancedMissileController == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            advancedMissileController.FireMissileFromPod(0); // Fires from the first pod in the list
        }
    }
    
    public void OnFirePod2(InputAction.CallbackContext context)
    {
        if (plane == null || advancedMissileController == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            advancedMissileController.FireMissileFromPod(1); // Fires from the second pod
        }
    }

    public void OnFirePod3(InputAction.CallbackContext context)
    {
        if (plane == null || advancedMissileController == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            advancedMissileController.FireMissileFromPod(2); // Fires from the third pod
        }
    }

    // --- ADD THESE NEW INPUT METHODS ---

    // Assign this to your "D-pad Left" input action
    public void OnCycleCamera(InputAction.CallbackContext context)
    {
        if (plane == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            if (MissileCameraManager.Instance != null) // Added null check
            {
                MissileCameraManager.Instance.CycleToNextCamera();
            }
        }
    }

    // Assign this to your "R-Stick Button" input action
    public void OnResetCamera(InputAction.CallbackContext context)
    {
        if (plane == null) return;
        if (context.phase == InputActionPhase.Performed)
        {
            if (MissileCameraManager.Instance != null) // Added null check
            {
                MissileCameraManager.Instance.ActivatePlaneCamera();
            }
        }
    }
    // --- END OF ADD ---

    public void OnToggleAI(InputAction.CallbackContext context) {
        if (plane == null) return;
        if (aiController != null) {
            aiController.enabled = !aiController.enabled;
        }
    }

    void Update() {
       if (plane == null) return;
       if (aiController != null && aiController.enabled) return;
       plane.SetControlInput(controlInput);
    }
}