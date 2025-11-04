// // HealthController.cs
// using UnityEngine;

// public class HealthController : MonoBehaviour
// {
//     [SerializeField]
//     private float maxHealth = 100f;
//     private float currentHealth;

//     // A reference to the Plane script, if this is on a plane
//     private Plane plane; 

//     void Awake()
//     {
//         currentHealth = maxHealth;
//         // Check if this object is also a plane
//         plane = GetComponent<Plane>(); 
//     }

//     public void ApplyDamage(float damage)
//     {
//         currentHealth -= damage;

//         // If this is a plane, we let the Plane script handle its own health logic
//         // for damage effects and its death sequence.
//         if (plane != null)
//         {
//             plane.ApplyDamage(damage);
//         }
//         else // If it's not a plane, we handle its destruction here
//         {
//             if (currentHealth <= 0)
//             {
//                 // Simple destruction for non-plane objects
//                 Destroy(gameObject); 
//             }
//         }
//     }
// }