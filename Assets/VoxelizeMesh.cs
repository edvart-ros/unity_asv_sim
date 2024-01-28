using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

[ExecuteInEditMode]
public class VoxelizeMesh : MonoBehaviour
{
    public Mesh meshToVoxelize;
    public int voxelSize = 3; // Size of the voxels
    public float yOffset = 0f;
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    public LayerMask meshLayer;
    public List<Vector3> pointsData;

    private Rigidbody rb;

    public void Test()
    {
        rb = GetComponentInParent<Rigidbody>();
        yOffset = voxelSize / 2f;
        // Clear the pointsInsideMesh list
        pointsInsideMesh.Clear();
        print("Running Test");
        Bounds bounds = meshToVoxelize.bounds;
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


        Debug.Log(pointsInsideMesh.Count);
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
                rb.AddForceAtPosition(997 * voxelSize * voxelSize * voxelSize * Vector3.up,
                    transform.TransformPoint(point));



            }
        }
    }


    bool IsInsideMesh(Vector3 point)
    {
        Bounds bounds = meshToVoxelize.bounds;
        Ray ray = new Ray(point, bounds.center - point);
        RaycastHit hit;

        // Draw raycasts
        Debug.DrawRay(ray.origin, ray.direction * 3, Color.yellow, 2f);

        if (Physics.Raycast(ray, out hit, 100f, meshLayer))
        {
            Debug.Log("Hit");
            return false;
        }

        return true;
    }

    void OnDrawGizmos()
    {
        if (meshToVoxelize != null)
        {
            Bounds bounds = meshToVoxelize.bounds;
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

