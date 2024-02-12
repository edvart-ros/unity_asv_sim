using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System;

// TODO: Investigate if transforms could be optimized by using a single transform for all submerged objects

namespace WaterInteraction
{
    /// Lists previously in GetSubmergedTriangles.
    /// Class holding the vertices, triangles and normals of the submerged mesh.
    public class MeshData 
    {
        public List<Vector3> Vertices { get; set; } = new List<Vector3>();
        public List<int> Triangles { get; set; } = new List<int>();
        public List<Vector3> Normals { get; set; } = new List<Vector3>();
    }

    
    public class SubmergedData
    {
        public Vector3[] SubmergedVertices { get; set; }
        public int[] SubmergedTriangles { get; set; }
        public Vector3[] SubmergedNormals { get; set; }
        public Transform SubmersionTransform { get; set; }
        public List<Vector3> IntersectionVertices { get; set; } = new List<Vector3>();
    }
    
    
    public class Submerged 
    {
        // OK Public
        public float[] faceCenterHeightsAboveWater = new float[0];
        public Vector3[] faceCentersWorld = new Vector3[0];
        public Vector3[] faceNormalsLocal = new Vector3[0]; // Used in KernerDynamics
        public Vector3[] pressureCenters = new Vector3[0];
        public Vector3[] waterLineVerts = new Vector3[0];
        public Mesh newSubmergedMesh = new Mesh();

        public float volume = 0f;

        // OK Private
        private int numberOfHullVertices;
        private float[] triangleAreas;
        public Mesh hullMesh;

        // Only submergedVolume uses these
        public Vector3 centroid = Vector3.zero;
        public Vector3 centroidUp = Vector3.zero;
        public Vector3 centroidDown = Vector3.zero; 


        // Called from Submersion.cs
        /// Populates global variables with the submerged mesh and its properties
        public Submerged(Mesh simplifiedHullMesh, bool debug=false) 
        {
            hullMesh = simplifiedHullMesh;
            numberOfHullVertices = hullMesh.vertices.Length;
        }


        public void Update(Patch patch, Transform submersionTransform) 
        {
            newSubmergedMesh.Clear();
            SubmergedData submergedData = new SubmergedData();
            submergedData.SubmersionTransform = submersionTransform;

            ProduceSubmergedTriangles(submergedData, patch, hullMesh.vertices, hullMesh.triangles, hullMesh.normals);

            newSubmergedMesh.vertices = submergedData.SubmergedVertices; 
            newSubmergedMesh.triangles = submergedData.SubmergedTriangles;

            faceNormalsLocal = submergedData.SubmergedNormals;
            waterLineVerts = submergedData.IntersectionVertices.ToArray();
            
            // TODO: Consider setting globals in function, no return
            triangleAreas               = GetTriangleAreas(submergedData);
            faceCenterHeightsAboveWater = GetTriangleCenterHeights(submergedData, patch);
            (volume, centroid)          = GetSubmergedVolume(submergedData, faceCenterHeightsAboveWater); 
            faceCentersWorld            = GetFaceCenters(submergedData);
        }


        // Called in Update
        /// Returns the arrays of vertices, triangles and normals of the submerged mesh.
        /// It also splits the triangles depending on how many vertices are submerged.
        public void ProduceSubmergedTriangles
            (SubmergedData data, Patch patch,  Vector3[] bodyVertices, int[] bodyTriangles, Vector3[] bodyVertNormals) 
        {
            MeshData meshData = new MeshData();
            Vector3[] verticesLocal = new Vector3[3];
            Vector3[] verticesWorld = new Vector3[3];
            Vector3[] normalsLocal = new Vector3[3];
            float[] vertexHeights = new float[3];

            // Loop through input triangles
            for (int i = 0; i < bodyTriangles.Length - 2; i += 3) 
            {

                int submergedCount = 0;

                // Get the local and world positions of the current triangle,
                // compute depth, track number of submerged vertices in triangle
                for (int j = 0; j < 3; j++) 
                {
                    verticesLocal[j] = bodyVertices[bodyTriangles[i + j]]; 
                    normalsLocal[j] = bodyVertNormals[bodyTriangles[i + j]];
                    verticesWorld[j] = data.SubmersionTransform.TransformPoint(verticesLocal[j]);
                    float height = patch.GetPatchRelativeHeight(verticesWorld[j]);
                    vertexHeights[j] = height;
                    if (height < 0) submergedCount++; // depth > 0 == submerged point
                }
                Vector3 triangleNormal = (normalsLocal[0] + normalsLocal[1] + normalsLocal[2]).normalized;

                // How many vertices are underwater?
                // Split them accordingly
                switch (submergedCount) 
                {
                    case 0: 
                    {
                        break;
                    }
                    case 1: 
                    {
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);
                        Vector3 highestMinusLowestPoint  = localVerticesSorted[2] - localVerticesSorted[0]; 
                        Vector3 highestMinusMiddlePoint  = localVerticesSorted[1] - localVerticesSorted[0]; 

                        float heightRatioLowToMid  = -sortedHeights[0] / (sortedHeights[1] - sortedHeights[0]); 
                        float heightRatioLowToHigh = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                        
                        Vector3 intersectPointLowToHigh = heightRatioLowToHigh * highestMinusLowestPoint;
                        Vector3 intersectPointLowToMid  = heightRatioLowToMid * highestMinusMiddlePoint;

                        Vector3 newEdgeLowToHigh = localVerticesSorted[0] + intersectPointLowToHigh;
                        Vector3 newEdgeLowToMid  = localVerticesSorted[0] + intersectPointLowToMid;
                        
                        Vector3 normal = triangleNormal * Utils.GetFaceNormal(localVerticesSorted[0], newEdgeLowToHigh, newEdgeLowToMid).magnitude;
                        AppendTriangle(meshData, localVerticesSorted[0], newEdgeLowToHigh, newEdgeLowToMid, triangleNormal);

                        data.IntersectionVertices.Add(newEdgeLowToHigh);
                        data.IntersectionVertices.Add(newEdgeLowToMid);
                        break;
                    }
                    case 2: 
                    {
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);

                        Vector3 highestMinusLowestPoint = localVerticesSorted[2] - localVerticesSorted[0];
                        Vector3 highestMinusMiddlePoint = localVerticesSorted[2] - localVerticesSorted[1];

                        float heightRatioHighToMid = -sortedHeights[1] / (sortedHeights[2] - sortedHeights[1]);
                        float heightRatioHighToLow = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
        
                        Vector3 intersectPointHighToLow = heightRatioHighToLow * highestMinusLowestPoint;
                        Vector3 intersectPointHighToMid = heightRatioHighToMid * highestMinusMiddlePoint;

                        Vector3 newEdgeHighToLow = localVerticesSorted[0] + intersectPointHighToLow;
                        Vector3 newEdgeHighToMid = localVerticesSorted[1] + intersectPointHighToMid;
        
                        AppendTriangle(meshData, localVerticesSorted[1], newEdgeHighToMid, localVerticesSorted[0], triangleNormal);
                        AppendTriangle(meshData, localVerticesSorted[0], newEdgeHighToMid, newEdgeHighToLow, triangleNormal);

                        data.IntersectionVertices.Add(newEdgeHighToMid);
                        data.IntersectionVertices.Add(newEdgeHighToLow);
                        break;
                    }
                    case 3: 
                    {
                        AppendTriangle(meshData, verticesLocal[0], verticesLocal[1], verticesLocal[2], triangleNormal);
                        break;
                    }
                }
            }
            data.SubmergedVertices = meshData.Vertices.ToArray();
            data.SubmergedTriangles = meshData.Triangles.ToArray();
            data.SubmergedNormals = meshData.Normals.ToArray();
        }


        /// Returns the areas of the triangles in the submerged mesh.
        /// Takes in the vertices of the submerged mesh.
        public float[] GetTriangleAreas(SubmergedData data)
        {
            Vector3[] vertices = data.SubmergedVertices;
            float[] areas = new float[vertices.Length/3];
            for (int i = 0; i < vertices.Length - 2; i += 3) 
            {
                Vector3 normal = Utils.GetFaceNormal(vertices[i], vertices[i+1], vertices[i+2]); 
                areas[i / 3] = normal.magnitude;
            }
            return areas;
        }


        /// Called in GetSubmergedTriangles
        /// Updates the vertices, triangles and normals of the submerged mesh.
        public void AppendTriangle(MeshData meshData, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 triNormal)
        {
            int count = meshData.Vertices.Count;
            meshData.Vertices.Add(v1);
            meshData.Vertices.Add(v2);
            meshData.Vertices.Add(v3);
            meshData.Triangles.Add(count);
            meshData.Triangles.Add(count + 1);
            meshData.Triangles.Add(count + 2);
            meshData.Normals.Add(triNormal);
        }


        // TODO: This is newest from dev branch
        /// Calculates the difference of volume between the submerged part of hull and any eventual
        /// water above it. Also calculates the center of volume of this submerged part of the hull.
        public (float vol, Vector3 volCenter) GetSubmergedVolume(SubmergedData data, float[] heights)
        {
            Transform submersionTransform = data.SubmersionTransform;
            Vector3[] vertices = data.SubmergedVertices;
            int[] triangles = data.SubmergedTriangles;
            Vector3[]  normals = data.SubmergedNormals;
            
            float totalVolumeDown = 0f;
            float totalVolumeUp = 0f;
            Vector3 sumVolumeCenterDown = Vector3.zero;
            Vector3 sumVolumeCenterUp = Vector3.zero;

            Vector3[] pointsWorldSpace = new Vector3[3];
            
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float depth = -heights[i/3];
                pointsWorldSpace[0] = submersionTransform.TransformPoint(vertices[triangles[i]]);
                pointsWorldSpace[1] = submersionTransform.TransformPoint(vertices[triangles[i+1]]);
                pointsWorldSpace[2] = submersionTransform.TransformPoint(vertices[triangles[i+2]]);
                
                
                // Triangle edges projected on horizontal plane (?)
                Vector3 edgeAtoB = pointsWorldSpace[1]-pointsWorldSpace[0]; edgeAtoB.y = 0f;
                Vector3 edgeAtoC = pointsWorldSpace[2]-pointsWorldSpace[0]; edgeAtoC.y = 0f;
                
                // Get the area of the triangle in the horizontal plane
                float projectedArea = 0.5f*Vector3.Cross(edgeAtoB, edgeAtoC).magnitude;
                float volume = projectedArea*depth;
                
                Vector3 centroid = (pointsWorldSpace[0] + pointsWorldSpace[1] + pointsWorldSpace[2]) / 3.0f;//+new Vector3(0f, depth*0.5f, 0)
                centroid.y += depth * 0.5f;
                
                // Determine if face is pointing up (negative contribution) or down (positive contribution) //TODO: MOVE DOWN
                bool trianglePointingDown = submersionTransform.TransformDirection(normals[i/3]).y < 0;
                if (trianglePointingDown) 
                {
                    sumVolumeCenterDown += centroid*volume;
                    totalVolumeDown += volume;
                }
                else 
                {
                    sumVolumeCenterUp += centroid*volume;
                    totalVolumeUp += volume;
                }
            }
            
            float totalVolume = totalVolumeDown-totalVolumeUp;
            
            if (Math.Abs(totalVolume) < 0.00001f) return (0f, Vector3.zero); //Changed these to a comparison with a small number

            centroidUp   = (Math.Abs(totalVolumeUp) < 0.00001f)   ? Vector3.zero : sumVolumeCenterUp/totalVolumeUp;
            centroidDown = (Math.Abs(totalVolumeDown) < 0.00001f) ? Vector3.zero : sumVolumeCenterDown/totalVolumeDown;
            
            Vector3 center = (centroidDown * totalVolumeDown - centroidUp * totalVolumeUp) / (totalVolume);
            return (totalVolume, center);
        }


        /// Queries the patch for the height of the center of each triangle in the submerged mesh.
        /// Returns a float array of the heights.
        public float[] GetTriangleCenterHeights(SubmergedData data, Patch patch) 
        {
            Transform transform = data.SubmersionTransform;
            Vector3[] vertices = data.SubmergedVertices;
            int[] triangles = data.SubmergedTriangles;
            float[] heights = new float[vertices.Length / 3];
            
            for (int i = 0; i < triangles.Length - 2; i += 3) 
            {
                Vector3 centerVert = (vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]])/3.0f;
                heights[i/3] = patch.GetPatchRelativeHeight(transform.TransformPoint(centerVert));
            }
            return heights;
        }


        /// Calculates the center of each triangle in the submerged mesh.
        public Vector3[] GetFaceCenters(SubmergedData data)
        {
            Transform transform = data.SubmersionTransform;
            Vector3[] vertices = data.SubmergedVertices;
            int[] triangles = data.SubmergedTriangles;
            int numberOfFaces = triangles.Length/3;
            Vector3[] centers = new Vector3[numberOfFaces];
            
            for (int i = 0; i < triangles.Length - 2; i += 3) 
            {
                Vector3 centerLocal = (vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]])/3.0f;
                centers[i/3] = transform.TransformPoint(centerLocal);
            }
            return centers;
        }
        
        
        /// Calculates the resistance coefficient of the submerged hull. 
        public float GetResistanceCoefficient(float speed, float hullZmin, float hullZmax) 
        {
            float submergedArea = Utils.CalculateMeshArea(newSubmergedMesh);
            Vector3[] vertices = newSubmergedMesh.vertices;
            int[] triangles = newSubmergedMesh.triangles;
            float Rn = CalculateReynoldsNumber(speed, Math.Abs(hullZmax - hullZmin));

            float onePlusK = 0;
            for (int i = 0; i < triangles.Length - 2; i += 3) 
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                float Si = (0.5f) * Vector3.Cross((v1 - v0), (v2 - v0)).magnitude;
                float Ki = GetTriangleK((v0.z + v1.z + v2.z) / 3.0f, hullZmin, hullZmax);
                onePlusK += (1 + Ki) * Si;
            }
            onePlusK = Mathf.Clamp(onePlusK / submergedArea, 1.22f, 1.65f);
            float Cf = 0.075f / ((Mathf.Log10(Rn) - 2.0f) * (Mathf.Log10(Rn) - 2.0f));
            float Cfr = onePlusK * Cf;
            return Cfr;
        }
        

        // Used in GetResistanceCoefficient
        private float CalculateReynoldsNumber(float velocity, float L, float viscosity = Constants.waterViscosity) 
        {
            return (velocity * L) / viscosity;
        }


        // Used in GetResistanceCoefficient
        private float GetTriangleK(float z, float hullZmin, float hullZmax) 
        {
            float f = (-3.0f / (hullZmax - hullZmin)) * z + 3.0f * hullZmax / (hullZmax - hullZmin) - 1.0f;
            return f;
        }

        
        /// Sorts the vertices of a triangle by their heights.
        /// Indexed from 0 to 2, low to high.
        /// Called in GetSubmergedTriangles.
        private static (Vector3[], float[]) SortVerticesAgainstFloats(Vector3[] vertices, float[] heights) 
        {
            if (heights[0] > heights[1]) 
            {
                Swap(ref heights[0], ref heights[1]);
                Swap(ref vertices[0], ref vertices[1]);
            }

            if (heights[1] > heights[2]) 
            {
                Swap(ref heights[1], ref heights[2]);
                Swap(ref vertices[1], ref vertices[2]);
            }

            if (heights[0] > heights[1]) 
            {
                Swap(ref heights[0], ref heights[1]);
                Swap(ref vertices[0], ref vertices[1]);
            }

            return (vertices, heights);
        }


        private static void Swap<T>(ref T a, ref T b) 
        {
            T temp = a;
            a = b;
            b = temp;
        }
    }
}


// Old variable names with new:
// LJ_H = intersectPointLowToHigh
// LJ_M = intersectPointLowToMid
// J_H = newEdgeLowToHigh
// J_M = newEdgeLowToMid

// LI_L = interpolatedLengthLowToHigh
// LI_M = interpolatedLengthMidToHigh

