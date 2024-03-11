using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System;
using System.IO;


namespace WaterInteraction
{
    public static class Forces 
    {
        public static Vector3 BuoyancyForce(float height, Vector3 normal, float area) 
        {
            Vector3 F = Constants.waterDensity * Constants.gravity * height * normal*area;
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);
            return FVertical;
        }
    }
    
    
    public static class Constants 
    {
        public const float gravity = 9.80665f;
        public const float waterDensity = 997;
        public const float waterViscosity = 1.0016f;
    }
    
    
    public class Utils : MonoBehaviour
    {
        private string path = "Assets/Data/";
        
        
        /// Called in Submerged.cs/GetSubmergedTriangles().
        /// Retuns the normal vector of a triangle given its vertices.
        public static Vector3 GetFaceNormal(Vector3 A, Vector3 B, Vector3 C) 
        {
            Vector3 normal = 0.5f * Vector3.Cross((B - A), (C - A));
            return normal;
        }


        public static Vector3 GetAveragePoint(Vector3[] vecs) 
        {
            Vector3 tot = Vector3.zero;
            foreach (Vector3 v in vecs) 
            {
                tot += v;
            }
            return tot / vecs.Length;
        }

        
        public static void DebugDrawTriangle(Vector3[] triangle, Color color) 
        {
            UnityEngine.Debug.DrawLine(triangle[0], triangle[1], color);
            UnityEngine.Debug.DrawLine(triangle[0], triangle[2], color);
            UnityEngine.Debug.DrawLine(triangle[1], triangle[2], color);
        }
        
        
        public static float[] CalculateTriangleAreas(Data data) 
        {
            int triangleCount = data.maxTriangleIndex / 3;
            float[] triangleAreas = data.triangleAreas;

            for (int i = 0; i < data.maxTriangleIndex; i += 3) 
            {
                Vector3 v1 = data.vertices[data.triangles[i + 1]] - data.vertices[data.triangles[i]];
                Vector3 v2 = data.vertices[data.triangles[i + 2]] - data.vertices[data.triangles[i]];
                float area = 0.5f * Vector3.Cross(v1, v2).magnitude;
                triangleAreas[i / 3] = area;
            }
            return triangleAreas;
        }
        
        
        public static void DrawPatch(Patch patch)
        {
            int[] triangles = patch.baseGridMesh.triangles;
            Vector3[] vertices = patch.patchVertices;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                Vector3[] tri = new Vector3[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] };
                Utils.DebugDrawTriangle(tri, Color.red);
            }
        }
        
        
        /// Using generics to log data to a csv file. First parameter is file path,
        /// second and third are the data to be logged.
        public static void LogDataToFile<T1, T2>(string filePath, T1 x, T2 y)
        {
            string data = $"{x},{y}";
            using (StreamWriter sw = File.AppendText(filePath))
            {
                sw.WriteLine(data);
            }
        }
        
        
        /// Measures the time of an action. Lambda expressions are used to pass the action.
        public static double MeasureExecutionTime(Action action)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }
        
        
        public (string depthLogFile, string timeLogFile) InitializeLogs(bool logVolumeData, bool logTimeData)
        {
            string depthLogFile = path + "VolumeData-" + transform.name + ".csv";
            string timeLogFile = path + "TimeData-" + transform.name + ".csv";

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

            return (depthLogFile, timeLogFile);
        }
    }
}