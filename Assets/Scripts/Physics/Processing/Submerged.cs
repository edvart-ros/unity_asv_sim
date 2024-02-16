using System.Collections.Generic;
using UnityEngine;
using System;

// TODO: Investigate if transforms could be optimized by using a single transform for all submerged objects

namespace WaterInteraction
{
    /// Lists previously in GetSubmergedTriangles.
    /// Class holding the vertices, triangles and normals of the submerged mesh.

    
    public class Data
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Vector3[] normals;
        public Transform transform;

        //public Vector3[] waterLineVerts { get; set; }
        public Vector3[] faceCentersWorld;
        public float[] faceCenterHeightsAboveWater;
        public float[] triangleAreas;
        public Vector3 centroid;
        public float volume;
        public int maxTriangleIndex;
        public Data(int maxNumTriangles){
            vertices = new Vector3[maxNumTriangles];
            triangles = new int[maxNumTriangles];
            normals = new Vector3[maxNumTriangles/3];
            //waterLineVerts = new Vector3[maxNumTriangles];
            faceCentersWorld = new Vector3[maxNumTriangles/3];
            faceCenterHeightsAboveWater = new float[maxNumTriangles/3];
            triangleAreas = new float[maxNumTriangles/3];
            centroid = new Vector3();
            volume = new float();
            maxTriangleIndex = 0;
        }
    
    }
    
    
    public class Submerged 
    {
        // OK Public
        
        public Data data;

        // OK Private
        public Mesh hullMesh;
        // caching input mesh data (these shouldnt change at runtime)
        private Vector3[] hullMeshVertices, hullMeshNormals;
        private int[] hullMeshTriangles;

        // Only submergedVolume uses these

        // Called from Submersion.cs
        /// Populates global variables with the submerged mesh and its properties
        public Submerged(Mesh simplifiedHullMesh, bool debug=false) 
        {
            hullMesh = simplifiedHullMesh;
            hullMeshVertices = simplifiedHullMesh.vertices;
            hullMeshNormals = simplifiedHullMesh.normals;
            hullMeshTriangles = simplifiedHullMesh.triangles;
            
            data = new Data(simplifiedHullMesh.triangles.Length*2);
        }


        public void Update(Patch patch, Transform submersionTransform) 
        {
            data.maxTriangleIndex = 0;
            data.transform = submersionTransform;

            ProduceSubmergedTriangles(data, patch, hullMeshVertices, hullMeshTriangles, hullMeshNormals);
            
            // TODO: Consider setting globals in function, no return
            data.triangleAreas               = GetTriangleAreas(data);
            data.faceCenterHeightsAboveWater = GetTriangleCenterHeights(data, patch);
            (data.volume, data.centroid)          = GetSubmergedVolume(data, data.faceCenterHeightsAboveWater); 
            GetFaceCenters(data);
        }


        // Called in Update
        /// Returns the arrays of vertices, triangles and normals of the submerged mesh.
        /// It also splits the triangles depending on how many vertices are submerged.
        public void ProduceSubmergedTriangles
            (Data data, Patch patch,  Vector3[] bodyVertices, int[] bodyTriangles, Vector3[] bodyVertNormals) 
        {
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
                    verticesWorld[j] = data.transform.TransformPoint(verticesLocal[j]);
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
                        break;
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);
                        Vector3 highestMinusLowestPoint  = localVerticesSorted[2] - localVerticesSorted[0]; 
                        Vector3 highestMinusMiddlePoint  = localVerticesSorted[1] - localVerticesSorted[0]; 

                        float heightRatioLowToMid  = -sortedHeights[0] / (sortedHeights[1] - sortedHeights[0]); 
                        float heightRatioLowToHigh = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                        
                        Vector3 intersectPointLowToHigh = heightRatioLowToHigh * highestMinusLowestPoint;
                        Vector3 intersectPointLowToMid  = heightRatioLowToMid * highestMinusMiddlePoint;

                        Vector3 newEdgeLowToHigh = localVerticesSorted[0] + intersectPointLowToHigh;
                        Vector3 newEdgeLowToMid  = localVerticesSorted[0] + intersectPointLowToMid;
                        
                        AppendTriangle(data, localVerticesSorted[0], newEdgeLowToHigh, newEdgeLowToMid, triangleNormal);
                        break;
                    }
                    case 2: 
                    {
                        break;
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);

                        Vector3 highestMinusLowestPoint = localVerticesSorted[2] - localVerticesSorted[0];
                        Vector3 highestMinusMiddlePoint = localVerticesSorted[2] - localVerticesSorted[1];

                        float heightRatioHighToMid = -sortedHeights[1] / (sortedHeights[2] - sortedHeights[1]);
                        float heightRatioHighToLow = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
        
                        Vector3 intersectPointHighToLow = heightRatioHighToLow * highestMinusLowestPoint;
                        Vector3 intersectPointHighToMid = heightRatioHighToMid * highestMinusMiddlePoint;

                        Vector3 newEdgeHighToLow = localVerticesSorted[0] + intersectPointHighToLow;
                        Vector3 newEdgeHighToMid = localVerticesSorted[1] + intersectPointHighToMid;
        
                        AppendTriangle(data, localVerticesSorted[1], newEdgeHighToMid, localVerticesSorted[0], triangleNormal);
                        AppendTriangle(data, localVerticesSorted[0], newEdgeHighToMid, newEdgeHighToLow, triangleNormal);
                        break;
                    }
                    case 3: 
                    {
                        AppendTriangle(data, verticesLocal[0], verticesLocal[1], verticesLocal[2], triangleNormal);
                        break;
                    }
                }
            }
        }


        /// Returns the areas of the triangles in the submerged mesh.
        /// Takes in the vertices of the submerged mesh.
        public float[] GetTriangleAreas(Data data)
        {
            Vector3[] vertices = data.vertices;
            float[] areas = data.triangleAreas;
            for (int i = 0; i < data.maxTriangleIndex - 2; i += 3) 
            {
                Vector3 normal = Utils.GetFaceNormal(vertices[i], vertices[i+1], vertices[i+2]); 
                areas[i / 3] = normal.magnitude;
            }
            return areas;
        }


        /// Called in GetSubmergedTriangles
        /// Updates the vertices, triangles and normals of the submerged mesh.
        public void AppendTriangle(Data data, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 triNormal)
        {
            int currentIndex = data.maxTriangleIndex;
            data.triangles[currentIndex] = currentIndex;
            data.triangles[currentIndex+1] = currentIndex+1;
            data.triangles[currentIndex+2] = currentIndex+2;

            data.vertices[currentIndex] = v1;
            data.vertices[currentIndex+1] = v2;
            data.vertices[currentIndex+2] = v3;
            data.normals[currentIndex/3] = triNormal;
            data.maxTriangleIndex = currentIndex + 3;
        }


        // TODO: This is newest from dev branch
        /// Calculates the difference of volume between the submerged part of hull and any eventual
        /// water above it. Also calculates the center of volume of this submerged part of the hull.
        public (float vol, Vector3 volCenter) GetSubmergedVolume(Data data, float[] heights)
        {
            Transform submersionTransform = data.transform;
            Vector3[] vertices = data.vertices;
            int[] triangles = data.triangles;
            Vector3[]  normals = data.normals;
            
            float totalVolumeDown = 0f;
            float totalVolumeUp = 0f;
            Vector3 sumVolumeCenterDown = Vector3.zero;
            Vector3 sumVolumeCenterUp = Vector3.zero;

            Vector3[] pointsWorldSpace = new Vector3[3];
            
            for (int i = 0; i < this.data.maxTriangleIndex; i += 3)
            {
                float depth = -heights[i / 3];
                pointsWorldSpace[0] = submersionTransform.TransformPoint(vertices[triangles[i]]);
                pointsWorldSpace[1] = submersionTransform.TransformPoint(vertices[triangles[i + 1]]);
                pointsWorldSpace[2] = submersionTransform.TransformPoint(vertices[triangles[i + 2]]);


                // Triangle edges projected on horizontal plane (?)
                Vector3 edgeAtoB = pointsWorldSpace[1]- pointsWorldSpace[0]; edgeAtoB.y = 0f;
                Vector3 edgeAtoC = pointsWorldSpace[2]- pointsWorldSpace[0]; edgeAtoC.y = 0f;
                
                // Get the area of the triangle in the horizontal plane
                float projectedArea = 0.5f* Vector3.Cross(edgeAtoB, edgeAtoC).magnitude;
                float volume = projectedArea* depth;

                Vector3 centroid = (pointsWorldSpace[0] + pointsWorldSpace[1] + pointsWorldSpace[2]) / 3.0f;//+new Vector3(0f, depth*0.5f, 0)
                centroid.y += depth * 0.5f;
                
                // Determine if face is pointing up (negative contribution) or down (positive contribution) //TODO: MOVE DOWN
                bool trianglePointingDown = submersionTransform.TransformDirection(normals[i / 3]).y < 0;
                if (trianglePointingDown) 
                {
                    sumVolumeCenterDown += centroid* volume;
                    totalVolumeDown += volume;
                }
                else 
                {
                    sumVolumeCenterUp += centroid* volume;
                    totalVolumeUp += volume;
                }
            }
            
            float totalVolume = totalVolumeDown-totalVolumeUp;
            
            if (Math.Abs(totalVolume) < 0.00001f) return (0f, Vector3.zero); //Changed these to a comparison with a small number

            Vector3 centroidUp   = (Math.Abs(totalVolumeUp) < 0.00001f)   ? Vector3.zero : sumVolumeCenterUp/totalVolumeUp;
            Vector3 centroidDown = (Math.Abs(totalVolumeDown) < 0.00001f) ? Vector3.zero : sumVolumeCenterDown/totalVolumeDown;
            
            Vector3 center = (centroidDown * totalVolumeDown - centroidUp * totalVolumeUp) / (totalVolume);
            return (totalVolume, center);
        }


        /// Queries the patch for the height of the center of each triangle in the submerged mesh.
        /// Returns a float array of the heights.
        public float[] GetTriangleCenterHeights(Data data, Patch patch) 
        {
            Transform transform = data.transform;
            Vector3[] vertices = data.vertices;
            int[] triangles = data.triangles;
            float[] heights = this.data.faceCenterHeightsAboveWater;
            
            for (int i = 0; i < data.maxTriangleIndex - 2; i += 3) 
            {
                Vector3 centerVert = (vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]])/3.0f;
                heights[i/3] = patch.GetPatchRelativeHeight(transform.TransformPoint(centerVert));
            }
            return heights;
        }


        /// Calculates the center of each triangle in the submerged mesh.
        public Vector3[] GetFaceCenters(Data data)
        {
            Transform transform = data.transform;
            Vector3[] vertices = data.vertices;
            int[] triangles = data.triangles;
            Vector3[] centers = this.data.faceCentersWorld;
            
            for (int i = 0; i < data.maxTriangleIndex - 2; i += 3) 
            {
                Vector3 centerLocal = (vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]])/3.0f;
                centers[i/3] = transform.TransformPoint(centerLocal);
            }
            return centers;
        }
        
        
        /// Calculates the resistance coefficient of the submerged hull. 
        public float GetResistanceCoefficient(float speed, float hullZmin, float hullZmax, Data data) 
        {
            float submergedArea = CalculateMeshArea(data);
            float Rn = CalculateReynoldsNumber(speed, Math.Abs(hullZmax - hullZmin));

            float onePlusK = 0;
            for (int i = 0; i < data.maxTriangleIndex - 2; i += 3) 
            {
                Vector3 v0 = data.vertices[data.triangles[i]];
                Vector3 v1 = data.vertices[data.triangles[i + 1]];
                Vector3 v2 = data.vertices[data.triangles[i + 2]];
                float Si = (0.5f) * Vector3.Cross((v1 - v0), (v2 - v0)).magnitude;
                float Ki = GetTriangleK((v0.z + v1.z + v2.z) / 3.0f, hullZmin, hullZmax);
                onePlusK += (1 + Ki) * Si;
            }
            onePlusK = Mathf.Clamp(onePlusK / submergedArea, 1.22f, 1.65f);
            float Cf = 0.075f / ((Mathf.Log10(Rn) - 2.0f) * (Mathf.Log10(Rn) - 2.0f));
            float Cfr = onePlusK * Cf;
            return Cfr;
        }
        

        public float CalculateMeshArea(Data data) 
        {
            float totalArea = 0.0f;
            for (int i = 0; i < data.maxTriangleIndex - 2; i += 3) 
            {
                Vector3 v1 = data.vertices[data.triangles[i + 1]] - data.vertices[data.triangles[i]];
                Vector3 v2 = data.vertices[data.triangles[i + 2]] - data.vertices[data.triangles[i]];
                Vector3 cross = Vector3.Cross(v1, v2);
                totalArea += 0.5f * cross.magnitude;
            }
            return totalArea;
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

