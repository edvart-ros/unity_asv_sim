using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using System.IO;

public class VoxelizedBuoyancy : MonoBehaviour
{
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    private string path = "Assets/Data/localPointsData.json";
    private List<Vector3> globalPositions = new List<Vector3>();
    private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    private int voxelSize = 6;
    private int voxelVolume = 1;
    
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
    
    
    
    
    
    
    
    // Start is called before the first frame update
    void Awake()
    {
        voxelVolume = voxelSize * voxelSize * voxelSize;
        // Populate local list with saved points
        relativePositions = LoadPoints();
    }

    
    private void FixedUpdate()
    {
        //Debug.Log("update");
        //Debug.Log(pointsInsideMesh.Count);
        foreach (var point in pointsInsideMesh)
        {
            //Debug.Log(transform.TransformPoint(point).y);
            //if (transform.TransformPoint(point).y <= 0)
            {
                //Debug.Log("Called buoyancy");
                //rb.AddForceAtPosition(997 * voxelSize * voxelSize * voxelSize * Vector3.up, transform.TransformPoint(point));



            }
        }
    }
    
    
    private List<Vector3> LoadPoints()
    {
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<List<Vector3>>(json);
    }
}
