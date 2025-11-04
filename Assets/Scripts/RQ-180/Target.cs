// Target.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using HomingMissile;

// The enum is no longer needed here.

public class Target : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField]
    new string name;
    [SerializeField]
    public TargetClassDefinition targetClass; // CHANGED: Now uses the ScriptableObject

    // ... (The rest of your Target.cs script is unchanged)
    [Header("Health")]
    [SerializeField]
    private float maxHealth = 100f;
    [SerializeField] // --- MODIFIED: Added this attribute to make it visible ---
    private float currentHealth = 100f;

    [Header("Components")]
    [SerializeField]
    private List<GameObject> graphics;
    [SerializeField]
    private List<GameObject> damageEffects;
    [SerializeField]
    private List<GameObject> deathEffects;

    public string Name => name;
    public Vector3 Position => rigidbody.position;
    public Vector3 Velocity => rigidbody.linearVelocity;
    public Plane Plane { get; private set; }
    
    public bool IsAlive
    {
        get
        {
            if (Plane != null) return !Plane.Dead;
            return currentHealth > 0;
        }
    }

    new Rigidbody rigidbody;
    
    // --- MODIFIED: Changed list type from <homing_missile> to <Rigidbody> ---
    List<Rigidbody> incomingMissiles; 
    
    const float sortInterval = 0.5f;
    float sortTimer;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        Plane = GetComponent<Plane>();
        
        // --- MODIFIED: Changed list type from <homing_missile> to <Rigidbody> ---
        incomingMissiles = new List<Rigidbody>(); 
        
        currentHealth = maxHealth; // This line remains the same
        SetEffectsActive(damageEffects, false);
        SetEffectsActive(deathEffects, false);
    }

    void Update()
    {
        // --- MODIFICATION ---
        // We only want this logic to run if the target is alive.
        // If the target is dead, we want the effects set by
        // ApplyDamage() to persist without this method interfering.
        if (IsAlive)
        {
            if (currentHealth < maxHealth)
            {
                SetEffectsActive(damageEffects, true);
            }
            else
            {
                SetEffectsActive(damageEffects, false);
            }
        }
        // --- END MODIFICATION ---
    }
    
    public void ApplyDamage(float damage)
    {
        if (!IsAlive) return;

        bool wasAlive = currentHealth > 0;
        currentHealth -= damage;

        if (Plane != null)
        {
            Plane.ApplyDamage(damage);
        }
        else
        {
            if (currentHealth <= 0 && wasAlive)
            {
                currentHealth = 0;
                
                // --- MODIFICATION ---
                // This line was changed from 'false' to 'true'
                // Now, both damage (smoke) and death (explosion)
                // effects will be told to play when health reaches 0.
                SetEffectsActive(damageEffects, true);
                SetEffectsActive(deathEffects, true);
                // --- END MODIFICATION ---

                foreach(var graphic in graphics)
                {
                    graphic.SetActive(false);
                }
            }
        }
    }
    
    private void SetEffectsActive(List<GameObject> effects, bool isActive)
    {
        foreach (var effect in effects)
        {
            if (effect != null && effect.activeSelf != isActive)
            {
                effect.SetActive(isActive);
            }
        }
    }

    void FixedUpdate()
    {
        sortTimer = Mathf.Max(0, sortTimer - Time.fixedDeltaTime);
        if (sortTimer == 0)
        {
            SortIncomingMissiles();
            sortTimer = sortInterval;
        }
    }

    void SortIncomingMissiles()
        {
            if (incomingMissiles.Count > 0)
            {
                // First, remove any null (destroyed) missiles from the list
                incomingMissiles.RemoveAll(missile => missile == null);

                // Now, sort the remaining valid missiles
                incomingMissiles.Sort((a, b) =>
                {
                    // Add null checks just in case, though RemoveAll should have caught them
                    if (a == null && b == null) return 0;
                    if (a == null) return 1; // Put nulls at the end
                    if (b == null) return -1; // Keep valid missiles at the front

                    var distA = Vector3.Distance(a.position, Position);
                    var distB = Vector3.Distance(b.position, Position);
                    return distA.CompareTo(distB);
                });
            }
        }

    // --- MODIFIED: Changed return type from homing_missile to Rigidbody ---
    public Rigidbody GetIncomingMissile()
    {
        return incomingMissiles.Count > 0 ? incomingMissiles[0] : null;
    }

    // --- MODIFIED: Changed parameter type from homing_missile to Rigidbody ---
    public void NotifyMissileLaunched(Rigidbody missileRb, bool isIncoming)
    {
        if (isIncoming)
        {
            if (missileRb != null) // Added a null check just in case
            {
                incomingMissiles.Add(missileRb);
                SortIncomingMissiles();
            }
        }
        else
        {
            incomingMissiles.Remove(missileRb);
        }
    }
}