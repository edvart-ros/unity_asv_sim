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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        data = new List<string>();
        controlTimesteps = Random.Range(40, 300);
        controlTimestepsAcc = 0;
        fwdForce = Random.Range(-500f, 500f);
        rightForce = Random.Range(-500f, 500f);
        yawTorque = Random.Range(-500f, 500f);
    }

    void FixedUpdate()
    {
        if (controlTimestepsAcc > controlTimesteps){ // if the control has been exerted for the alloted timesteps Kt
            fwdForce = Random.Range(-500f, 500f);
            rightForce = Random.Range(-500f, 500f);
            yawTorque = Random.Range(-500f, 500f);
            controlTimesteps = Random.Range(40, 300);
            controlTimestepsAcc = 0;
        }
        rb.AddRelativeForce(fwdForce*Vector3.forward + rightForce*Vector3.right);
        rb.AddRelativeTorque(yawTorque*-Vector3.up);

        Vector3 position = rb.position;
        Vector3 localVelocity = rb.velocity;
        Vector3 localAngularVelocity = rb.angularVelocity;
        Vector3 rpy = rb.rotation.eulerAngles;

        // Store data

        // using NED coordinate system, converting from unity. capturing 6DOF data. eta = [N, E, psi, u, v, r,]^T
        // [Timestamp, N (z), E (x), psi (y-axis rotation, left hand), u (Vz), v (Vx), r (Wy), F_N (F.z), F_E (F.x), T_psi (T.y)]. Enclosed in parentheses: Unity equivalent
        data.Add($"{Time.time}, {position.z}, {position.x}, {rpy.y}, {localVelocity.z}, {localVelocity.x}, {-localAngularVelocity.y}, {fwdForce}, {rightForce}, {yawTorque}");
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
        string filePath = Path.Combine(dataDirectory, "data.csv");

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
}