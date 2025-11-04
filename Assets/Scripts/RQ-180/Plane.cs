// Plane.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// This is the helper class we created for the multi-lock system.
public class TargetInfo
{
    public Target target;
    public bool isTracking = false;
    public bool isLocked = false;
    public Vector3 seekerDirection;

    public TargetInfo(Target target, Vector3 initialDirection)
    {
        this.target = target;
        this.seekerDirection = initialDirection;
    }
}

[System.Serializable]
public class LockParameters
{
    public TargetClassDefinition targetClass;
    public float lockRange = 8000f;
    public float lockAngle = 45f;
}

public class Plane : MonoBehaviour
{
    [SerializeField]
    float maxHealth;
    [SerializeField]
    float health;
    [SerializeField]
    float maxThrust;
    [SerializeField]
    float throttleSpeed;
    [SerializeField]
    float gLimit;
    [SerializeField]
    float gLimitPitch;

    [Header("Lift")]
    [SerializeField]
    float liftPower;
    [SerializeField]
    AnimationCurve liftAOACurve;
    [SerializeField]
    [Tooltip("Extra lift power generated when the gear is down.")]
    float gearDownLiftPower;
    [SerializeField]
    [Tooltip("Angle of Attack bias in degrees when the gear is down.")]
    float gearDownAOABias;
    [SerializeField]
    [Tooltip("Extra form drag generated when the gear is down.")]
    float gearDownDrag;
    [SerializeField]
    float inducedDrag;
    [SerializeField]
    AnimationCurve inducedDragCurve;
    [SerializeField]
    float rudderPower;
    [SerializeField]
    AnimationCurve rudderAOACurve;
    [SerializeField]
    AnimationCurve rudderInducedDragCurve;
    [SerializeField]
    float flapsLiftPower;
    [SerializeField]
    float flapsAOABias;
    [SerializeField]
    float flapsDrag;
    [SerializeField]
    float flapsRetractSpeed;


    [Header("Steering")]
    [SerializeField]
    Vector3 turnSpeed;
    [SerializeField]
    Vector3 turnAcceleration;
    [SerializeField]
    AnimationCurve steeringCurve;

    [Header("Drag")]
    [SerializeField]
    AnimationCurve dragForward;
    [SerializeField]
    AnimationCurve dragBack;
    [SerializeField]
    AnimationCurve dragLeft;
    [SerializeField]
    AnimationCurve dragRight;
    [SerializeField]
    AnimationCurve dragTop;
    [SerializeField]
    AnimationCurve dragBottom;
    [SerializeField]
    Vector3 angularDrag;
    [SerializeField]
    float airbrakeDrag;

    [Header("Weapons")]
    [SerializeField]
    public List<Target> potentialTargets;
    [SerializeField]
    private float lockSpeed = 2f;
    [SerializeField]
    public List<LockParameters> lockSettings;

    [Header("Misc")]
    [SerializeField]
    List<Collider> landingGear;
    [SerializeField]
    PhysicsMaterial landingGearBrakesMaterial;
    [SerializeField]
    List<GameObject> graphics;
    [SerializeField]
    GameObject damageEffect;
    [SerializeField]
    private List<GameObject> deathEffects;
    [SerializeField]
    bool landingGearDeployed;
    [SerializeField]
    float gearRetractSpeed;
    [SerializeField]
    float initialSpeed;
    [SerializeField]
    private bool flapsDeployed;
    
    public Dictionary<Target, TargetInfo> TrackedTargetsInfo { get; private set; }
    new PlaneAnimation animation;
    float throttleInput;
    Vector3 controlInput;
    Vector3 lastVelocity;
    PhysicsMaterial landingGearDefaultMaterial;
    
    private Dictionary<TargetClassDefinition, LockParameters> lockSettingsDict;

    public float MaxHealth {
        get { return maxHealth; }
        set { maxHealth = Mathf.Max(0, value); }
    }

    public float Health {
        get { return health; }
        private set {
            health = Mathf.Clamp(value, 0, maxHealth);
            if (damageEffect != null)
            {
                damageEffect.SetActive(health < maxHealth && health > 0);
            }
            if (health == 0 && MaxHealth != 0 && !Dead) {
                Die();
            }
        }
    }
    public bool Dead { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public float Throttle { get; private set; }
    public Vector3 EffectiveInput { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 LocalVelocity { get; private set; }
    public Vector3 LocalGForce { get; private set; }
    public Vector3 LocalAngularVelocity { get; private set; }
    public float AngleOfAttack { get; private set; }
    public float AngleOfAttackYaw { get; private set; }
    public bool AirbrakeDeployed { get; private set; }
    public bool BayDoorsOpen { get; private set; }
    public bool LandingGearDeployed {
        get { return landingGearDeployed; }
        private set {
            landingGearDeployed = value;
            foreach (var lg in landingGear) {
                lg.enabled = value;
            }
        }
    }

    public bool FlapsDeployed {
        get { return flapsDeployed; }
        private set { flapsDeployed = value; }
    }
    
    public List<Target> GetLockedTargets()
    {
        return TrackedTargetsInfo.Values.Where(info => info.isLocked).Select(info => info.target).ToList();
    }
    
    private LockParameters GetLockParameters(TargetClassDefinition targetClass)
    {
        if (lockSettingsDict.ContainsKey(targetClass))
        {
            return lockSettingsDict[targetClass];
        }
        return null;
    }

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        animation = GetComponent<PlaneAnimation>();

        if (landingGear.Count > 0) {
            landingGearDefaultMaterial = landingGear[0].sharedMaterial;
        }
        
        if (Rigidbody != null && initialSpeed > 0)
        {
            Rigidbody.linearVelocity = Rigidbody.rotation * new Vector3(0, 0, initialSpeed);
        }

        lockSettingsDict = new Dictionary<TargetClassDefinition, LockParameters>();
        foreach (var setting in lockSettings)
        {
            if (setting.targetClass != null && !lockSettingsDict.ContainsKey(setting.targetClass))
            {
                lockSettingsDict.Add(setting.targetClass, setting);
            }
        }
        
        TrackedTargetsInfo = new Dictionary<Target, TargetInfo>();
        if (potentialTargets != null)
        {
            foreach (var target in potentialTargets)
            {
                if (target != null && !TrackedTargetsInfo.ContainsKey(target))
                {
                    TrackedTargetsInfo.Add(target, new TargetInfo(target, transform.forward));
                }
            }
        }
    }

    void UpdateMultiLock(float dt)
    {
        foreach (var info in TrackedTargetsInfo.Values)
        {
            if (info.target == null || !info.target.gameObject.activeInHierarchy)
            {
                info.isTracking = false;
                info.isLocked = false;
                continue;
            }

            LockParameters pars = GetLockParameters(info.target.targetClass);
            if (pars == null)
            {
                info.isTracking = false;
                info.isLocked = false;
                continue;
            }

            var error = info.target.Position - Rigidbody.position;
            var targetDir = error.normalized;
            var range = error.magnitude;

            if (info.target.IsAlive && range < pars.lockRange && Vector3.Angle(transform.forward, targetDir) < pars.lockAngle)
            {
                info.isTracking = true;
                info.seekerDirection = Vector3.RotateTowards(info.seekerDirection, targetDir, lockSpeed * dt, 0);
                info.isLocked = Vector3.Angle(info.seekerDirection, targetDir) < 1f;
            }
            else
            {
                info.isTracking = false;
                info.isLocked = false;
                info.seekerDirection = Vector3.RotateTowards(info.seekerDirection, transform.forward, lockSpeed * 2 * dt, 0);
            }
        }
    }

    public void SetThrottleInput(float input) {
        if (Dead) return;
        throttleInput = input;
    }

    public void SetControlInput(Vector3 input) {
        if (Dead) return;
        controlInput = Vector3.ClampMagnitude(input, 1);
    }

    public void ToggleLandingGear() {
        if (LocalVelocity.z < gearRetractSpeed) {
            LandingGearDeployed = !LandingGearDeployed;
        }
    }

    public void ToggleFlaps() {
        if (LocalVelocity.z < flapsRetractSpeed) {
            FlapsDeployed = !FlapsDeployed;
        }
    }

    public void ToggleBayDoors() {
        if (Dead) return;
        BayDoorsOpen = !BayDoorsOpen;
    }

    public void ApplyDamage(float damage) {
        Health -= damage;
    }

    void Die() {
        throttleInput = 0;
        Throttle = 0;
        Dead = true;

        if (damageEffect != null)
        {
            var ps = damageEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            } else {
                damageEffect.SetActive(false);
            }
        }
        
        foreach (var effect in deathEffects)
        {
            if (effect != null)
            {
                effect.SetActive(true);
            }
        }

        foreach (var graphic in graphics)
        {
            graphic.SetActive(false);
        }
    }
    
    void UpdateThrottle(float dt) {
        float target = 0;
        if (throttleInput > 0) target = 1;
        Throttle = Utilities.MoveTo(Throttle, target, throttleSpeed * Mathf.Abs(throttleInput), dt);
        AirbrakeDeployed = Throttle == 0 && throttleInput == -1;
        if (AirbrakeDeployed) {
            foreach (var lg in landingGear) {
                lg.sharedMaterial = landingGearBrakesMaterial;
            }
        } else {
            foreach (var lg in landingGear) {
                lg.sharedMaterial = landingGearDefaultMaterial;
            }
        }
    }

    void UpdateLandingGearLogic() {
        if (LocalVelocity.z > gearRetractSpeed) {
            LandingGearDeployed = false;
        }
    }
    
    void UpdateFlapsLogic() {
        if (LocalVelocity.z > flapsRetractSpeed) {
            FlapsDeployed = false;
        }
    }

    void CalculateAngleOfAttack() {
        if (LocalVelocity.sqrMagnitude < 0.1f) {
            AngleOfAttack = 0;
            AngleOfAttackYaw = 0;
            return;
        }
        AngleOfAttack = Mathf.Atan2(-LocalVelocity.y, LocalVelocity.z);
        AngleOfAttackYaw = Mathf.Atan2(LocalVelocity.x, LocalVelocity.z);
    }

    void CalculateGForce(float dt) {
        var invRotation = Quaternion.Inverse(Rigidbody.rotation);
        var acceleration = (Velocity - lastVelocity) / dt;
        LocalGForce = invRotation * acceleration;
        lastVelocity = Velocity;
    }

    void CalculateState(float dt) {
        var invRotation = Quaternion.Inverse(Rigidbody.rotation);
        Velocity = Rigidbody.linearVelocity;
        LocalVelocity = invRotation * Velocity;
        LocalAngularVelocity = invRotation * Rigidbody.angularVelocity;
        CalculateAngleOfAttack();
    }

    void UpdateThrust() {
        Rigidbody.AddRelativeForce(Throttle * maxThrust * Vector3.forward);
    }
    
    // --- METHOD REPLACED ---
    void UpdateDrag() {
        var lv = LocalVelocity;
        var lv2 = lv.sqrMagnitude;

        float airbrakeDrag = AirbrakeDeployed ? this.airbrakeDrag : 0;
        float currentGearDrag = LandingGearDeployed ? gearDownDrag : 0f;
        float currentFlapsDrag = FlapsDeployed ? flapsDrag : 0f;

        var coefficient = Utilities.Scale6(
            lv.normalized,
            dragRight.Evaluate(Mathf.Abs(lv.x)), dragLeft.Evaluate(Mathf.Abs(lv.x)),
            dragTop.Evaluate(Mathf.Abs(lv.y)), dragBottom.Evaluate(Mathf.Abs(lv.y)),
            dragForward.Evaluate(Mathf.Abs(lv.z)) + airbrakeDrag + currentGearDrag + currentFlapsDrag,
            dragBack.Evaluate(Mathf.Abs(lv.z))
        );

        var drag = coefficient.magnitude * lv2 * -lv.normalized;
        Rigidbody.AddRelativeForce(drag);
    }

    Vector3 CalculateLift(float angleOfAttack, Vector3 rightAxis, float liftPower, AnimationCurve aoaCurve, AnimationCurve inducedDragCurve) {
        var liftVelocity = Vector3.ProjectOnPlane(LocalVelocity, rightAxis);
        var v2 = liftVelocity.sqrMagnitude;
        var liftCoefficient = aoaCurve.Evaluate(angleOfAttack * Mathf.Rad2Deg);
        var liftForce = v2 * liftCoefficient * liftPower;
        var liftDirection = Vector3.Cross(liftVelocity.normalized, rightAxis);
        var lift = liftDirection * liftForce;
        var dragForce = liftCoefficient * liftCoefficient;
        var dragDirection = -liftVelocity.normalized;
        var inducedDrag = dragDirection * v2 * dragForce * this.inducedDrag * inducedDragCurve.Evaluate(Mathf.Max(0, LocalVelocity.z));
        return lift + inducedDrag;
    }
    
    void UpdateLift() {
        if (LocalVelocity.sqrMagnitude < 1f) return;

        float currentGearLift = LandingGearDeployed ? gearDownLiftPower : 0f;
        float currentFlapsLift = FlapsDeployed ? flapsLiftPower : 0f;
        float currentGearAOABias = LandingGearDeployed ? gearDownAOABias : 0f;
        float currentFlapsAOABias = FlapsDeployed ? flapsAOABias : 0f;

        var liftForce = CalculateLift(
            AngleOfAttack + ((currentGearAOABias + currentFlapsAOABias) * Mathf.Deg2Rad),
            Vector3.right,
            liftPower + currentGearLift + currentFlapsLift,
            liftAOACurve,
            inducedDragCurve
        );

        var yawForce = CalculateLift(AngleOfAttackYaw, Vector3.up, rudderPower, rudderAOACurve, rudderInducedDragCurve);

        Rigidbody.AddRelativeForce(liftForce);
        Rigidbody.AddRelativeForce(yawForce);
    }

    void UpdateAngularDrag() {
       var av = LocalAngularVelocity;
       var drag = av.sqrMagnitude * -av.normalized;
       Rigidbody.AddRelativeTorque(Vector3.Scale(drag, angularDrag), ForceMode.Acceleration);
    }

    Vector3 CalculateGForce(Vector3 angularVelocity, Vector3 velocity) {
        return Vector3.Cross(angularVelocity, velocity);
    }

    Vector3 CalculateGForceLimit(Vector3 input) {
        return Utilities.Scale6(input,
            gLimit, gLimitPitch,
            gLimit, gLimit,
            gLimit, gLimit
        ) * 9.81f;
    }



    float CalculateGLimiter(Vector3 controlInput, Vector3 maxAngularVelocity) {
        if (controlInput.magnitude < 0.01f) return 1;
        var maxInput = controlInput.normalized;
        var limit = CalculateGForceLimit(maxInput);
        var maxGForce = CalculateGForce(Vector3.Scale(maxInput, maxAngularVelocity), LocalVelocity);
        if (maxGForce.magnitude > limit.magnitude) {
            return limit.magnitude / maxGForce.magnitude;
        }
        return 1;
    }

    float CalculateSteering(float dt, float angularVelocity, float targetVelocity, float acceleration) {
        var error = targetVelocity - angularVelocity;
        var accel = acceleration * dt;
        return Mathf.Clamp(error, -accel, accel);
    }

    void UpdateSteering(float dt) {
       var speed = Mathf.Max(0, LocalVelocity.z);
       var steeringPower = steeringCurve.Evaluate(speed);
       var gForceScaling = CalculateGLimiter(controlInput, turnSpeed * Mathf.Deg2Rad * steeringPower);
       var targetAV = Vector3.Scale(controlInput, turnSpeed * steeringPower * gForceScaling);
       var av = LocalAngularVelocity * Mathf.Rad2Deg;
       var correction = new Vector3(
           CalculateSteering(dt, av.x, targetAV.x, turnAcceleration.x * steeringPower),
           CalculateSteering(dt, av.y, targetAV.y, turnAcceleration.y * steeringPower),
           CalculateSteering(dt, av.z, targetAV.z, turnAcceleration.z * steeringPower)
       );
       Rigidbody.AddRelativeTorque(correction * Mathf.Deg2Rad, ForceMode.VelocityChange);
       var correctionInput = new Vector3(
           Mathf.Clamp((targetAV.x - av.x) / turnAcceleration.x, -1, 1),
           Mathf.Clamp((targetAV.y - av.y) / turnAcceleration.y, -1, 1),
           Mathf.Clamp((targetAV.z - av.z) / turnAcceleration.z, -1, 1)
       );
       var effectiveInput = (correctionInput + controlInput) * gForceScaling;
       EffectiveInput = new Vector3(
           Mathf.Clamp(effectiveInput.x, -1, 1),
           Mathf.Clamp(effectiveInput.y, -1, 1),
           Mathf.Clamp(effectiveInput.z, -1, 1)
       );
    }

    void FixedUpdate() {
        float dt = Time.fixedDeltaTime;

        CalculateState(dt);
        CalculateGForce(dt);
        UpdateLandingGearLogic();
        UpdateFlapsLogic();

        UpdateThrottle(dt);

        if (!Dead) {
            UpdateThrust();
            UpdateLift();
            UpdateSteering(dt);
            UpdateMultiLock(dt);
        } else {
            Vector3 up = Rigidbody.rotation * Vector3.up;
            Vector3 forward = Rigidbody.linearVelocity.normalized;
            Rigidbody.rotation = Quaternion.LookRotation(forward, up);
        }

        UpdateDrag();
        UpdateAngularDrag();

        CalculateState(dt);
    }

    void OnCollisionEnter(Collision collision) {
       for (int i = 0; i < collision.contactCount; i++) {
            var contact = collision.contacts[i];
            if (landingGear.Contains(contact.thisCollider)) {
                return;
            }
            Health = 0;
            Rigidbody.isKinematic = true;
            Rigidbody.position = contact.point;
            Rigidbody.rotation = Quaternion.Euler(0, Rigidbody.rotation.eulerAngles.y, 0);
            foreach (var go in graphics) {
                go.SetActive(false);
            }
            return;
        }
    }
}