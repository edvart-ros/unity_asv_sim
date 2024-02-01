using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine.UIElements;

namespace WaterInteraction {
    public static class Constants {
        public const float g = 9.80665f;
        public const float rho = 0.5f * 997;
        public const float waterViscosity = 1.0016f;
    }
    public class Patch {

        public WaterSurface water;
        public float sideLength;
        public float cellSize;
        public int gridFidelity;
        public Vector3 gridOrigin;


        public int numGridPoints;
        public Vector3[] patchVertices;
        public Mesh baseGridMesh;
        public Patch(WaterSurface water, float sideLength, int gridFidelity, Vector3 gridOrigin) {
            this.water = water;
            this.sideLength = sideLength;
            this.gridFidelity = gridFidelity;
            this.gridOrigin = gridOrigin;
            Initialize();
        }

        // water height querying (burst)
        // Input job parameters
        NativeArray<float3> targetPositionBuffer;
        // Output job parameters
        NativeArray<float> errorBuffer;
        NativeArray<float3> candidatePositionBuffer;
        NativeArray<float3> projectedPositionWSBuffer;
        NativeArray<float3> directionBuffer;
        NativeArray<int> stepCountBuffer;


        public void Initialize() {
            cellSize = sideLength / gridFidelity;
            baseGridMesh = ConstructWaterGridMesh();
            numGridPoints = baseGridMesh.vertices.Length;
            // Allocate the buffers
            targetPositionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            errorBuffer = new NativeArray<float>(numGridPoints, Allocator.Persistent);
            candidatePositionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            projectedPositionWSBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            directionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            stepCountBuffer = new NativeArray<int>(numGridPoints, Allocator.Persistent);
        }


        public Mesh ConstructWaterGridMesh() {
            // get the position of the game object
            Mesh meshOut = new Mesh();
            float vertexOffset = sideLength / gridFidelity;
            int rows = gridFidelity + 1;
            int cols = gridFidelity + 1;

            Vector3[] vertices = new Vector3[rows * cols];
            int[] triangles = new int[(rows - 1) * (cols - 1) * 6];

            for (var i = 0; i < rows; i++) {
                for (var j = 0; j < cols; j++) {
                    int vertexIndex = i * cols + j;
                    Vector3 currentVertex = gridOrigin + new Vector3(vertexOffset * j, (float)0.0, -vertexOffset * i);
                    vertices[vertexIndex] = currentVertex;
                }
            }
            for (var i = 0; i < rows - 1; i++) {
                for (var j = 0; j < cols - 1; j++) {
                    int idxOffset = ((i * (cols - 1)) + j) * 6;

                    int topLeft = i * cols + j;
                    int topRight = i * cols + (j + 1);
                    int bottomLeft = (i + 1) * cols + j;
                    int bottomRight = (i + 1) * cols + (j + 1);

                    // left triangle
                    triangles[idxOffset + 0] = topLeft;
                    triangles[idxOffset + 1] = bottomRight;
                    triangles[idxOffset + 2] = bottomLeft;
                    // right triangle
                    triangles[idxOffset + 3] = topLeft;
                    triangles[idxOffset + 4] = topRight;
                    triangles[idxOffset + 5] = bottomRight;
                }
            }
            meshOut.vertices = vertices;
            meshOut.triangles = triangles;
            return meshOut;
        }

        public int GetNumberOfSubmergedVerts(Vector3[] verts, Transform t) {
            int numSubmerged = 0;
            foreach (var v in verts) {
                if (GetPatchRelativeHeight(v) < 0) {
                    numSubmerged++;
                }
            }
            return numSubmerged;
        }

        public (int, int) PointToCell(Vector3 point) {
            Vector3 adjustedPoint = point - gridOrigin;
            int i = Mathf.FloorToInt(-adjustedPoint.z / cellSize);
            int j = Mathf.FloorToInt(adjustedPoint.x / cellSize);
            return (i, j);
        }


        public Vector3[] GetPatchLeftTriangleVertices(int i, int j) {
            int n = gridFidelity + 1;
            Vector3 topLeftVertex = patchVertices[i * n + j];
            Vector3 bottomLeftVertex = patchVertices[(i + 1) * n + j];
            Vector3 bottomRightVertex = patchVertices[(i + 1) * n + (j + 1)];
            return new Vector3[] { topLeftVertex, bottomLeftVertex, bottomRightVertex };

        }

        public Vector3[] GetPatchRightTriangleVertices(int i, int j) {
            int n = gridFidelity + 1;
            Vector3 topLeftVertex = patchVertices[i * n + j];
            Vector3 topRightVertex = patchVertices[i * n + (j + 1)];
            Vector3 bottomRightVertex = patchVertices[(i + 1) * n + (j + 1)];
            return new Vector3[] { topLeftVertex, topRightVertex, bottomRightVertex };
        }

        public float GetHeightAboveTriangle(Vector3 point, Vector3[] triangle) {
            Vector3 AB = triangle[1] - triangle[0];
            Vector3 AC = triangle[2] - triangle[0];
            Vector3 abc = Vector3.Cross(AB, AC);
            float d = Vector3.Dot(abc, triangle[0]);
            return (point.y - (d - abc.x * point.x - abc.z * point.z) / abc.y);
        }

        public Vector3[] GetPatchTriangleVerticesWorld(Vector3 point) {
            (int iCell, int jCell) = PointToCell(point);
            float xInCell = (point.x - gridOrigin.x) - cellSize * jCell;
            float zInCell = (point.z - gridOrigin.z) - (cellSize * (-iCell));
            if (xInCell >= -zInCell) {
                return GetPatchRightTriangleVertices(iCell, jCell);
            }
            else {
                return GetPatchLeftTriangleVertices(iCell, jCell);
            }
        }

        public float GetPatchRelativeHeight(Vector3 point) {
            Vector3[] patchVerts = GetPatchTriangleVerticesWorld(point);
            return GetHeightAboveTriangle(point, patchVerts);
        }





        public static (Vector3[], float[]) SortVerticesAgainstFloats(Vector3[] vertices, float[] heights) {
            void Swap<T>(ref T a, ref T b) {
                T temp = a;
                a = b;
                b = temp;
            }

            if (heights[0] > heights[1]) {
                Swap(ref heights[0], ref heights[1]);
                Swap(ref vertices[0], ref vertices[1]);
            }

            if (heights[1] > heights[2]) {
                Swap(ref heights[1], ref heights[2]);
                Swap(ref vertices[1], ref vertices[2]);
            }

            if (heights[0] > heights[1]) {
                Swap(ref heights[0], ref heights[1]);
                Swap(ref vertices[0], ref vertices[1]);
            }

            return (vertices, heights);
        }




        private float CalculateMeshArea(Mesh mesh) {
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
        private float[] CalculateTriangleAreas(Mesh mesh) {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            int L = triangles.Length;
            float triangleArea;
            List<float> triangleAreas = new List<float>(L);
            for (int i = 0; i < L - 2; i += 3) {
                Vector3 v1 = vertices[triangles[i + 1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 2]] - vertices[triangles[i]];
                triangleArea = 0.5f * (Vector3.Cross(v1, v2)).magnitude;
                triangleAreas.Add(triangleArea);
            }
            return triangleAreas.ToArray();
        }


        // //burst version
        public void Update(Transform transform) {
            gridOrigin = new Vector3(-sideLength / 2 + transform.position.x, 0, sideLength / 2 + transform.position.z);
            Vector3[] vertices = baseGridMesh.vertices;
            for (var i = 0; i < vertices.Length; i++) {
                targetPositionBuffer[i] = transform.position + vertices[i];
                vertices[i].x += transform.position.x;
                vertices[i].z += transform.position.z;
            }

            WaterSimSearchData simData = new WaterSimSearchData();
            if (!water.FillWaterSearchData(ref simData)) {
                patchVertices = vertices;
                return;
            }

            // Prepare the first band
            WaterSimulationSearchJob searchJob = new WaterSimulationSearchJob
            {
                // Assign the simulation data
                simSearchData = simData,
                // Fill the input data
                targetPositionWSBuffer = targetPositionBuffer,
                startPositionWSBuffer = targetPositionBuffer,
                maxIterations = 8,
                error = 0.01f,
                includeDeformation = true,
                excludeSimulation = false,

                errorBuffer = errorBuffer,
                candidateLocationWSBuffer = candidatePositionBuffer,
                projectedPositionWSBuffer = projectedPositionWSBuffer,
                directionBuffer = directionBuffer,
                stepCountBuffer = stepCountBuffer
            };
            // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
            JobHandle handle = searchJob.Schedule(vertices.Length, 1);
            handle.Complete();

            // update vertex positions
            for (var i = 0; i < vertices.Length; i++) {
                float waterHeight = projectedPositionWSBuffer[i].y;
                vertices[i].y = waterHeight;
            }
            patchVertices = vertices;
            return;
        }

        public void DisposeRoutine() {
            targetPositionBuffer.Dispose();
            errorBuffer.Dispose();
            candidatePositionBuffer.Dispose();
            projectedPositionWSBuffer.Dispose();
            directionBuffer.Dispose();
            stepCountBuffer.Dispose();
        }


    }
    public class Submerged {
        public Mesh hullMesh;
        public Mesh mesh = new Mesh();
        float[] triangleAreas;
        public Vector3[] FaceNormalsWorld = new Vector3[0];
        public Vector3[] FaceCentersWorld = new Vector3[0];
        public float[] FaceCenterHeightsAboveWater;
        private int L;
        public Submerged(Mesh simplifiedHullMesh) {
            hullMesh = simplifiedHullMesh;
            L = hullMesh.vertices.Length;
        }

        public void Update(Patch patch, Transform t) {
            mesh.Clear();
            // cache hull vertices and wave field approximation vertices
            Vector3[] hullVerts = hullMesh.vertices;
            int[] hullTris = hullMesh.triangles;

            // get the set of completely submerged triangles
            (Vector3[] subVerts, int[] subTris) = GetSubmergedTriangles(patch, t, hullVerts, hullTris);
            // split these into vertical and horizontal triangles
            (subVerts, subTris) = SplitTrianglesHorizontally(subVerts, subTris, t);
            triangleAreas = GetTriangleAreas(subVerts);

            FaceCenterHeightsAboveWater = GetTriangleCenterHeights(patch, t, subVerts, subTris);
            mesh.vertices = subVerts;
            mesh.triangles = subTris;

        }



        public (Vector3[], int[]) GetSubmergedTriangles(Patch patch, Transform t, Vector3[] bodyVerts, int[] bodyTris) {
            List<Vector3> vertsOut = new List<Vector3>();
            List<int> trisOut = new List<int>();

            // loop through input triangles
            for (int i = 0; i < bodyTris.Length - 2; i += 3) {
                Vector3[] vertsL = new Vector3[3];
                Vector3[] vertsW = new Vector3[3];
                float[] vertHeights = new float[3];
                int submCount = 0;

                // get the local and world positions of the current triangle, compute depth, track number of submerged verts in triangle
                for (int j = 0; j < 3; j++) {
                    vertsL[j] = bodyVerts[bodyTris[i + j]];
                    vertsW[j] = t.TransformPoint(vertsL[j]);
                    float height = patch.GetPatchRelativeHeight(vertsW[j]);
                    vertHeights[j] = height;
                    if (height < 0) submCount++; // depth > 0 == submerged point
                }


                // how many vertices are underwater?
                switch (submCount) {
                    case 0: {
                            break;
                        }
                    case 1: {
                            (Vector3[] sortedVertsL, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(vertsL, vertHeights);
                            Vector3 LH = sortedVertsL[2] - sortedVertsL[0];
                            Vector3 LM = sortedVertsL[1] - sortedVertsL[0];

                            float t_M = -sortedHeights[0] / (sortedHeights[1] - sortedHeights[0]);
                            float t_H = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                            Vector3 LJ_H = t_H * LH;
                            Vector3 LJ_M = t_M * LM;

                            Vector3 J_H = sortedVertsL[0] + LJ_H;
                            Vector3 J_M = sortedVertsL[0] + LJ_M;
                            AppendTriangle(ref vertsOut, ref trisOut, sortedVertsL[0], J_H, J_M);

                            break;
                        }
                    case 2: {
                            (Vector3[] sortedVertsL, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(vertsL, vertHeights);

                            Vector3 LH = sortedVertsL[2] - sortedVertsL[0];
                            Vector3 MH = sortedVertsL[2] - sortedVertsL[1];

                            float t_M = -sortedHeights[1] / (sortedHeights[2] - sortedHeights[1]);
                            float t_L = -sortedHeights[0] / (sortedHeights[2] - sortedHeights[0]);
                            Vector3 LI_L = t_L * LH;
                            Vector3 LI_M = t_M * MH;

                            Vector3 I_L = sortedVertsL[0] + LI_L;
                            Vector3 I_M = sortedVertsL[1] + LI_M;
                            AppendTriangle(ref vertsOut, ref trisOut, sortedVertsL[1], I_M, sortedVertsL[0]);
                            AppendTriangle(ref vertsOut, ref trisOut, sortedVertsL[0], I_M, I_L);

                            break;
                        }
                    case 3:
                        AppendTriangle(ref vertsOut, ref trisOut, vertsL[0], vertsL[1], vertsL[2]);
                        break;


                }
            }
            return (vertsOut.ToArray(), trisOut.ToArray());
        }

        public (Vector3[], int[]) SplitTrianglesHorizontally(Vector3[] verts, int[] tris, Transform t) {
            List<Vector3> subVerts = new List<Vector3>(tris.Length*2);
            List<int> subTris = new List<int>(tris.Length*2);
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3[] vertsW = new Vector3[3];
                for (int j = 0; j < 3; j++) vertsW[j] = t.TransformPoint(verts[tris[i + j]]);
                (Vector3[] topTri, Vector3[] botTri) = SplitSubmergedTriangleHorizontally(vertsW);
                AppendTriangle(ref subVerts, ref subTris, t.InverseTransformPoint(topTri[0]),t.InverseTransformPoint(topTri[1]), t.InverseTransformPoint(topTri[2]));
                AppendTriangle(ref subVerts, ref subTris, t.InverseTransformPoint(botTri[0]), t.InverseTransformPoint(botTri[1]), t.InverseTransformPoint(botTri[2]));
            }
            return (subVerts.ToArray(), subTris.ToArray());
        }

        public Vector3[] GetPressureCenters(Patch patch, Transform t, Vector3[] verts, int[] tris) {
            // TODO
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3[] vertsL = new Vector3[3];
                Vector3[] vertsW = new Vector3[3];

                for (int j = 0; j < 3; j++) vertsW[j] = t.TransformPoint(verts[tris[i + j]]);
            }
            return new Vector3[0];
        }


        public float[] GetTriangleCenterHeights(Patch patch, Transform t, Vector3[] verts, int[] tris) {
            float[] heights = new float[verts.Length / 3];
            for (int i = 0; i < tris.Length - 2; i += 3) {
                Vector3 centerVert = (verts[tris[i]] + verts[tris[i+1]] + verts[tris[i+2]])/3;
                heights[i/3] = patch.GetPatchRelativeHeight(t.TransformPoint(centerVert));
            }
            return heights;
        }

        private (Vector3[], Vector3[]) SplitSubmergedTriangleHorizontally(Vector3[] tri) {
            float[] vertexHeights = new float[3] { tri[0].y, tri[1].y, tri[2].y };
            (Vector3[] sortedVerticesWorld, float[] sortedHeights) = Patch.SortVerticesAgainstFloats(tri, vertexHeights);
            (Vector3 L, Vector3 M, Vector3 H) = (sortedVerticesWorld[0], sortedVerticesWorld[1], sortedVerticesWorld[2]);

            // Initialize the points for the new triangles
            Vector3 D;
            Vector3[] upperTriangle;
            Vector3[] lowerTriangle;

            // Check for vertical alignment of L and H
            if (Math.Abs(H.x - L.x) < 1e-6 && Math.Abs(H.z - L.z) < 1e-6) {
                // If LH is approximately vertical
                D = new Vector3(L.x, M.y, L.z);

                upperTriangle = new Vector3[] { H, M, D };
                lowerTriangle = new Vector3[] { L, D, M };
            }
            else {
                // General case
                // Calculate the slope for the LH line segment
                float dx = H.x - L.x;
                float dz = H.z - L.z;
                float dy = H.y - L.y;
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
        public float[] GetTriangleAreas(Vector3[] verts) {
            float[] areas = new float[verts.Length/3];
            for (int i = 0; i < verts.Length-2; i+=3) {
                Vector3 n = Utils.GetFaceNormal(verts[0], verts[1], verts[2]);
                areas[i / 3] = n.magnitude;
            }
            return areas;
        }


        public void AppendTriangle(ref List<Vector3> verts, ref List<int> tris, Vector3 v1, Vector3 v2, Vector3 v3) {
            int count = verts.Count;
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);
            tris.Add(count);
            tris.Add(count + 1);
            tris.Add(count + 2);
        }


        public float GetResistanceCoefficient(float speed, float hullZmin, float hullZmax) {
            float submergedArea = Utils.CalculateMeshArea(mesh);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float Rn = CalculateReynoldsNumber(speed, Math.Abs(hullZmax - hullZmin));

            float onePlusK = 0;
            for (int i = 0; i < triangles.Length - 2; i += 3) {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                float Si = (0.5f) * Vector3.Cross((v1 - v0), (v2 - v0)).magnitude;
                float Ki = GetTriangleK((v0.z + v1.z + v2.z) / 3, hullZmin, hullZmax);
                onePlusK += (1 + Ki) * Si;
            }
            onePlusK = Mathf.Clamp(onePlusK / submergedArea, 1.22f, 1.65f);
            float Cf = 0.075f / ((Mathf.Log10(Rn) - 2) * (Mathf.Log10(Rn) - 2));
            float Cfr = onePlusK * Cf;
            return Cfr;
        }

        private float CalculateReynoldsNumber(float velocity, float L, float viscosity = Constants.waterViscosity) {
            return (velocity * L) / viscosity;
        }


        private float GetTriangleK(float z, float hullZmin, float hullZmax) {
            float f = (-3 / (hullZmax - hullZmin)) * z + 3 * hullZmax / (hullZmax - hullZmin) - 1;
            return f;
        }

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
        public static Vector3 BuoyancyForce(float height, Vector3 normal) {
            Vector3 F = Constants.rho * Constants.g * height * normal;
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);
            return FVertical;
        }
    }
}