// BaseMissile.cs
using UnityEngine;

// This is an 'abstract' class, meaning it can't be used by itself.
// It MUST be inherited by another script (like Maverick, Brimstone, etc.)
public abstract class BaseMissile : MonoBehaviour
{
    // 1. Common fields the AdvancedMissileController needs.
    [Header("Base Missile Settings")]
    public GameObject target;
    public GameObject shooter;

    // 2. The 'abstract' launch method.
    // This tells C# that any script inheriting from BaseMissile
    // MUST provide its own 'usemissile' method.
    public abstract void usemissile(Vector3 initialVelocity);
}