// PlaneAnimation.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LandingGearPart {
    public Transform transform;
    public float deflectionAngle;
}

[System.Serializable]
public class BayDoorPart {
    public Transform transform;
    public float deflectionAngle;
}

public class PlaneAnimation : MonoBehaviour {
    [SerializeField]
    List<GameObject> afterburnerGraphics;
    [SerializeField]
    float afterburnerThreshold;
    [SerializeField]
    float afterburnerMinSize;
    [SerializeField]
    float afterburnerMaxSize;
    [SerializeField]
    float maxAileronDeflection;
    [SerializeField]
    float maxElevatorDeflection;
    [SerializeField]
    float maxRudderDeflection;
    [SerializeField]
    float airbrakeDeflection;
    [SerializeField]
    float deflectionSpeed;
    [SerializeField]
    Transform rightAileron;
    [SerializeField]
    Transform leftAileron;
    [SerializeField]
    List<Transform> elevators;
    [SerializeField]
    List<Transform> rudders;
    [SerializeField]
    List<Transform> airbrakes;
    [SerializeField]
    List<LandingGearPart> gearParts;
    [SerializeField]
    List<LandingGearPart> coverParts;
    [SerializeField]
    List<BayDoorPart> bayDoorParts;
    // --- Restored Flap Animation ---
    [SerializeField]
    List<Transform> flaps;
    [SerializeField]
    float flapsDeflection;

    Plane plane;
    List<Transform> afterburnersTransforms;
    Dictionary<Transform, Quaternion> neutralPoses;
    Vector3 deflection;
    float airbrakePosition;
    float gearPosition;
    float bayDoorPosition;
    // --- Restored Flap Animation ---
    float flapsPosition;


    void Start() {
        plane = GetComponent<Plane>();
        afterburnersTransforms = new List<Transform>();
        neutralPoses = new Dictionary<Transform, Quaternion>();

        foreach (var go in afterburnerGraphics) { afterburnersTransforms.Add(go.GetComponent<Transform>()); }
        AddNeutralPose(leftAileron);
        AddNeutralPose(rightAileron);
        foreach (var t in elevators) { AddNeutralPose(t); }
        foreach (var t in rudders) { AddNeutralPose(t); }
        foreach (var t in airbrakes) { AddNeutralPose(t); }
        foreach (var part in gearParts) { AddNeutralPose(part.transform); }
        foreach (var part in coverParts) { AddNeutralPose(part.transform); }
        foreach (var part in bayDoorParts) { AddNeutralPose(part.transform); }
        // Add neutral poses for the new flaps
        foreach (var flap in flaps) { AddNeutralPose(flap); }

        if (plane.LandingGearDeployed) {
            gearPosition = 1f;
        }
    }

    void AddNeutralPose(Transform transform) {
        if (transform != null) {
            neutralPoses.Add(transform, transform.localRotation);
        }
    }

    Quaternion CalculatePose(Transform transform, Quaternion offset) {
        if (transform == null) return Quaternion.identity;
        return neutralPoses[transform] * offset;
    }

    void UpdateAfterburners() {
        float throttle = plane.Throttle;
        float afterburnerT = Mathf.Clamp01(Mathf.InverseLerp(afterburnerThreshold, 1, throttle));
        float size = Mathf.Lerp(afterburnerMinSize, afterburnerMaxSize, afterburnerT);
        if (throttle >= afterburnerThreshold) {
            for (int i = 0; i < afterburnerGraphics.Count; i++) {
                afterburnerGraphics[i].SetActive(true);
                afterburnersTransforms[i].localScale = new Vector3(size, size, size);
            }
        } else {
            for (int i = 0; i < afterburnerGraphics.Count; i++) {
                afterburnerGraphics[i].SetActive(false);
            }
        }
    }

    void UpdateControlSurfaces(float dt) {
        var input = plane.EffectiveInput;
        deflection.x = Utilities.MoveTo(deflection.x, input.x, deflectionSpeed, dt, -1, 1);
        deflection.y = Utilities.MoveTo(deflection.y, input.y, deflectionSpeed, dt, -1, 1);
        deflection.z = Utilities.MoveTo(deflection.z, input.z, deflectionSpeed, dt, -1, 1);
        if (rightAileron != null)
            rightAileron.localRotation = CalculatePose(rightAileron, Quaternion.Euler(deflection.z * maxAileronDeflection, 0, 0));
        if (leftAileron != null)
            leftAileron.localRotation = CalculatePose(leftAileron, Quaternion.Euler(-deflection.z * maxAileronDeflection, 0, 0));
        foreach (var t in elevators) {
            if (t != null)
                t.localRotation = CalculatePose(t, Quaternion.Euler(deflection.x * maxElevatorDeflection, 0, 0));
        }
        foreach (var t in rudders) {
            if (t != null)
                t.localRotation = CalculatePose(t, Quaternion.Euler(0, -deflection.y * maxRudderDeflection, 0));
        }
    }

    void UpdateAirbrakes(float dt) {
        var target = plane.AirbrakeDeployed ? 1 : 0;
        airbrakePosition = Utilities.MoveTo(airbrakePosition, target, deflectionSpeed, dt);
        foreach (var t in airbrakes)
        {
            if (t != null)
                t.localRotation = CalculatePose(t, Quaternion.Euler(-airbrakePosition * airbrakeDeflection, 0, 0));
        }
    }
    
    void UpdateLandingGear(float dt) {
        var target = plane.LandingGearDeployed ? 1 : 0;
        gearPosition = Utilities.MoveTo(gearPosition, target, deflectionSpeed, dt);
        foreach (var part in gearParts) {
            if (part.transform != null) {
                part.transform.localRotation = CalculatePose(part.transform, Quaternion.Euler((1 - gearPosition) * part.deflectionAngle, 0, 0));
            }
        }
        foreach (var part in coverParts) {
            if (part.transform != null) {
                part.transform.localRotation = CalculatePose(part.transform, Quaternion.Euler(gearPosition * part.deflectionAngle, 0, 0));
            }
        }
    }
    
    void UpdateBayDoors(float dt) {
        var target = plane.BayDoorsOpen ? 1 : 0;
        bayDoorPosition = Utilities.MoveTo(bayDoorPosition, target, deflectionSpeed, dt);
        foreach (var part in bayDoorParts) {
            if (part.transform != null) {
                part.transform.localRotation = CalculatePose(part.transform, Quaternion.Euler(bayDoorPosition * part.deflectionAngle, 0, 0));
            }
        }
    }

    // --- Restored UpdateFlaps Method ---
    void UpdateFlaps(float dt) {
        var target = plane.FlapsDeployed ? 1 : 0;
        flapsPosition = Utilities.MoveTo(flapsPosition, target, deflectionSpeed, dt);

        foreach (var flap in flaps) {
            if (flap != null)
                flap.localRotation = CalculatePose(flap, Quaternion.Euler(flapsPosition * flapsDeflection, 0, 0));
        }
    }

    void LateUpdate() {
        if (plane == null) return;
        float dt = Time.deltaTime;

        UpdateAfterburners();
        UpdateControlSurfaces(dt);
        UpdateAirbrakes(dt);
        UpdateLandingGear(dt);
        UpdateBayDoors(dt);
        UpdateFlaps(dt); // Call animation for new flaps
    }
}