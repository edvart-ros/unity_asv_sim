using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using TMPro;
using System.IO;
using UnityEditor.Playables;


//TODO: Investigate bug where the smaller voxels cause the ship to lay lower in the water
// It obviously has something to do with the amount of points, and difference between that is not accounted for
// there seems to be about a .5 or .4 difference in mass to maintain the same waterline,
// from 6 voxels to 3 voxels (15000 to 8500)
// TODO: Investigate difference in mass contribution from Kerner model 5 000 000 to voxel model 15 000


public class VoxelizedBuoyancy : MonoBehaviour
{
    public Transform planeTransform;
    
    public TextMeshProUGUI buoyancyText;
    
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    private string path = "Assets/Data/localPointsData.json";
    private List<Vector3> globalPositions = new List<Vector3>();
    private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    private int voxelVolume;
    private Transform parentTransform;
    private float actualForce;
    
    private Rigidbody shipRigidbody;
    
    
    void Awake()
    {
        // Populate local list and int with saved information
        Vector3ListWrapper wrapper = LoadPoints();
        pointsInsideMesh = wrapper.localPoints;
        voxelVolume = wrapper.volume;
        shipRigidbody = GetComponent<Rigidbody>();
    }

    
    private void FixedUpdate()
    {
        UpdateGlobalPosition();
        ApplyForce(CalculateCenterOfMass(GetPointsUnderPlane()));
        
        buoyancyText.text = "Buoyancy Force: " + actualForce.ToString("F2");
    }


    private List<Vector3> GetPointsUnderPlane()
    {
        // Create a Plane object using the position and up vector of the planeTransform
        Plane plane = new Plane(planeTransform.up, planeTransform.position);
        
        List<Vector3> pointsUnderPlane = new List<Vector3>();

        foreach (var point in globalPositions)
        {
            if (!plane.GetSide(point))
            {
                pointsUnderPlane.Add(point);
            }
        }

        return pointsUnderPlane;
    }
    
    
    private (Vector3, int) CalculateCenterOfMass(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var point in points)
        {
            sum += point;
        }
        return (sum / points.Count, points.Count);
    }


    private void ApplyForce((Vector3 centerOfMass, int numberOfPoints) data)
    {
        float force = CalculateForce(data.numberOfPoints, voxelVolume, 9.81f);
        shipRigidbody.AddForceAtPosition(force*Vector3.up, data.Item1);
        Debug.DrawRay(data.Item1,force*Vector3.up, Color.red);
    }


    private float CalculateForce(int numberOfPoints, float volume, float gravity)
    {
        float force = numberOfPoints * volume * gravity;
        actualForce = force;
        return force;
    }
    
    
    private void UpdateGlobalPosition()
    {
        if (!transform.hasChanged) return;
        globalPositions.Clear();
        foreach (Vector3 point in pointsInsideMesh)
        {
            globalPositions.Add(transform.TransformPoint(point));
        }
        // Reset the hasChanged flag
        transform.hasChanged = false;
    }
    
    
    private Vector3ListWrapper LoadPoints()
    {
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<Vector3ListWrapper>(json);
    }
    
    
    private void OnDrawGizmos()
    {
        if (globalPositions.Count == 0) return;
        Gizmos.color = Color.magenta;
        foreach (Vector3 point in globalPositions)
        {
            Gizmos.DrawSphere(point, 1); 
        }
    }
}
