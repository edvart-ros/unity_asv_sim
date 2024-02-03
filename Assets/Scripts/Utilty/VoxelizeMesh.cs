using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Packages for saving data to a file
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


[ExecuteInEditMode]
public class VoxelizeMesh : MonoBehaviour
{
    //public VoxelizedBuoyancy voxelizedBuoyancy;
    
    public Mesh meshToVoxelize;
    public Mesh otherMeshToVoxelize;
    public int voxelSize = 6; // Size of the voxels
    public float yOffset = 0f;
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    public LayerMask meshLayer;
    public List<Vector3> pointsData;

    private Rigidbody rb;

    public void Test()
    {
        rb = GetComponentInParent<Rigidbody>();
        yOffset = voxelSize / 2f;// Probably redundant. Use halfWidth instead
        // Clear the pointsInsideMesh list
        pointsInsideMesh.Clear();
        print("Running Test");
        Bounds bounds = otherMeshToVoxelize.bounds;
        Vector3 start = transform.position - bounds.extents;
        Vector3 center = transform.position; // Center of the bounding box
        float halfWidth = bounds.extents.x; // Half the width of the bounding box
        
        // Loop starting from the center and moving outwards in the positive x direction
        for (float x = center.x; x <= center.x + halfWidth; x += voxelSize)
        {
            for (float y = start.y + yOffset; y <= start.y + yOffset + bounds.size.y; y += voxelSize)
            {
                for (float z = start.z; z <= start.z + bounds.size.z; z += voxelSize)
                {
                    Vector3 point = new Vector3(x, y, z);
                    // Add your logic here for working with the point
                    if (IsInsideMesh(point))
                    {
                        //Debug.Log(point);
                        pointsInsideMesh.Add(point);

                    }
                }
            }
        }

        // Loop starting from the center and moving outwards in the negative x direction
        for (float x = center.x - voxelSize; x >= center.x - halfWidth; x -= voxelSize)
        {
            for (float y = start.y + yOffset; y <= start.y + yOffset + bounds.size.y; y += voxelSize)
            {
                for (float z = start.z; z <= start.z + bounds.size.z; z += voxelSize)
                {
                    Vector3 point = new Vector3(x, y, z);
                    // Add your logic here for working with the point
                    if (IsInsideMesh(point))
                    {
                        //Debug.Log(point);
                        pointsInsideMesh.Add(point);

                    }
                }
            }
        }

        // Write the number of points in the list to a file
        
        // TODO: This saving might not work.
        SavePoints(pointsInsideMesh, "Assets/pointsData.txt");
        print("No. of points in list: " + pointsInsideMesh.Count);
        pointsData = pointsInsideMesh;
    }


    private void Start()
    {
        Test();
        Debug.Log("start");
        Debug.Log(pointsInsideMesh.Count);
    }

    
    private void FixedUpdate()
    {
        //Debug.Log("update");
        //Debug.Log(pointsInsideMesh.Count);
        foreach (var point in pointsInsideMesh)
        {
            //Debug.Log(transform.TransformPoint(point).y);
            if (transform.TransformPoint(point).y <= 0)
            {
                //Debug.Log("Called buoyancy");
                //rb.AddForceAtPosition(997 * voxelSize * voxelSize * voxelSize * Vector3.up, transform.TransformPoint(point));



            }
        }
    }


    bool IsInsideMesh(Vector3 point)
    {
        Bounds bounds = otherMeshToVoxelize.bounds;
        Ray ray = new Ray(point, bounds.center - point);//
        RaycastHit hit;
        RaycastHit[] hits;
        
        // Draw raycasts
        Debug.DrawRay(ray.origin, ray.direction * 3, Color.yellow, 2f);

        hits = Physics.RaycastAll(ray.origin, ray.direction, 100f, meshLayer);
            
        //if (hits.Length % 2 == 0) // Even number of hits
        {
            //print("Even hit");
            //return false;
        }
        if (Physics.Raycast(ray, out hit, 100f, meshLayer)) // Even number of hits
        {
            print("Hit");
            return false;
        }
        // Add more logic here

        print("Odd hit");
        return true;
    }

    
    public void SavePoints(List<Vector3> pointsList, string path)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (FileStream stream = new FileStream(path, FileMode.Create))
        {
            formatter.Serialize(stream, pointsList);
        }
    }
    
    
    void OnDrawGizmos()
    {
        if (otherMeshToVoxelize != null)
        {
            Bounds bounds = otherMeshToVoxelize.bounds;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, bounds.size);

            Gizmos.color = Color.green;
            foreach (Vector3 point in pointsInsideMesh)
            {
                //Debug.Log(point);
                Gizmos.DrawWireCube(point, Vector3.one * voxelSize); // Draw a cube at each point
            }
        }
    }
}
