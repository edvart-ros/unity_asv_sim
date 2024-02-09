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
        public Vector3[] centroids = new Vector3[0];
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
        public Vector3[] centroidsUp = new Vector3[0]; 
        public Vector3[] centroidsDown = new Vector3[0]; 


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

            GetSubmergedTriangles(submergedData, patch, hullMesh.vertices, hullMesh.triangles, hullMesh.normals);

            newSubmergedMesh.vertices = submergedData.SubmergedVertices; 
            newSubmergedMesh.triangles = submergedData.SubmergedTriangles;

            faceNormalsLocal = submergedData.SubmergedNormals;
            waterLineVerts = submergedData.IntersectionVertices.ToArray();
            
            triangleAreas               = GetTriangleAreas(submergedData);
            faceCenterHeightsAboveWater = GetTriangleCenterHeights(submergedData, patch);
            (volume, centroid)          = GetSubmergedVolume(submergedData, faceCenterHeightsAboveWater); // TODO: Consider setting globals in function, no return
            faceCentersWorld            = GetFaceCenters(submergedData);
        }


        // Called in Update
        /// Returns the arrays of vertices, triangles and normals of the submerged mesh.
        /// It also splits the triangles depending on how many vertices are submerged.
        public void GetSubmergedTriangles
            (SubmergedData data, Patch patch,  Vector3[] bodyVertices, int[] bodyTriangles, Vector3[] bodyVertNormals) 
        {
            MeshData meshData = new MeshData();

            // Loop through input triangles
            for (int i = 0; i < bodyTriangles.Length - 2; i += 3) 
            {
                Vector3[] verticesLocal = new Vector3[3];
                Vector3[] verticesWorld = new Vector3[3];
                Vector3[] normalsLocal = new Vector3[3];
                float[] vertexHeights = new float[3];

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
                    { // TODO: Continue from here
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);
                        Vector3 highestMinusLowest = localVerticesSorted[2] - localVerticesSorted[0]; 
                        Vector3 middleMinusLowest  = localVerticesSorted[1] - localVerticesSorted[0]; 

                        float heightRatioLowToMid  = -sortedHeights[0] / (sortedHeights[1] - sortedHeights[0]); 
                        float heightRatioLowToHigh = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                        Vector3 LJ_H = heightRatioLowToHigh * highestMinusLowest; // TODO: Rename 
                        Vector3 LJ_M = heightRatioLowToMid * middleMinusLowest;

                        Vector3 J_H = localVerticesSorted[0] + LJ_H;
                        Vector3 J_M = localVerticesSorted[0] + LJ_M;
                        Vector3 normal = triangleNormal * Utils.GetFaceNormal(localVerticesSorted[0], J_H, J_M).magnitude;
                        AppendTriangle(meshData, localVerticesSorted[0], J_H, J_M, triangleNormal);

                        data.IntersectionVertices.Add(J_H);
                        data.IntersectionVertices.Add(J_M);
                        break;
                    }
                    case 2: 
                    {
                        (Vector3[] localVerticesSorted, float[] sortedHeights) = SortVerticesAgainstFloats(verticesLocal, vertexHeights);

                        Vector3 heightDifferenceLowToHigh = localVerticesSorted[2] - localVerticesSorted[0];
                        Vector3 heightDifferenceMidToHigh = localVerticesSorted[2] - localVerticesSorted[1];

                        float heightRatioMidToHigh = -sortedHeights[1] / (sortedHeights[2] - sortedHeights[1]);
                        float heightRatioLowToHigh = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
        
                        Vector3 LI_L = heightRatioLowToHigh * heightDifferenceLowToHigh; // TODO: Rename. InterpolatedLengthLowToHigh?
                        Vector3 LI_M = heightRatioMidToHigh * heightDifferenceMidToHigh;

                        Vector3 I_L = localVerticesSorted[0] + LI_L;
                        Vector3 I_M = localVerticesSorted[1] + LI_M;
                        Vector3 normal = 
                            triangleNormal * Utils.GetFaceNormal(localVerticesSorted[1], I_M, localVerticesSorted[0]).magnitude;// TODO: Remove, not used
        
                        AppendTriangle(meshData, localVerticesSorted[1], I_M, localVerticesSorted[0], triangleNormal);
        
                        normal = triangleNormal * Utils.GetFaceNormal(localVerticesSorted[0], I_M, I_L).magnitude; // TODO: Remove, not used
        
                        AppendTriangle(meshData, localVerticesSorted[0], I_M, I_L, triangleNormal);

                        data.IntersectionVertices.Add(I_M);
                        data.IntersectionVertices.Add(I_L);
                        break;
                    }
                    case 3: 
                    {
                        Vector3 normal = 
                            triangleNormal * 
                            Utils.GetFaceNormal(verticesLocal[0], verticesLocal[1], verticesLocal[2]).magnitude;// TODO: Remove, not used
        
                        AppendTriangle(meshData, verticesLocal[0], verticesLocal[1], verticesLocal[2], triangleNormal);
        
                        break;
                    }
                }
            }
            data.SubmergedVertices = meshData.Vertices.ToArray();
            data.SubmergedTriangles = meshData.Triangles.ToArray();
            data.SubmergedNormals = meshData.Normals.ToArray();
            //return (meshData.Vertices.ToArray(), meshData.Triangles.ToArray(), meshData.Normals.ToArray());
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

            List<Vector3> currentCentroidsDown = new List<Vector3>();
            List<Vector3> currentCentroidsUp = new List<Vector3>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                float depth = -heights[i/3];
                Vector3[] pointsWorldSpace = new Vector3[] 
                {
                    submersionTransform.TransformPoint(vertices[triangles[i]]),
                    submersionTransform.TransformPoint(vertices[triangles[i + 1]]),
                    submersionTransform.TransformPoint(vertices[triangles[i + 2]])
                };
                
                // Determine if face is pointing up (negative contribution) or down (positive contribution) //TODO: MOVE DOWN
                bool trianglePointingDown = submersionTransform.TransformDirection(normals[i/3]).y < 0;
                
                // Triangle edges projected on horizontal plane (?)
                Vector3 edgeAtoB = pointsWorldSpace[1]-pointsWorldSpace[0]; edgeAtoB.y = 0f;
                Vector3 edgeAtoC = pointsWorldSpace[2]-pointsWorldSpace[0]; edgeAtoC.y = 0f;
                
                // Get the area of the triangle in the horizontal plane
                float projectedArea = 0.5f*Vector3.Cross(edgeAtoB, edgeAtoC).magnitude;
                float volume = projectedArea*depth;
                
                Vector3 centroid = (pointsWorldSpace[0] + pointsWorldSpace[1] + pointsWorldSpace[2]) / 3.0f;//+new Vector3(0f, depth*0.5f, 0)
                centroid += new Vector3(0f, depth * 0.5f, 0);
                
                if (trianglePointingDown) 
                {
                    sumVolumeCenterDown += centroid*volume;
                    totalVolumeDown += volume;
                    currentCentroidsDown.Add(centroid);
                }
                else 
                {
                    sumVolumeCenterUp += centroid*volume;
                    totalVolumeUp += volume;
                    currentCentroidsUp.Add(centroid);
                }
            }
            
            float totalVolume = totalVolumeDown-totalVolumeUp;
            
            if (Math.Abs(totalVolume) < 0.00001f) return (0f, Vector3.zero); //TODO: I have changed these to a comparison with a small number

            centroidsUp = currentCentroidsUp.ToArray();
            centroidsDown = currentCentroidsDown.ToArray();

            centroidUp =    (Math.Abs(totalVolumeUp) < 0.00001f)   ? Vector3.zero : sumVolumeCenterUp/totalVolumeUp;
            centroidDown =  (Math.Abs(totalVolumeDown) < 0.00001f) ? Vector3.zero : sumVolumeCenterDown/totalVolumeDown;
            
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
        
        
        // TODO: Check if this is used
        /// Splits the triangle into two triangles along the horizontal plane.
        /// Results in the waterline of the ship.
        private (Vector3[], Vector3[]) SplitSubmergedTriangleHorizontally(Vector3[] tri) 
        {
            float[] vertexHeights = new float[3] { tri[0].y, tri[1].y, tri[2].y };
            (Vector3[] sortedVerticesWorld, float[] sortedHeights) = SortVerticesAgainstFloats(tri, vertexHeights);
            (Vector3 L, Vector3 M, Vector3 H) = (sortedVerticesWorld[0], sortedVerticesWorld[1], sortedVerticesWorld[2]);

            // Initialize the points for the new triangles
            Vector3 D; 
            Vector3[] upperTriangle;
            Vector3[] lowerTriangle;

            // Check for vertical alignment of L and H
            if (Math.Abs(H.x - L.x) < 1e-6 && Math.Abs(H.z - L.z) < 1e-6) 
            {
                // If LH is approximately vertical
                D = new Vector3(L.x, M.y, L.z);

                upperTriangle = new Vector3[] { H, M, D };
                lowerTriangle = new Vector3[] { L, D, M };
            }
            else 
            {
                // General case
                // Calculate the slope for the LH line segment
                float dx = H.x - L.x;
                float dz = H.z - L.z;
                float dy = H.y - L.y;
                if (dy == 0) {
                    dy = 1e-12f;
                }
                float mX = dx / dy;
                float mZ = dz / dy;

                // Calculate the x and z coordinates of D
                float x = L.x + mX * (M.y - L.y);
                float z = L.z + mZ * (M.y - L.y);
                D = new Vector3(x, M.y, z);

                upperTriangle = new Vector3[] { H, M, D };
                lowerTriangle = new Vector3[] { L, D, M };
            }

            return (upperTriangle, lowerTriangle);
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

        
        // TODO: Not used
        private Vector3 CalculateBuoyancyCenterTopTriangle(Patch patch, Vector3[] triVerts) 
        {
            // takes in a triangle in world coordinates (with a horizontal base) and calculates its center of pressure/buoyancy
            Vector3 A = triVerts[0];
            Vector3 B = triVerts[1];
            Vector3 C = triVerts[2];

            float y0 = -patch.GetPatchRelativeHeight(A);
            float h = A.y - B.y; 
            float tc = (4.0f * y0 + 3.0f * h) / (6.0f * y0 + 4.0f * h);

            //if ((6 * y0 + 4 * h) == 0) {
            //    tc = 0.75f;
            //}
            //tc = 0.75f;

            Vector3 centerBuoyancy = A + tc * ((B + C) / 2.0f - A);
            return centerBuoyancy;
        }


        // TODO: Not used
        private Vector3 CalculateBuoyancyCenterBottomTriangle(Patch patch, Vector3[] triVerts) 
        {
            Vector3 A = triVerts[0];
            Vector3 B = triVerts[1];
            Vector3 C = triVerts[2];

            float y0 = -patch.GetPatchRelativeHeight(B);
            float h = B.y-A.y;
            float tc = (2.0f * y0 + h) / (6.0f * y0 + 2.0f * h);
            //tc = 0.5f;
            Vector3 centerBuoyancy = A + tc * ((B + C) / 2.0f - A);
            return centerBuoyancy;
        }


        // Moved from Patch.cs, not used elsewhere
        /// Sorts the vertices of a triangle by their heights
        /// Indexed from 0 to 2, low to high
        /// Called in GetSubmergedTriangles
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

