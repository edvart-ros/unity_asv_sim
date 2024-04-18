using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Physics.Water.Dynamics;
using UnityEditor.Playables;
using System.Collections;
using System.Diagnostics;
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
// TODO: Find Voxels at waterline, i.e. height is less than radius +- above the water


public class PatchVoxelizedBuoyancy : MonoBehaviour
{
    public TextMeshProUGUI buoyancyText;
    
    // Water querying variables
    [Tooltip("Set to zero to toggle water patch off.")]
    public float patchSize = 10;
    public int patchResolution = 4;
    public WaterSurface targetSurface = null;
    public bool CutTheVoxels;
    
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    //private string pathToVoxelFile = "Assets/Data/localPointsData.json";
    private string path = "Assets/Data/Voxels/";
    private List<Vector3> globalVoxelPositions = new List<Vector3>();
    //private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    
    private float actualForce;
    private float voxelVolume;
    private float voxelRadius;
    
    private Transform parentTransform;
    private Patch patch;
    private Rigidbody shipRigidbody;
    
    // Test frameworks
    public bool drawPatch;
    public bool drawVoxelPositionGizmo;
    public bool logVolumeData;
    public bool logTimeData;
    
    private string depthLogFile;
    private string timeLogFile;
    private int iteration;
    private Stopwatch stopwatch = new Stopwatch();
    
    
    void Awake()
    {
        Vector3ListWrapper wrapper = LoadPoints();
        pointsInsideMesh = wrapper.localPoints;
        voxelVolume = wrapper.volume;
        voxelRadius = wrapper.radius;
        shipRigidbody = GetComponent<Rigidbody>();
    }


    private void Start()
    {
        if (patchSize == 0) return;
        
        Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
        patch = new Patch(targetSurface, patchSize, patchResolution, gridOrigin);
        patch.Update(transform);
        
        iteration = 0;
        
        InitializeLogs();
    }

    
    private void FixedUpdate()
    {
        if (patchSize == 0) return;
        stopwatch.Start();
        
        UpdateGlobalVoxelPosition();
        patch.Update(transform);
        if (CutTheVoxels) ApplyForce(GetDetailedPointsUnderWaterPatch());
        else ApplyForce(GetSimplePointsUnderWaterPatch());
        
        stopwatch.Stop();

        if (logTimeData && iteration < 100)
        {
            Utils.LogDataToFile(timeLogFile, iteration++, stopwatch.Elapsed.TotalMilliseconds * 1000.0);
            //print("Time for iteraton " + iteration + " is " + stopwatch.Elapsed.TotalMilliseconds * 1000.0 + " microseconds.");
            //iteration++;
        }
        if (drawPatch) Utils.DrawPatch(patch);
        //buoyancyText.text = "Buoyancy Force: " + actualForce.ToString("F2");
        stopwatch.Reset();
    }
    
    
    /// Cuts the voxels at the waterline. Results in less discontinutity in the submerged volume
    public (Vector3, float) GetDetailedPointsUnderWaterPatch()
    {
        float semiSubmergedVolume = 0.0f;
        float baseSurface = 4*voxelRadius*voxelRadius;
        Vector3 sumOfPositions = Vector3.zero;
        int numberOfPoints = 0;
        
        //List<Vector3> pointsUnderPatch = new List<Vector3>();
        List<Vector3> pointsFullySubmerged = new List<Vector3>();
        List<Vector3> pointsAtSurface = new List<Vector3>();
        //List<Vector3> pointsAtSurfaceOverPatch = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            float heightOfPoint = patch.GetPatchRelativeHeight(point);
            if (heightOfPoint < 0.00001f - voxelRadius)
            {
                pointsFullySubmerged.Add(point);
                sumOfPositions += point;
                numberOfPoints++;
            }
            else if (heightOfPoint > -voxelRadius && heightOfPoint < voxelRadius)
            {
                pointsAtSurface.Add(point);
                semiSubmergedVolume += baseSurface * (voxelRadius - heightOfPoint);
                sumOfPositions += point;
                numberOfPoints++;
            }
        }
        
        float fullySubmergedVolume = pointsFullySubmerged.Count * voxelVolume;
        float totalVolume = semiSubmergedVolume + fullySubmergedVolume;
        
        if (logVolumeData && iteration < 3)
        {
            print("Logging data to file.");
            Utils.LogDataToFile(depthLogFile, -(transform.position.y - 0.5f), totalVolume);
        }
        
        float force = CalculateForceFromVolumeOnly(totalVolume);
        Vector3 centerOfBuoyancy = sumOfPositions / numberOfPoints;
        return (centerOfBuoyancy, force);
    }
    
    
    /// Does not cutting of the voxels
    public (Vector3, float) GetSimplePointsUnderWaterPatch()
    {
        float semiSubmergedVolume = 0.0f;
        float baseSurface = 4*voxelRadius*voxelRadius;
        Vector3 sumOfPositions = Vector3.zero;
        int numberOfPoints = 0;
        
        List<Vector3> pointsSubmerged = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            float heightOfPoint = patch.GetPatchRelativeHeight(point);
            if (heightOfPoint < 0.00001f)
            {
                pointsSubmerged.Add(point);
                sumOfPositions += point;
                numberOfPoints++;
            }
        }
        
        float totalVolume = pointsSubmerged.Count * voxelVolume;
        
        if (logVolumeData)
        {
            print("Logging data to file.");
            Utils.LogDataToFile(depthLogFile, -(transform.position.y - 0.5f), totalVolume);
        }
        
        float force = CalculateForceFromVolumeOnly(totalVolume);
        Vector3 centerOfBuoyancy = sumOfPositions / numberOfPoints;
        return (centerOfBuoyancy, force);
    }
    
    
    private void ApplyForce((Vector3 centerOfBuoyancy, float force) data)
    {
        if(!logVolumeData) shipRigidbody.AddForceAtPosition(data.force*Vector3.up, data.centerOfBuoyancy);
        UnityEngine.Debug.DrawRay(data.centerOfBuoyancy,data.force*Vector3.up, Color.red);
    }
    
    
    private float CalculateForceFromVolumeOnly(float volume)
    {
        return actualForce = Constants.waterDensity * volume * Constants.gravity;
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
    
    
    public (List<Vector3>,List<Vector3>,List<Vector3>) DevelopPointLists()
    {
        List<Vector3> pointsFullySubmerged = new List<Vector3>();
        List<Vector3> pointsAtSurfaceUnderPatch = new List<Vector3>();
        List<Vector3> pointsAtSurfaceOverPatch = new List<Vector3>();

        foreach (var point in globalVoxelPositions)
        {
            float heightOfPoint = patch.GetPatchRelativeHeight(point);
            if (heightOfPoint < 0.00001f - voxelRadius)
            {
                pointsFullySubmerged.Add(point);
            }
            else if (heightOfPoint < 0.00001f && heightOfPoint > -voxelRadius)
            {
                pointsAtSurfaceUnderPatch.Add(point);
            }
            else if (heightOfPoint > 0.00001f && heightOfPoint < voxelRadius)
            {
                pointsAtSurfaceOverPatch.Add(point);
            }
        }
        
        return (pointsFullySubmerged, pointsAtSurfaceUnderPatch, pointsAtSurfaceOverPatch);
    }


    private void InitializeLogs()
    {
        depthLogFile = path + "VolumeData-" + transform.name + ".csv";
        timeLogFile = path + "TimeData-" + transform.name + ".csv";

        if (!File.Exists(depthLogFile) && logVolumeData)
        {
            print("Beginning to log volume data");
            Utils.LogDataToFile(depthLogFile,"depth","volume");
            // Add a constant downward force 
            GetComponent<Rigidbody>().velocity = new Vector3(0f, -0.1f, 0f);
        }
        
        if (!File.Exists(timeLogFile) && logTimeData)
        {
            print("Beginning to log time data");
            Utils.LogDataToFile(timeLogFile,"iteration_number","time");
        }
    }


    private void OnDrawGizmos()
    {
        if (globalVoxelPositions.Count == 0) return;
        if (drawVoxelPositionGizmo)
        {
            var (pointsFullySubmerged, 
                pointsAtSurfaceUnderPatch, 
                pointsAtSurfaceOverPatch) = DevelopPointLists();

            Gizmos.color = Color.green;
            foreach (Vector3 point in pointsFullySubmerged)
            {
                Gizmos.DrawSphere(point, 0.25f*voxelRadius); 
            }
            Gizmos.color = Color.yellow;
            foreach (Vector3 point in pointsAtSurfaceUnderPatch)
            {
                Gizmos.DrawSphere(point, 0.25f*voxelRadius); 
            }
            Gizmos.color = Color.red;
            foreach (Vector3 point in pointsAtSurfaceOverPatch)
            {
                Gizmos.DrawSphere(point, 0.25f*voxelRadius); 
            }
        }
    }
}
