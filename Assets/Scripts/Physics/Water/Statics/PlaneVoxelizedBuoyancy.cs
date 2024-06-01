using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Physics.Water.Dynamics;
using System.Collections;
using WaterInteraction;
using UnityEngine;
using System.IO;
using System;
using TMPro;

// This script is aimed at testing the buoyancy of a ship using a voxelized mesh
// It does not support a dynamic water surface, but rather creates a plane 
// based on input transforms. 


public class PlaneVoxelizedBuoyancy : MonoBehaviour
{
    [Tooltip("Any GameObject transform can be used here.")]
    public Transform planeTransform;
    
    public TextMeshProUGUI buoyancyText;
    
    public bool logData;

    private Plane plane = new Plane(Vector3.up, Vector3.zero);
    private List<Vector3> globalVoxelPositions = new List<Vector3>();
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    private string path = "Assets/Data/";
    private Vector3 parentPosition;

    private float actualForce;
    private float voxelVolume;
    private float voxelRadius;

    private Transform parentTransform;
    private Rigidbody shipRigidbody;


    void Awake()
    {
        // Populate local list and int with saved information
        Vector3ListWrapper wrapper = LoadPoints();
        pointsInsideMesh = wrapper.localPoints;
        voxelVolume = wrapper.volume;
        voxelRadius = wrapper.radius;
        shipRigidbody = GetComponent<Rigidbody>();
    }


    private void Start()
    {
        string localPath = path + "VolumeData-" + transform.name + ".csv";
        if (!File.Exists(localPath) && logData)
        {
            print("Beginning to log data");
            Utils.LogDataToFile(localPath,"depth","volume");
        }
        if(logData) GetComponent<Rigidbody>().velocity = new Vector3(0f, -0.1f, 0f);
    }

    
    private void FixedUpdate()
    {
        UpdateGlobalVoxelPosition();
        if (planeTransform)
            plane = new Plane(planeTransform.up, planeTransform.position);
        DoPlaneBuoyancy();
    }


    private void DoPlaneBuoyancy()
    {
        CalculateAndApplyForce(CalculateCenterOfPoints(GetPointsUnderPlane()));
        float displacedVolume = TestVolume(CalculateCenterOfPoints(GetPointsUnderPlane()));
        string localPath = path + "VolumeData-" + transform.name + ".csv";
        if (logData)
        {
            print("Logging data to file.");
            Utils.LogDataToFile(localPath, -(transform.position.y - 0.5f), displacedVolume);
        }
        buoyancyText.text = "Buoyancy Force: " + actualForce.ToString("F2");

    }
    
    
    /// Returns the list of points under the Plane.
    private List<Vector3> GetPointsUnderPlane()
    {
        List<Vector3> pointsUnderPlane = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            if (!plane.GetSide(point)) pointsUnderPlane.Add(point);
        }

        return pointsUnderPlane;
    }
    
    
    private (Vector3, int) CalculateCenterOfPoints(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var point in points)
        {
            sum += point;
        }
        return (sum / points.Count, points.Count);
    }

    
    private void CalculateAndApplyForce((Vector3 centerOfBuoyancy, int numberOfPoints) data)
    {
        float force = CalculateForce(data.numberOfPoints, voxelVolume);
        shipRigidbody.AddForceAtPosition(force*Vector3.up, data.centerOfBuoyancy);
        //Debug.DrawRay(data.centerOfBuoyancy,force*Vector3.up, Color.red);
    }
    
    
    private float CalculateForce(int numberOfPoints, float volume)
    {
        return actualForce = Constants.waterDensity * numberOfPoints * volume * Constants.gravity;
    }
    
    
    private void UpdateGlobalVoxelPosition()
    {
        if (!transform.hasChanged) return;
        globalVoxelPositions.Clear();
        foreach (Vector3 point in pointsInsideMesh)
        {
            globalVoxelPositions.Add(transform.TransformPoint(point));
        }
        // Reset the hasChanged flag
        transform.hasChanged = false;
    }
    
    
    private Vector3ListWrapper LoadPoints()
    {
        string json = File.ReadAllText(path + "localPointsData-" + transform.name + ".json");
        return JsonUtility.FromJson<Vector3ListWrapper>(json);
    }
    
    
    private float TestVolume((Vector3 centerOfBuoyancy, int numberOfPoints) data)
    {// Total volume submerged
        return data.numberOfPoints * voxelVolume;
    }
    
    
    private void OnDrawGizmos()
    {
        if (globalVoxelPositions.Count == 0) return;
        
        Gizmos.color = Color.magenta;
        foreach (Vector3 point in globalVoxelPositions)
        {
            Gizmos.DrawSphere(point, 0.25f*voxelRadius); 
        }
    }
}
