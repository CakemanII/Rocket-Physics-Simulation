using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Rocket : MonoBehaviour
{
    private float[,] thrustCurve = new float[,]
    {
        {0.148f, 7.638f},
        {0.228f, 12.253f},
        {0.294f, 16.391f},
        {0.353f, 20.21f},
        {0.382f, 22.756f},
        {0.419f, 25.26f},
        {0.477f, 23.074f},
        {0.52f, 20.845f},
        {0.593f, 19.093f},
        {0.688f, 17.5f},
        {0.855f, 16.225f},
        {1.037f, 15.427f},
        {1.205f, 14.948f},
        {1.423f, 14.627f},
        {1.452f, 15.741f},
        {1.503f, 14.785f},
        {1.736f, 14.623f},
        {1.955f, 14.303f},
        {2.21f, 14.141f},
        {2.494f, 13.819f},
        {2.763f, 13.338f},
        {3.12f, 13.334f},
        {3.382f, 13.013f},
        {3.404f, 9.352f},
        {3.418f, 4.895f},
        {3.45f, 0f}
    };

    [SerializeField] Rigidbody rocketRigidbody;

    [Header("Rocket Settings")]
    [SerializeField] bool instantLaunch = false;

    [SerializeField] private int motorAmount = 5;

    [SerializeField] private float launchAngularAcceleration = 0f;
    [SerializeField] private float airResistanceCoefficient = 0.5f; 
    
    [SerializeField] private float parachuteDragCoefficient = 1.75f;
    [SerializeField] private float parachuteDeployVelocity = 0f;
    [Tooltip("Velocity at which the parachute will deploy (negative value for downward velocity).")]

    private bool launchSequenceInitiated = false;
    private float launchDelay;
    private float timeSinceLaunchInitiated = 0f;

    private bool parachuteDeployed = false; 

    private Vector3 previousVelocity;
    private Vector3 acceleration;

    // Update is called once per frame
    void Update()
    {
        // Check for launch inputs
        if (!launchSequenceInitiated)
        {
            LaunchInput();
        }
    }

    void FixedUpdate()
    {
        // Calculate current acceleration
        calculateAcceleration();

        // Apply Air Resistance Force
        // force = 0.5 * airDensity * dragCoefficient * area(NEGLIGIBLE) * velocity^2
        Vector3 airResistanceForce = 0.1f * -rocketRigidbody.linearVelocity.normalized * 0.5f * 1.225f * airResistanceCoefficient * rocketRigidbody.linearVelocity.sqrMagnitude;
        rocketRigidbody.AddForce(airResistanceForce);

        // Do not continue if launch sequence not initiated
        if (!launchSequenceInitiated)
            return;

        // Add to launch time
        timeSinceLaunchInitiated += Time.fixedDeltaTime;

        // Get current time since launch sequence finished
        float timeSinceLaunch = timeSinceLaunchInitiated - launchDelay;
        if (timeSinceLaunch < 0f)
            return;

        // Apply angular acceleration
        if (launchAngularAcceleration != 0f)
        {
            rocketRigidbody.angularVelocity += transform.up * launchAngularAcceleration * Time.fixedDeltaTime;
        }

        // Get thrust from thrust curve
        float thrust = GetThrustFromCurve(timeSinceLaunch);

        // Apply thrust force
        rocketRigidbody.AddForce(transform.up * thrust, ForceMode.Force);

        // Apply parachute drag if applicable
        if (parachuteDeployed)
        {
            Vector3 v = rocketRigidbody.linearVelocity;
            if (v.magnitude < 0.1f) return;
            float airDensity = 1.155f; // kg/m^3 at 700m altitude

            Vector3 drag = -v.normalized * 0.5f * airDensity * parachuteDragCoefficient * 10 * v.sqrMagnitude;
            rocketRigidbody.AddForce(drag);
        }
        else
        {
            // Determine if the parachute should be deployed
            if (CanParachuteDeploy())
            {
                parachuteDeployed = true;
                Debug.Log("Parachute deployed!");
            }
        }
    }

    private bool CanParachuteDeploy()
    {
        return rocketRigidbody.linearVelocity.y <= parachuteDeployVelocity && 
        acceleration.y < 0f &&
        timeSinceLaunchInitiated > 3f &&
        !parachuteDeployed;
    }

    private void calculateAcceleration()
    {
        acceleration = new Vector3(
            calculateDerivative(rocketRigidbody.linearVelocity.x, previousVelocity.x, Time.fixedDeltaTime),
            calculateDerivative(rocketRigidbody.linearVelocity.y, previousVelocity.y, Time.fixedDeltaTime),
            calculateDerivative(rocketRigidbody.linearVelocity.z, previousVelocity.z, Time.fixedDeltaTime)
        );

        previousVelocity = rocketRigidbody.linearVelocity;
    }

    private float calculateDerivative(float currentValue, float previousValue, float deltaTime)
    {
        // Placeholder for future derivative calculations if needed
        return (currentValue - previousValue) / deltaTime;
    }

    private float GetThrustFromCurve(float timeSinceLaunch)
    {
        // If timeSinceLaunch is beyond the last entry, return 0
        if (timeSinceLaunch >= thrustCurve[thrustCurve.GetLength(0) - 1, 0])
            return 0f;

        // Find the appropriate segment in the thrust curve
        for (int i = 0; i < thrustCurve.GetLength(0) - 1; i++)
        {
            if (timeSinceLaunch >= thrustCurve[i, 0] && timeSinceLaunch < thrustCurve[i + 1, 0])
            {
                // Linear interpolation between the two points
                float t = (timeSinceLaunch - thrustCurve[i, 0]) / (thrustCurve[i + 1, 0] - thrustCurve[i, 0]);
                return Mathf.Lerp(thrustCurve[i, 1], thrustCurve[i + 1, 1], t) * motorAmount;
            }
        }

        return 0f; // Default return value
    }

    private void LaunchInput() {
        // Space = 0
        if (Input.GetKeyDown(KeyCode.Space) || instantLaunch)
            initiateLaunchSequence(0f);
        else if (Input.GetKeyDown(KeyCode.Alpha1))
            initiateLaunchSequence(1f);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            initiateLaunchSequence(2f);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            initiateLaunchSequence(3f);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            initiateLaunchSequence(4f);
        else if (Input.GetKeyDown(KeyCode.Alpha5))
            initiateLaunchSequence(5f);
        else if (Input.GetKeyDown(KeyCode.Alpha6))
            initiateLaunchSequence(6f);
        else if (Input.GetKeyDown(KeyCode.Alpha7))
            initiateLaunchSequence(7f);
        else if (Input.GetKeyDown(KeyCode.Alpha8))
            initiateLaunchSequence(8f);
        else if (Input.GetKeyDown(KeyCode.Alpha9))
            initiateLaunchSequence(9f);
    }

    private void initiateLaunchSequence(float delay)
    {
        Debug.Log("Launch sequence initiated with delay: " + delay + " seconds.");
        launchSequenceInitiated = true;
        launchDelay = delay;
    }

    #region Getters
    public Vector3 Velocity() => rocketRigidbody.linearVelocity;
    public Vector3 Acceleration() => acceleration;
    public float CurrentAltitude() => transform.position.y;

    public Vector3 AngularVelocity() => rocketRigidbody.angularVelocity;
    public Vector3 AngularPosition() => transform.eulerAngles;

    public bool IsParachuteDeployed() => parachuteDeployed;    

    public float CurrentThrust()
    {
        float timeSinceLaunch = timeSinceLaunchInitiated - launchDelay;
        if (timeSinceLaunch < 0f) return 0f;
        return GetThrustFromCurve(timeSinceLaunch);
    }

    public float TimeSinceInitiated() => timeSinceLaunchInitiated;
    #endregion
}
