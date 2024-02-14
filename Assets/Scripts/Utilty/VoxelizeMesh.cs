using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO; // Package for saving data to file


[ExecuteInEditMode]
public class VoxelizeMesh : MonoBehaviour
{
    [Tooltip("The layer with the colliders. You usually only want the object to voxelize in this layer.")]
    public LayerMask colliderLayer;

    [Tooltip("This field is required. Usually, the mesh of the object you want to voxelize.")]
    public Mesh boundsTarget;

    public float voxelSize = 6;
    
    private List<Vector3> pointsInsideMesh = new List<Vector3>();
    private string path = "Assets/Data/localPointsData.json";


    /// Voxelize the mesh by evenly distributing a point cloud.
    /// Iterates over these points to determine which are inside.
    /// Saves the points to a file.
    public void DeterminePoints()
    {
        print("Determining Points");
        pointsInsideMesh.Clear();
        Bounds bounds;
        if (boundsTarget) bounds = boundsTarget.bounds;
        else
        {
            print("Error: Bounds target is required");
            return;
        }

        int totalPoints = 0;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;


        // Calculate starting points for each axis to ensure the middle line goes through the points
        Vector3 start = new Vector3(
            center.x - Mathf.Floor(extents.x / voxelSize) * voxelSize,
            center.y - Mathf.Floor(extents.y / voxelSize) * voxelSize,
            center.z - Mathf.Floor(extents.z / voxelSize) * voxelSize);

        // Loop for each axis starting from the calculated start point and moving outwards
        for (float x = start.x; x <= center.x + extents.x; x += voxelSize)
        { for (float y = start.y; y <= center.y + extents.y; y += voxelSize)
            { for (float z = start.z; z <= center.z + extents.z; z += voxelSize)
                {
                    Vector3 point = new Vector3(x, y, z);
                    if (IsInsideMesh(point)) pointsInsideMesh.Add(point);
                    totalPoints++;
                }
            }
        }

        print("Number of points inside mesh: " + pointsInsideMesh.Count + " out of " + totalPoints);
        ConvertPointsToLocalSpaceAndSave();
    }


    private bool IsInsideMesh(Vector3 point)
    {
        Ray ray = new Ray(point, boundsTarget.bounds.center - point);
        Debug.DrawRay(ray.origin, ray.direction * 3, Color.yellow, 2f);
        bool hitDetected = Physics.Raycast(ray, 100f, colliderLayer);

        if (hitDetected) return false;
        return true;
    }


    private void ConvertPointsToLocalSpaceAndSave()
    {
        Vector3ListWrapper wrapper = new Vector3ListWrapper();
        wrapper.volume = voxelSize * voxelSize * voxelSize;

        foreach (Vector3 point in pointsInsideMesh)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            wrapper.localPoints.Add(localPoint);
        }

        string json = JsonUtility.ToJson(wrapper);
        File.WriteAllText(path, json);
    }

    
    /// Run over each point inside mesh and determine how many neighbors.
    private void FindFaces()
    {
        Vector3[] directions = new Vector3[]
        {
            new Vector3(voxelSize, 0, 0), // Right
            new Vector3(-voxelSize, 0, 0), // Left
            new Vector3(0, voxelSize, 0), // Up
            new Vector3(0, -voxelSize, 0), // Down
            new Vector3(0, 0, voxelSize), // Forward
            new Vector3(0, 0, -voxelSize) // Backward
        };
        
        Dictionary<Vector3, List<Vector3>> pointNeighborsDirections = new Dictionary<Vector3, List<Vector3>>();

        foreach (Vector3 point in pointsInsideMesh)
        {
            List<Vector3> neighborDirections = new List<Vector3>();

            foreach (Vector3 direction in directions)
            {
                Vector3 neighbor = point + direction;
                // Check if the neighbor is inside the mesh
                if (IsInsideMesh(neighbor))
                {
                    // Add the direction to the list if the neighbor is inside the mesh
                    neighborDirections.Add(direction);
                }
            }

            // Store the directions to neighbors for the current point
            if (neighborDirections.Count > 0)
            {
                pointNeighborsDirections[point] = neighborDirections;
            }
        }
    }
    
    
    private void OnDrawGizmos()
    {
        if (!boundsTarget) return;
        Bounds bounds = boundsTarget.bounds;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, bounds.size);

        Gizmos.color = Color.green;
        foreach (Vector3 point in pointsInsideMesh)
        {
            Gizmos.DrawWireCube(point, Vector3.one * voxelSize);
        }
    }
}


[System.Serializable]
public class Vector3ListWrapper
{
    public List<Vector3> localPoints = new List<Vector3>();
    public float volume;
}