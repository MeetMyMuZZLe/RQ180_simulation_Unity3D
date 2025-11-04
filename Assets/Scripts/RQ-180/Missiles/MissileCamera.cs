// MissileCamera.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MissileCamera : MonoBehaviour
{
    [SerializeField]
    private Vector3 cameraOffset = new Vector3(0, 5, -15);

    [Header("Smoothing")]
    [SerializeField]
    [Range(0.01f, 0.5f)]
    private float positionAlpha = 0.05f; 
    
    [SerializeField]
    [Range(0.01f, 0.5f)]
    private float rotationAlpha = 0.05f;

    [Header("Explosion View")]
    [SerializeField]
    private float hangTimeAfterExplosion = 2.0f; // How long to wait

    private Rigidbody targetRigidbody;
    private new Camera camera;
    private Vector3 smoothPosition;
    private Quaternion smoothRotation;

    // --- NEW: State variables ---
    private bool isTargetDestroyed = false;
    private float destroyTimer;
    // --- END NEW ---

    void Awake()
    {
        camera = GetComponent<Camera>();
        destroyTimer = hangTimeAfterExplosion; // Initialize the timer
    }

    public void SetTarget(Rigidbody target)
    {
        targetRigidbody = target;
        
        Vector3 desiredPosition = targetRigidbody.position + (targetRigidbody.rotation * cameraOffset);
        Quaternion targetRotation = Quaternion.LookRotation(targetRigidbody.position - desiredPosition);
        
        smoothPosition = desiredPosition;
        smoothRotation = targetRotation;
        
        transform.position = smoothPosition;
        transform.rotation = smoothRotation;
    }

    void LateUpdate()
    {
        // --- THIS IS THE NEW LOGIC ---

        // 1. Check if the target is destroyed
        if (targetRigidbody == null)
        {
            isTargetDestroyed = true; // Mark it as destroyed
        }

        // 2. If it's destroyed, start the countdown
        if (isTargetDestroyed)
        {
            destroyTimer -= Time.deltaTime;
            if (destroyTimer <= 0)
            {
                Destroy(this.gameObject); // Now destroy the camera
            }
            // Do NOT follow the target anymore, just stay put.
            return; 
        }

        // --- END NEW LOGIC ---

        // 3. If target is NOT destroyed, follow it (this is your old logic)
        Vector3 desiredPosition = targetRigidbody.position + (targetRigidbody.rotation * cameraOffset);
        Quaternion targetRotation = Quaternion.LookRotation(targetRigidbody.position - smoothPosition); 

        smoothPosition = (smoothPosition * (1 - positionAlpha)) + (desiredPosition * positionAlpha);
        smoothRotation = Quaternion.Slerp(smoothRotation, targetRotation, rotationAlpha);

        transform.position = smoothPosition;
        transform.rotation = smoothRotation;
    }

    void OnDestroy()
    {
        if (MissileCameraManager.Instance != null)
        {
            MissileCameraManager.Instance.UnregisterMissileCamera(this.camera);
        }
    }
}