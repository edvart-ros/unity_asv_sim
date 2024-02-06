using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Packages for reading data from file
using System.IO;

namespace WaterInteraction.Statics
{
    public class BuoyancyVoxel
    {
        private List<Vector3> pointsInsideMesh = new List<Vector3>();
        private string path = "Assets/localPointsData.json";
        
        
        void Awake()
        {
            pointsInsideMesh = LoadPoints();
        }
        
        void FixedUpdate()
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
}