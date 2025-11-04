// homing_missile.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HomingMissile
{
    public enum MissileType { Standard, Heavy, Fast }

    public class homing_missile : MonoBehaviour
    {
        [Header("Missile Identity")]
        public MissileType missileType;

        [Header("Basic Settings")]
        public int speed = 60;
        public int downspeed = 30;
        public int damage = 50;
        public bool fully_active = false;
        public int timebeforeactivition = 20;
        public int timebeforebursting = 40;
        public int timebeforedestruction = 450;
        public int timealive;
        public GameObject target;
        public GameObject shooter;
        public Rigidbody projectilerb;
        public bool isactive = false;
        public Vector3 sleepposition;
        
        [Header("Audio & Effects")]
        public AudioSource launch_sound;
        public AudioSource thrust_sound;
        public GameObject smoke_obj;
        public ParticleSystem smoke;
        public GameObject smoke_position;
        public GameObject destroy_effect;
        
        [Header("Tracking")]
        [SerializeField] private float rotateSpeed = 95f;
        [SerializeField] private float maxDistancePredict = 100f;
        [SerializeField] private float minDistancePredict = 5f;
        [SerializeField] private float maxTimePrediction = 5f;
        private Vector3 standardPrediction, deviatedPrediction;
        
        [Header("Deviation")]
        [SerializeField] private float deviationAmount = 50f;
        [SerializeField] private float deviationSpeed = 2f;

        private Rigidbody targetRb;

        private void Start()
        {
            projectilerb = this.GetComponent<Rigidbody>();
        }

        public void call_destroy_effects()
        {
            if (destroy_effect != null)
            {
                Instantiate(destroy_effect, transform.position, transform.rotation);
            }
        }

        public void setmissile()
        {
            timealive = 0;
            transform.Rotate(0, 0, 0);
            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
            }
        }

        public void DestroyMe()
        {
            if (target != null)
            {
                Target targetComponent = target.GetComponent<Target>();
                if (targetComponent != null)
                {
                    targetComponent.NotifyMissileLaunched(this.projectilerb, false);
                }
            }

            isactive = false;
            fully_active = false;
            timealive = 0;

            if (smoke != null)
            {
                smoke.transform.SetParent(null);
                smoke.Stop();
                Destroy(smoke.gameObject, 5f);
            }

            if (projectilerb != null)
            {
                projectilerb.linearVelocity = Vector3.zero;
                projectilerb.angularVelocity = Vector3.zero;
            }
            
            if (thrust_sound != null)
            {
                thrust_sound.Stop();
            }

            call_destroy_effects();
            Destroy(this.gameObject);
        }

        // --- METHOD REPLACED ---
        public void usemissile(Vector3 initialVelocity) // CHANGED: Added parameter
        {
            if (launch_sound != null)
            {
                launch_sound.Play();
            }

            if (projectilerb != null)
            {
                projectilerb.isKinematic = false;
                projectilerb.useGravity = true;
                projectilerb.linearVelocity = initialVelocity; // ADDED: Set initial velocity
            }

            isactive = true;
            setmissile();

            if (target != null)
            {
                Target targetComponent = target.GetComponent<Target>();
                if (targetComponent != null)
                {
                    targetComponent.NotifyMissileLaunched(this.projectilerb, true);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isactive) return;

            Rigidbody targetRigidbody = other.attachedRigidbody;
            if (targetRigidbody == null) return;

            Target targetComponent = targetRigidbody.GetComponent<Target>();

            if (targetComponent != null)
            {
                if (targetComponent.gameObject == shooter)
                {
                    if (fully_active)
                    {
                        targetComponent.ApplyDamage(damage);
                        DestroyMe();
                    }
                }
                else
                {
                    targetComponent.ApplyDamage(damage);
                    DestroyMe();
                }
            }
        }

        void FixedUpdate()
        {
            if (!isactive) return;

            if (target == null || !target.activeInHierarchy)
            {
                DestroyMe();
                return;
            }

            timealive++;

            if (timealive == timebeforeactivition)
            {
                fully_active = true;
                if (thrust_sound != null) thrust_sound.Play();
            }

            if (timealive < timebeforebursting)
            {
                return;
            }

            if (timealive == timebeforebursting)
            {
                if (projectilerb != null)
                {
                    projectilerb.useGravity = false;
                }

                if (smoke_obj != null && smoke_position != null)
                {
                    GameObject smokeInstance = Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation);
                    smokeInstance.transform.SetParent(this.transform);
                    smoke = smokeInstance.GetComponent<ParticleSystem>();
                    if (smoke != null) smoke.Play();
                }
            }

            if (timealive >= timebeforedestruction)
            {
                DestroyMe();
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            float leadTimePercentage = Mathf.InverseLerp(minDistancePredict, maxDistancePredict, distanceToTarget);

            PredictMovement(leadTimePercentage);
            AddDeviation(leadTimePercentage);
            RotateTowardsTarget();

            projectilerb.linearVelocity = transform.forward * speed;
        }

        private void PredictMovement(float leadTimePercentage)
        {
            float predictionTime = Mathf.Lerp(0, maxTimePrediction, leadTimePercentage);
            if (targetRb != null)
            {
                standardPrediction = targetRb.position + targetRb.linearVelocity * predictionTime;
            }
            else
            {
                standardPrediction = target.transform.position;
            }
        }

        private void AddDeviation(float leadTimePercentage)
        {
            Vector3 deviation = new Vector3(Mathf.Cos(Time.time * deviationSpeed), 0, 0);
            Vector3 predictionOffset = transform.TransformDirection(deviation) * deviationAmount * leadTimePercentage;
            deviatedPrediction = standardPrediction + predictionOffset;
        }

        private void RotateTowardsTarget()
        {
            Vector3 heading = deviatedPrediction - transform.position;
            if (heading != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(heading);
                projectilerb.MoveRotation(Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                ));
            }
        }
    }
}