using System.Collections;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class CollectData : MonoBehaviour
{
    private Rigidbody rb;
    private List<string> data;
    private int controlTimesteps;
    private int controlTimestepsAcc;
    private float fwdForce;
    private float rightForce;
    private float yawTorque;

    public float FwdMaxF = 500f;
    public float RightMaxF = 500f;
    public float YawMaxT= 500f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        data = new List<string>();
        controlTimesteps = Random.Range(40, 300);
        controlTimestepsAcc = 0;
        fwdForce = Random.Range(-FwdMaxF, FwdMaxF);
        rightForce = Random.Range(-RightMaxF, RightMaxF);
        yawTorque = Random.Range(-YawMaxT, YawMaxT);
    }

    void FixedUpdate()
    {
        if (controlTimestepsAcc > controlTimesteps){ // if the control has been exerted for the alloted timesteps Kt
            fwdForce = Random.Range(-FwdMaxF, FwdMaxF);
            rightForce = Random.Range(-RightMaxF, RightMaxF);
            yawTorque = Random.Range(-YawMaxT, YawMaxT);
            controlTimesteps = Random.Range(40, 300);
            controlTimestepsAcc = 0;
        }
        rb.AddRelativeForce(fwdForce*Vector3.forward + rightForce*Vector3.right);
        rb.AddRelativeTorque(yawTorque*Vector3.up);

        Vector3 position = rb.position;
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        Vector3 localAngularVelocity = rb.angularVelocity;
        Vector3 rpy = rb.rotation.eulerAngles;

        float yawDeg = rpy.y;
        float yawRad = Mathf.Deg2Rad * yawDeg; // Convert to radians
        yawRad = NormalizeYaw(yawRad);

        // Store data

        // using NED coordinate system, converting from unity. capturing 6DOF data. eta = [N, E, psi, u, v, r,]^T
        // [Timestamp, N (z), E (x), psi (y-axis rotation, left hand), u (Vz), v (Vx), r (Wy), F_N (F.z), F_E (F.x), T_psi (T.y)]. Enclosed in parentheses: Unity equivalent
        data.Add($"{Time.time}, {position.z}, {position.x}, {yawRad}, {localVelocity.z}, {localVelocity.x}, {localAngularVelocity.y}, {fwdForce}, {rightForce}, {yawTorque}");
        controlTimestepsAcc++;
    }

    void OnDisable()
    {
        SaveData();
    }

    void OnApplicationQuit()
    {
        SaveData();
    }

    private void SaveData()
    {
        // Path to the Assets folder in the Unity project
        string projectPath = Application.dataPath;
        // Create a directory called "Data" if it doesn't exist
        string dataDirectory = Path.Combine(projectPath, @"Scripts\sysid\Data");
        Directory.CreateDirectory(dataDirectory); // This is safe to call; it won't overwrite if the directory already exists

        // Combine the new path with the filename
        string filePath = Path.Combine(dataDirectory, "validation_data.csv");

        try
        {
            File.WriteAllLines(filePath, data);
            Debug.Log("Data saved to: " + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error saving data: " + ex.Message);
        }
    }
    
    float NormalizeYaw(float yaw)
    {
        while (yaw > Mathf.PI)
        {
            yaw -= 2 * Mathf.PI;
        }
        while (yaw <= -Mathf.PI)
        {
            yaw += 2 * Mathf.PI;
        }
        return yaw;
    }
}