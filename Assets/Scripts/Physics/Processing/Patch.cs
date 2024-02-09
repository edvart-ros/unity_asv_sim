using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WaterInteraction
{
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
            if (abc.y == 0.0f) Debug.LogError("Division by zero when calculating height of point above triangle plane!");
            return (point.y - (d - abc.x * point.x - abc.z * point.z) / abc.y);
        }

        public Vector3[] GetPatchTriangleVerticesWorld(Vector3 point) {
            (int iCell, int jCell) = PointToCell(point);
            float xInCell = (point.x - gridOrigin.x) - cellSize * jCell;
            float zInCell = (point.z - gridOrigin.z) - (cellSize * (-iCell));
            return (xInCell >= -zInCell) ? GetPatchRightTriangleVertices(iCell, jCell) : GetPatchLeftTriangleVertices(iCell, jCell);
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
}