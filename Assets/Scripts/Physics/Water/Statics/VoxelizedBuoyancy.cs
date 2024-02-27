using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Physics.Water.Dynamics;
using UnityEditor.Playables;
using System.Collections;
using WaterInteraction;
using UnityEngine;
using System.IO;
using System;
using TMPro;


//TODO: Investigate bug where the smaller voxels cause the ship to lay lower in the water
// It obviously has something to do with the amount of points, and difference between that is not accounted for
// there seems to be about a .5 or .4 difference in mass to maintain the same waterline,
// from 6 voxels to 3 voxels (15000 to 8500)
// TODO: Consider factoring in Depth of the point to buoyancy force
// TODO: Find Voxels at waterling, i.e. height is less than radius +- above the water


public class VoxelizedBuoyancy : MonoBehaviour
{
    [Tooltip("Leave empty to toggle Plane buoyancy off.")]
    public Transform planeTransform;
    
    public TextMeshProUGUI buoyancyText;
    
    // Water querying variables
    [Tooltip("Set to zero to toggle water patch off.")]
    public float patchSize = 10;
    public int patchResolution = 4;
    public WaterSurface targetSurface = null;
    public bool drawPatch;
    
    public bool logData;
    
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    //private string pathToVoxelFile = "Assets/Data/localPointsData.json";
    private string path = "Assets/Data/";
    private List<Vector3> globalVoxelPositions = new List<Vector3>();
    //private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    
    private float actualForce;
    private float voxelVolume;
    
    private Transform parentTransform;
    private Patch patch;
    private Rigidbody shipRigidbody;
    
    
    void Awake()
    {
        // Populate local list and int with saved information
        Vector3ListWrapper wrapper = LoadPoints();
        pointsInsideMesh = wrapper.localPoints;
        voxelVolume = wrapper.volume;
        shipRigidbody = GetComponent<Rigidbody>();
    }


    private void Start() // Water patch setup
    {
        if (patchSize != 0)
        {
            Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
            patch = new Patch(targetSurface, patchSize, patchResolution, gridOrigin);
            patch.Update(transform);
        }
        
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
        if (planeTransform) DoPlaneBuoyancy();
        if (patchSize != 0) DoPatchBuoyancy();
        
        //buoyancyText.text = "Buoyancy Force: " + actualForce.ToString("F2");
    }


    private void DoPlaneBuoyancy()
    {
        //ApplyForce(CalculateCenterOfPoints(GetPointsUnderPlane()));
        float displacedVolume = TestVolume(CalculateCenterOfPoints(GetPointsUnderPlane()));
        string localPath = path + "VolumeData-" + transform.name + ".csv";
        if (logData)
        {
            print("Logging data to file.");
            Utils.LogDataToFile(localPath, -(transform.position.y - 0.5f), displacedVolume);
        }
    }
    
    
    private void DoPatchBuoyancy()
    {
        patch.Update(transform);
        ApplyForce(CalculateCenterOfPoints(GetPointsUnderWaterPatch()));
        List<Vector3> debugPoints = GetPointsUnderWaterPatch();
        foreach (var point in debugPoints)
        {
            Debug.DrawRay(point, Vector3.up, Color.red);
        }
        
        if (drawPatch) Utils.DrawPatch(patch);
    }
    
    
    /// Returns the list of points under the Plane.
    private List<Vector3> GetPointsUnderPlane()
    {
        // Create a Plane object using the position and up vector of the planeTransform
        Plane plane = new Plane(planeTransform.up, planeTransform.position);
        
        List<Vector3> pointsUnderPlane = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            if (!plane.GetSide(point)) pointsUnderPlane.Add(point);
        }

        return pointsUnderPlane;
    }
    
    
    /// Returns the list of points under the water patch.
    public List<Vector3> GetPointsUnderWaterPatch()
    {
        List<Vector3> pointsUnderPatch = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            if (patch.GetPatchRelativeHeight(point) < 0.00001f) pointsUnderPatch.Add(point);
        }

        return pointsUnderPatch;
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


    private void ApplyForce((Vector3 centerOfBuoyancy, int numberOfPoints) data)
    {
        float force = CalculateForce(data.numberOfPoints, voxelVolume);
        shipRigidbody.AddForceAtPosition(force*Vector3.up, data.centerOfBuoyancy);
        Debug.DrawRay(data.centerOfBuoyancy,force*Vector3.up, Color.red);
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
        string json = File.ReadAllText(path + "localPointsData.json");
        return JsonUtility.FromJson<Vector3ListWrapper>(json);
    }
    
    
    void ApplyHydrodynamicDampening() 
    {
        //foreach (var point in submergedPoints) 
        {
            //Vector3 velocity = GetPointVelocity(point); // Implement this method based on your system
            //float area = CalculateCrossSectionalArea(point); // Implement based on voxel size or shape
            //Vector3 dragForce = CalculateDragForce(shipRigidbody.velocity, area, dragCoefficient);
            //ApplyDragForce((point, dragForce)); // You'll need to implement how forces are applied in your system
        }
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
            //Gizmos.DrawSphere(point, 1); 
        }
    }
}
