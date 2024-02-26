using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System;


namespace WaterInteraction 
{
    public class Patch
    {
        // Used in Submersion.cs
        public Vector3[] patchVertices; 
        public int[] patchTriangles;
        public Mesh baseGridMesh;
        // Water height querying (burst)
        // Input job parameters
        NativeArray<float3> targetPositionBuffer;
        // Output job parameters
        NativeArray<float3> projectedPositionWorldSpaceBuffer; // projectedPositionWSBuffer Is this World Space?
        NativeArray<float3> candidatePositionBuffer;
        NativeArray<float3> directionBuffer;
        NativeArray<int> stepCountBuffer;
        NativeArray<float> errorBuffer;

        // OK Private
        private int numberOfGridPoints;
        private WaterSurface water;
        private Vector3 gridOrigin;
        private float sideLength;
        private int gridFidelity;
        private float cellSize;
        private Vector3[] baseGridMeshVertices;
        private int[] baseGridMeshTriangles;


        /// Populate patch variables and do initialize step
        public Patch(WaterSurface water, float sideLength, int gridFidelity, Vector3 gridOrigin) 
        {
            this.gridFidelity = gridFidelity;
            this.gridOrigin = gridOrigin;
            this.sideLength = sideLength;
            this.water = water; 
            Initialize();
        }
        
        
        /// Call patch builder and initialize buffers
        private void Initialize() 
        {
            cellSize = sideLength / gridFidelity;
            baseGridMesh = ConstructWaterGridMesh();
            baseGridMeshVertices = baseGridMesh.vertices;
            baseGridMeshTriangles = baseGridMesh.triangles;
            patchVertices = baseGridMesh.vertices;
            patchTriangles = baseGridMesh.triangles;
            numberOfGridPoints = baseGridMesh.vertices.Length; 
            // Allocate the buffers
            projectedPositionWorldSpaceBuffer = new NativeArray<float3>(numberOfGridPoints, Allocator.Persistent);
            candidatePositionBuffer = new NativeArray<float3>(numberOfGridPoints, Allocator.Persistent);
            targetPositionBuffer = new NativeArray<float3>(numberOfGridPoints, Allocator.Persistent);
            directionBuffer = new NativeArray<float3>(numberOfGridPoints, Allocator.Persistent);
            stepCountBuffer = new NativeArray<int>(numberOfGridPoints, Allocator.Persistent);
            errorBuffer = new NativeArray<float>(numberOfGridPoints, Allocator.Persistent);
        }
        
        
        /// Burst version. Updates the patch to follow the boat in x and z and queried water height in y.
        public void Update(Transform transform) 
        {
            SetGridOrigin(transform);
            
            WaterSimSearchData simData = new WaterSimSearchData();
            if (!water.FillWaterSearchData(ref simData)) 
            {
                return;
            }
            
            // Update vertex positions
            for (var i = 0; i < patchVertices.Length; i++) 
            {
                float waterHeight = projectedPositionWorldSpaceBuffer[i].y;
                patchVertices[i].x = baseGridMeshVertices[i].x + transform.position.x;
                patchVertices[i].z = baseGridMeshVertices[i].z + transform.position.z;
                patchVertices[i].y = waterHeight;
                targetPositionBuffer[i] = patchVertices[i];
            }

            ExecuteWaterSimulationSearchJob(simData, patchVertices);
            return;
        }
        
        
        /// Run once. Creates a new mesh representing the water patch grid based on parameters from submersion script. 
        private Mesh ConstructWaterGridMesh() 
        {
            Mesh meshOut = new Mesh();
            //float vertexOffset = sideLength / gridFidelity;
            int rows = gridFidelity + 1;
            int columns = gridFidelity + 1;

            Vector3[] vertices = new Vector3[rows * columns];
            int[] triangles = new int[gridFidelity * gridFidelity * 6];

            int triangleIndex = 0; 

            // Populate vertices and triangles arrays
            for (var i = 0; i < rows; i++) 
            {
                for (var j = 0; j < columns; j++) 
                {
                    int vertexIndex = i * columns + j; 
                    vertices[vertexIndex] = gridOrigin + new Vector3(cellSize * j, 0.0f, -cellSize * i);

                    // Skip the last row and column for triangles
                    if (i < rows - 1 && j < columns - 1) 
                    {
                        int topLeft = vertexIndex;
                        int topRight = topLeft + 1;
                        int bottomLeft = topLeft + columns;
                        int bottomRight = bottomLeft + 1;

                        // left triangle
                        triangles[triangleIndex++] = topLeft;
                        triangles[triangleIndex++] = bottomRight;
                        triangles[triangleIndex++] = bottomLeft;

                        // right triangle
                        triangles[triangleIndex++] = topLeft;
                        triangles[triangleIndex++] = topRight;
                        triangles[triangleIndex++] = bottomRight;
                    }
                }
            }

            meshOut.vertices = vertices;
            meshOut.triangles = triangles;
            return meshOut;
        }
        
        
        /// Searches for the water surface using the water simulation data.
        private void ExecuteWaterSimulationSearchJob(WaterSimSearchData simData,Vector3[] vertices)
        {
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
                projectedPositionWSBuffer = projectedPositionWorldSpaceBuffer,
                directionBuffer = directionBuffer,
                stepCountBuffer = stepCountBuffer
            };

            // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
            JobHandle handle = searchJob.Schedule(vertices.Length, 1);
            handle.Complete();
        }
        
        
        /// Move the patch to the boat transforms
        private void SetGridOrigin(Transform transform) 
        {
            float x = -sideLength / 2 + transform.position.x;
            float z = sideLength / 2 + transform.position.z;
            gridOrigin = new Vector3(x, 0, z);
        }

        
        // Called from Submerged.cs
        /// Returns the height of the water surface at the specified point in world space.
        /// By using the patch grid, the water surface height is queried at the specified point.
        /// Run over each point in physics mesh. Called from Submerged.cs/GetSubmergedTriangles()
        public float GetPatchRelativeHeight(Vector3 point) 
        {
            (Vector3, Vector3, Vector3) patchVertices = GetPatchTriangleVerticesWorld(point);
            return GetHeightAboveTriangle(point, patchVertices);
        }
        
        
        /// For the points of the submerged mesh, returns the height above the water surface. 
        private float GetHeightAboveTriangle(Vector3 point, (Vector3, Vector3, Vector3) triangle) 
        {
            Vector3 edgeAtoB = triangle.Item2 - triangle.Item1;
            Vector3 edgeAtoC = triangle.Item3 - triangle.Item1;
            Vector3 triangleNormal = Vector3.Cross(edgeAtoB, edgeAtoC); 
            // The plane equation
            float distance = Vector3.Dot(triangleNormal, triangle.Item1); 
            //if (abc.y == 0.0f) - Old line
            if (Math.Abs(triangleNormal.y) < 0.00001f) 
                Console.WriteLine("Division by zero when calculating height of point above triangle plane!");

            float heightAboveTriangle = (distance - triangleNormal.x * point.x - triangleNormal.z * point.z);
            heightAboveTriangle /= triangleNormal.y;
            heightAboveTriangle = point.y - heightAboveTriangle;
            return heightAboveTriangle;
            //return (point.y - (distance - triangleNormal.x * point.x - triangleNormal.z * point.z) / triangleNormal.y); - Old Line
        }
        
        
        /// Returns the world space vertices of the triangle in which the point lies.
        private (Vector3, Vector3, Vector3) GetPatchTriangleVerticesWorld(Vector3 point) 
        {
            (int rowCell, int columnCell) = PointToCell(point);
            float xInCell = (point.x - gridOrigin.x) - cellSize * columnCell;
            float zInCell = (point.z - gridOrigin.z) - (cellSize * (-rowCell));
            bool left = (xInCell >= -zInCell); 
            return GetPatchTriangleVertices(rowCell, columnCell, left);
        }
        
        
        /// Returns the row and column indices of the cell in which the point lies.
        private (int, int) PointToCell(Vector3 point) 
        {
            Vector3 adjustedPoint = point - gridOrigin;
            int rowIndex = Mathf.FloorToInt(-adjustedPoint.z / cellSize);
            int columnIndex = Mathf.FloorToInt(adjustedPoint.x / cellSize);
            return (rowIndex, columnIndex);
        }
        
        
        /// Returns the vertices of the triangle in the patch grid at the specified row and column indices.
        /// Depending on the value of the left parameter, the left or right triangle is returned.
        private (Vector3, Vector3, Vector3) GetPatchTriangleVertices(int rowIndex, int columnIndex, bool left)
        {
            int numberOfVertices = gridFidelity + 1; 
            Vector3 topLeftVertex = patchVertices[rowIndex * numberOfVertices + columnIndex];
            Vector3 bottomRightVertex = patchVertices[(rowIndex + 1) * numberOfVertices + (columnIndex + 1)];

            if (left)
            {
                Vector3 bottomLeftVertex = patchVertices[(rowIndex + 1) * numberOfVertices + columnIndex];
                return (topLeftVertex, bottomLeftVertex, bottomRightVertex); // Left triangle
            }
            else
            {
                Vector3 topRightVertex = patchVertices[rowIndex * numberOfVertices + (columnIndex + 1)];
                return (topLeftVertex, bottomRightVertex, topRightVertex); // Right triangle
            }
        }
        
        
        public void Dispose()
        {
            DisposeRoutine();
        }

        
        /// Dispose of the buffers.
        private void DisposeRoutine() 
        {
            projectedPositionWorldSpaceBuffer.Dispose();
            candidatePositionBuffer.Dispose();
            targetPositionBuffer.Dispose();
            directionBuffer.Dispose();
            stepCountBuffer.Dispose();
            errorBuffer.Dispose();
        }
    }
}