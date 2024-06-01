using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;


public class Propulsion : MonoBehaviour
{
    ROSConnection ros;
    public GameObject engineJoint;
    public String topicName;
    public GameObject propellerJoint;
    private float angleSetpoint = 0.0f;
    private float angleCurr = 0.0f;

    [Range(0.00001f, 1.0f)]
    public float angleK = 0.005f;

    private float thrustCommand = 0.0f;
    [Range(0.001f, 1.0f)]
    public float thrustK = 1.0f;
    public float maxThrust = 250.0f;
    [Range(0.0f, 3000.0f)]
    public float maxRpmVisual = 500.0f;
    private float thrustCurr;
    private Rigidbody rb;


    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<Float32Msg>(topicName + "/thrust", thrustCallback);
        ROSConnection.GetOrCreateInstance().Subscribe<Float32Msg>(topicName + "/angle", angleCallback);

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
        //Debug.DrawRay(propellerJoint.transform.position, thrustForce/maxThrust);

        // propeller visual control
        float propAngleTurnRate = (thrustCurr/maxThrust)*(maxRpmVisual/60);
        Quaternion rotation = Quaternion.Euler(propAngleTurnRate*360.0f*Time.deltaTime, 0.0f, 0.0f);
        propellerJoint.transform.localRotation *= rotation;
    }

    void thrustCallback(Float32Msg msg){
        thrustCommand = Math.Clamp(msg.data, -1.0f, 1.0f);
    }
    void angleCallback(Float32Msg msg){
        angleSetpoint = msg.data;
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
