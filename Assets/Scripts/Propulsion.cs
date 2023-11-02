using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Propulsion : MonoBehaviour
{
    public GameObject engineJoint;
    public GameObject propellerJoint;
    [Range(-90.0f, 90.0f)]
    public float angleSetpoint = 0.0f;
    private float angleCurr = 0.0f;

    [Range(0.00001f, 1.0f)]
    public float angleK = 0.005f;

    [Range(-1.0f, 1.0f)]
    public float thrustCommand = 0.0f;
    [Range(0.001f, 1.0f)]
    public float thrustK = 1.0f;
    public float maxThrust = 250.0f;
    [Range(0.0f, 3000.0f)]
    public float maxRpmVisual = 500.0f;
    private float thrustCurr;
    private Rigidbody rb;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        thrustCurr = 0.0f;
        angleCurr = NormalizeAngle(engineJoint.transform.rotation.eulerAngles.y);
    }

    void Update()
    {
        // engine angle control
        float angleError = AngleDifference(angleSetpoint, angleCurr);
        float angleCorrection = angleK * angleError;
        engineJoint.transform.localEulerAngles = new Vector3(0.0f, angleCurr + angleCorrection, 0.0f);;
        angleCurr = NormalizeAngle(engineJoint.transform.localEulerAngles.y);


        // force control
        float thrustSetpoint = thrustCommand*maxThrust;
        float thrustError = thrustSetpoint - thrustCurr;
        thrustCurr += Mathf.Clamp(thrustError*thrustK, -maxThrust, maxThrust);
        Vector3 thrustDirLocal = new Vector3(Mathf.Sin(Mathf.Deg2Rad*angleCurr), 0.0f, Mathf.Cos(Mathf.Deg2Rad*angleCurr));
        Vector3 thrustDir = transform.TransformDirection(thrustDirLocal);
        Vector3 thrustForce = thrustCurr*thrustDir;
        rb.AddForceAtPosition(thrustForce, propellerJoint.transform.position);
        Debug.DrawRay(propellerJoint.transform.position, thrustForce/maxThrust);

        // propeller visual control
        float propAngleTurnRate = (thrustCurr/maxThrust)*(maxRpmVisual/60);
        Quaternion rotation = Quaternion.Euler(propAngleTurnRate*360.0f*Time.deltaTime, 0.0f, 0.0f);
        propellerJoint.transform.localRotation *= rotation;
    }

    // This function normalizes an angle to [0, 360) degrees
    float NormalizeAngle(float angle)
    {
        while (angle < 0.0f) angle += 360.0f;
        while (angle >= 360.0f) angle -= 360.0f;
        return angle;
    }

    // Computes the shortest difference between two angles
    float AngleDifference(float a, float b)
    {
        float difference = NormalizeAngle(a - b);
        if (difference > 180.0f)
            difference -= 360.0f;
        return difference;
    }
}
