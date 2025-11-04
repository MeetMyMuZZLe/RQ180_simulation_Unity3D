// PlaneHUD.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using HomingMissile;

public class HUDElement
{
    public GameObject targetBoxGO;
    public Image targetBoxImage;
    public Text targetName;
    public Text targetRange;
    public Text targetClass; // ADDED: Field for the class name text

    public GameObject missileLockGO;
    public Image missileLockImage;

    public HUDElement(GameObject boxInstance, GameObject lockInstance)
    {
        targetBoxGO = boxInstance;
        targetBoxImage = boxInstance.GetComponent<Image>();
        targetName = boxInstance.transform.Find("TargetName").GetComponent<Text>();
        targetRange = boxInstance.transform.Find("TargetRange").GetComponent<Text>();
        targetClass = boxInstance.transform.Find("TargetClass").GetComponent<Text>(); // ADDED: Find the child component
        missileLockGO = lockInstance;
        missileLockImage = lockInstance.GetComponent<Image>();
    }

    public void SetActive(bool isActive)
    {
        targetBoxGO.SetActive(isActive);
        missileLockGO.SetActive(isActive);
    }
}

public class PlaneHUD : MonoBehaviour
{
    [SerializeField]
    float updateRate;
    [SerializeField]
    Color normalColor;
    [SerializeField]
    Color lockColor;
    [SerializeField]
    List<GameObject> helpDialogs;
    [SerializeField]
    Compass compass;
    [SerializeField]
    PitchLadder pitchLadder;
    [SerializeField]
    Bar throttleBar;
    [SerializeField]
    Transform hudCenter;
    [SerializeField]
    Transform velocityMarker;
    [SerializeField]
    Text airspeed;
    [SerializeField]
    Text aoaIndicator;
    [SerializeField]
    Text gforceIndicator;
    [SerializeField]
    Text altitude;
    [SerializeField]
    Bar healthBar;
    [SerializeField]
    Text healthText;
    [SerializeField]
    float targetArrowThreshold;
    [SerializeField]
    float missileArrowThreshold;
    [SerializeField]
    GameObject aiMessage;
    [SerializeField]
    List<Graphic> missileWarningGraphics;
    [SerializeField]
    RectTransform targetArrow;
    [SerializeField]
    RectTransform missileArrow;

    [Header("Multi-Lock UI Prefabs")]
    [SerializeField]
    private GameObject targetBoxPrefab;
    [SerializeField]
    private GameObject missileLockPrefab;
    [SerializeField]
    private Transform uiParent;

    private List<HUDElement> hudElementPool = new List<HUDElement>();
    private Dictionary<Target, HUDElement> activeHUDElements = new Dictionary<Target, HUDElement>();

    Plane plane;
    AIController aiController;
    Target selfTarget;
    Transform planeTransform;
    new Camera camera;
    Transform cameraTransform;

    GameObject hudCenterGO;
    GameObject velocityMarkerGO;
    GameObject targetArrowGO;
    GameObject missileArrowGO;

    float lastUpdateTime;

    const float metersToKnots = 1.94384f;
    const float metersToFeet = 3.28084f;

    private HUDElement GetHUDElementFromPool()
    {
        foreach (var element in hudElementPool)
        {
            if (!element.targetBoxGO.activeInHierarchy)
            {
                element.SetActive(true);
                return element;
            }
        }

        GameObject boxGO = Instantiate(targetBoxPrefab, uiParent);
        GameObject lockGO = Instantiate(missileLockPrefab, uiParent);
        HUDElement newElement = new HUDElement(boxGO, lockGO);
        hudElementPool.Add(newElement);
        return newElement;
    }

    private void ReturnHUDElementToPool(HUDElement element)
    {
        element.SetActive(false);
    }
    
    void Start() {
        hudCenterGO = hudCenter.gameObject;
        velocityMarkerGO = velocityMarker.gameObject;
        targetArrowGO = targetArrow.gameObject;
        missileArrowGO = missileArrow.gameObject;
    }
    
    public void SetPlane(Plane plane) {
        this.plane = plane;
        if (plane == null) {
            planeTransform = null;
            selfTarget = null;
        }
        else {
            aiController = plane.GetComponent<AIController>();
            planeTransform = plane.GetComponent<Transform>();
            selfTarget = plane.GetComponent<Target>();
            
            InitializeHUDElements();
        }
        if (compass != null) compass.SetPlane(plane);
        if (pitchLadder != null) pitchLadder.SetPlane(plane);
    }
    
    void InitializeHUDElements()
    {
        foreach (var element in activeHUDElements.Values)
        {
            ReturnHUDElementToPool(element);
        }
        activeHUDElements.Clear();

        if (plane.potentialTargets != null)
        {
            foreach (var target in plane.potentialTargets)
            {
                if (target != null)
                {
                    HUDElement newElement = GetHUDElementFromPool();
                    activeHUDElements.Add(target, newElement);
                }
            }
        }
    }
    
    public void SetCamera(Camera camera) {
        this.camera = camera;
        if (camera == null) {
            cameraTransform = null;
        } else {
            cameraTransform = camera.GetComponent<Transform>();
        }
        if (compass != null) compass.SetCamera(camera);
        if (pitchLadder != null) pitchLadder.SetCamera(camera);
    }

    public void ToggleHelpDialogs() {
        foreach (var dialog in helpDialogs) {
            dialog.SetActive(!dialog.activeSelf);
        }
    }

    void UpdateVelocityMarker() {
        var velocity = planeTransform.forward;
        if (plane.LocalVelocity.sqrMagnitude > 1) {
            velocity = plane.Rigidbody.linearVelocity;
        }
        var hudPos = TransformToHUDSpace(cameraTransform.position + velocity);
        if (hudPos.z > 0) {
            velocityMarkerGO.SetActive(true);
            velocityMarker.localPosition = new Vector3(hudPos.x, hudPos.y, 0);
        } else {
            velocityMarkerGO.SetActive(false);
        }
    }

    void UpdateAirspeed() {
        airspeed.text = string.Format("{0:0}", plane.LocalVelocity.z * metersToKnots);
    }

    void UpdateAOA() {
        aoaIndicator.text = string.Format("{0:0} AOA", plane.AngleOfAttack * Mathf.Rad2Deg);
    }

    void UpdateGForce() {
        gforceIndicator.text = string.Format("{0:0.0} G", plane.LocalGForce.y / 9.81f);
    }

    void UpdateAltitude() {
        altitude.text = string.Format("{0:0}", plane.Rigidbody.position.y * metersToFeet);
    }

    Vector3 TransformToHUDSpace(Vector3 worldSpace) {
        var screenSpace = camera.WorldToScreenPoint(worldSpace);
        return screenSpace - new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2);
    }

    void UpdateHUDCenter() {
        var rotation = cameraTransform.localEulerAngles;
        var hudPos = TransformToHUDSpace(cameraTransform.position + planeTransform.forward);
        if (hudPos.z > 0) {
            hudCenterGO.SetActive(true);
            hudCenter.localPosition = new Vector3(hudPos.x, hudPos.y, 0);
            hudCenter.localEulerAngles = new Vector3(0, 0, -rotation.z);
        } else {
            hudCenterGO.SetActive(false);
        }
    }

    void UpdateHealth() {
        healthBar.SetValue(plane.Health / plane.MaxHealth);
        healthText.text = string.Format("{0:0}", plane.Health);
    }
    
    void UpdateMultiLockUI()
    {
        if (plane == null) return;
        
        foreach (var info in plane.TrackedTargetsInfo.Values)
        {
            if (info.target == null || !activeHUDElements.ContainsKey(info.target)) continue;

            HUDElement ui = activeHUDElements[info.target];
            var targetDistance = Vector3.Distance(plane.Rigidbody.position, info.target.Position);
            var targetPos = TransformToHUDSpace(info.target.Position);

            if (targetPos.z > 0)
            {
                ui.targetBoxGO.SetActive(true);
                ui.targetBoxGO.transform.localPosition = new Vector3(targetPos.x, targetPos.y, 0);
                ui.targetName.text = info.target.Name;
                ui.targetRange.text = string.Format("{0:0 m}", targetDistance);

                // --- MODIFIED: Display the class name from the ScriptableObject ---
                if (info.target.targetClass != null)
                {
                    ui.targetClass.text = info.target.targetClass.name; // Use the asset's file name
                }
            }
            else
            {
                ui.targetBoxGO.SetActive(false);
            }
            
            if (info.isTracking && info.target.IsAlive)
            {
                var missileLockPos = info.isLocked ? targetPos : TransformToHUDSpace(plane.Rigidbody.position + info.seekerDirection * targetDistance);
                if (missileLockPos.z > 0)
                {
                    ui.missileLockGO.SetActive(true);
                    ui.missileLockGO.transform.localPosition = new Vector3(missileLockPos.x, missileLockPos.y, 0);
                }
            }
            else
            {
                ui.missileLockGO.SetActive(false);
            }
            
            Color currentColor;
            if (!info.target.IsAlive)
            {
                currentColor = Color.black;
            }
            else if (info.isLocked)
            {
                currentColor = lockColor;
            }
            else
            {
                currentColor = normalColor;
            }

            ui.targetBoxImage.color = currentColor;
            ui.targetName.color = currentColor;
            ui.targetRange.color = currentColor;
            ui.targetClass.color = currentColor; // Also color the class text
            ui.missileLockImage.color = currentColor;
        }
    }
    
    void UpdateWarnings() {
        // --- MODIFIED: 'incomingMissile' is now a Rigidbody, so I've renamed it for clarity ---
        var incomingMissileRb = selfTarget.GetIncomingMissile();
        
        if (incomingMissileRb != null) { // --- MODIFIED: Check the new variable
            
            // --- MODIFIED: Use .position directly, not .projectilerb.position ---
            var missilePos = TransformToHUDSpace(incomingMissileRb.position);
            var missileDir = (incomingMissileRb.position - plane.Rigidbody.position).normalized;
            // --- END OF MODIFICATIONS ---

            var missileAngle = Vector3.Angle(cameraTransform.forward, missileDir);
            
            if (missileAngle > missileArrowThreshold) {
                missileArrowGO.SetActive(true);
                float flip = missilePos.z > 0 ? 0 : 180;
                missileArrow.localEulerAngles = new Vector3(0, 0, flip + Vector2.SignedAngle(Vector2.up, new Vector2(missilePos.x, missilePos.y)));
            } else {
                missileArrowGO.SetActive(false);
            }
            foreach (var graphic in missileWarningGraphics) graphic.color = lockColor;
            pitchLadder.UpdateColor(lockColor);
            compass.UpdateColor(normalColor); // This was normalColor in your provided snippet
        } else {
            missileArrowGO.SetActive(false);
            foreach (var graphic in missileWarningGraphics) graphic.color = normalColor;
            pitchLadder.UpdateColor(normalColor);
            compass.UpdateColor(normalColor);
        }
    }

    void LateUpdate() {
        if (plane == null || camera == null) return;

        throttleBar.SetValue(plane.Throttle);

        if (!plane.Dead) {
            UpdateVelocityMarker();
            UpdateHUDCenter();
        } else {
            hudCenterGO.SetActive(false);
            velocityMarkerGO.SetActive(false);
        }

        if (aiController != null) {
            aiMessage.SetActive(aiController.enabled);
        }

        UpdateAirspeed();
        UpdateAltitude();
        UpdateHealth();
        UpdateMultiLockUI();
        UpdateWarnings();

        if (Time.time > lastUpdateTime + (1f / updateRate)) {
            UpdateAOA();
            UpdateGForce();
            lastUpdateTime = Time.time;
        }
    }
}