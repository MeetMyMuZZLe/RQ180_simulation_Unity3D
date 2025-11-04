// IP_Drone_inputs.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IndiePixel
{
    [RequireComponent(typeof(PlayerInput))]
    public class IP_Drone_inputs : MonoBehaviour
    {
        #region Varaiables
        // --- MODIFIED ---
        // These are now public auto-properties with { get; set; }
        // This allows other scripts (like our AI) to set these values,
        // not just the PlayerInput component.
        public Vector2 Cyclic { get; set; }
        public float Pedals { get; set; }
        public float Throttle { get; set; }
        // --- END MODIFICATION ---
        #endregion

        #region Main Methods
        void Update()
        {
            
        }
        #endregion

        #region Input Methods
        // These methods are called by the PlayerInput component (when it's active)
        // They now set the public properties instead of private fields.
        private void OnCyclic(InputValue value)
        {
            Cyclic = value.Get<Vector2>(); // Set the public property
        }

        private void OnPedals(InputValue value)
        {
            Pedals = value.Get<float>(); // Set the public property
        }

        private void OnThrottle(InputValue value)
        {
            Throttle = value.Get<float>(); // Set the public property
        }
        #endregion
    }
}