using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WaterInteraction
{
    public static class Constants {
        public const float g = 9.80665f;
        public const float rho = 0.5f * 997;
        public const float waterViscosity = 1.0016f;
    }
    
    public class Utils {
        public static Vector3 GetFaceNormal(Vector3 A, Vector3 B, Vector3 C) {
            Vector3 normal = 0.5f * Vector3.Cross((B - A), (C - A));
            return normal;
        }


        public static Vector3 GetAveragePoint(Vector3[] vecs) {
            Vector3 tot = Vector3.zero;
            foreach (Vector3 v in vecs) {
                tot += v;
            }
            return tot / vecs.Length;
        }

        public static void DebugDrawTriangle(Vector3[] triangle, Color color) {
            Debug.DrawLine(triangle[0], triangle[1], color);
            Debug.DrawLine(triangle[0], triangle[2], color);
            Debug.DrawLine(triangle[1], triangle[2], color);
        }
        public static float CalculateMeshArea(Mesh mesh) {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            float totalArea = 0.0f;
            for (int i = 0; i < triangles.Length - 2; i += 3) {
                Vector3 v1 = vertices[triangles[i + 1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 2]] - vertices[triangles[i]];
                Vector3 cross = Vector3.Cross(v1, v2);
                totalArea += 0.5f * cross.magnitude;
            }
            return totalArea;
        }

        public static float[] CalculateTriangleAreas(Mesh mesh) {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            int triangleCount = triangles.Length / 3;
            float[] triangleAreas = new float[triangleCount];

            for (int i = 0; i < triangles.Length; i += 3) {
                Vector3 v1 = vertices[triangles[i + 1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 2]] - vertices[triangles[i]];
                float area = 0.5f * Vector3.Cross(v1, v2).magnitude;
                triangleAreas[i / 3] = area;
            }
            return triangleAreas;
        }

    }
    public static class Forces {
        public static Vector3 BuoyancyForce(float height, Vector3 normal, float area) {
            Vector3 F = Constants.rho * Constants.g * height * normal*area;
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);
            return FVertical;
        }
    }
    
}