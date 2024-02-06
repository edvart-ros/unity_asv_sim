using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using System.IO;
using UnityEditor.Playables;


public class VoxelizedBuoyancy : MonoBehaviour
{
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    private string path = "Assets/Data/localPointsData.json";
    private List<Vector3> globalPositions = new List<Vector3>();
    private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    private int voxelVolume;
    private Transform parentTransform;
    
    private Rigidbody rb;
    
    
    // TODO: See following list:
    // 1. Read points from file
    // 2. Convert points from local to global coordinates
    // 3. Iterate over the points, finding the distance from water surface.
    // If above water, discard and continue
    // If below water, add to list of points to be used for buoyancy calculation
    // 4. Determine the centre of mass of the points
    // 5. Apply force to this point based on the amount of points total and the volume
    // which is constant for each point, so it is just the number of points * volume * gravity
    
    
    void Awake()
    {
        // Populate local list and int with saved information
        Vector3ListWrapper wrapper = LoadPoints();
        pointsInsideMesh = wrapper.localPoints;
        voxelVolume = wrapper.volume;
    }

    
    private void FixedUpdate()
    {
        UpdateGlobalPosition();
        foreach (var point in globalPositions)
        {
            //TODO: Add a check to see if the point is above or below the water surface
            //if (transform.TransformPoint(point).y <= 0)
            {
                //Debug.Log("Called buoyancy");
                //rb.AddForceAtPosition(997 * voxelSize * voxelSize * voxelSize * Vector3.up, transform.TransformPoint(point));



            }
        }
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
