using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace WaterInteraction{
    public static class Constants{
        public const float g = 9.80665f;
        public const float rho = 0.5f*997;
        public const float waterViscosity = 1.0016f;
    }
    public class Patch{

        public WaterSurface water;
        public float sideLength;
        public float cellSize;
        public int gridFidelity;
        public Vector3 gridOrigin;
        
        
        public int numGridPoints;
        public Vector3[] patchVertices;
        public Mesh gridMesh;
        public Patch(WaterSurface water, float sideLength, int gridFidelity, Vector3 gridOrigin)
        {
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


        public void Initialize(){
            cellSize = sideLength / gridFidelity;
            gridMesh = ConstructWaterGridMesh();
            numGridPoints = gridMesh.vertices.Length;
            // Allocate the buffers
            targetPositionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            errorBuffer = new NativeArray<float>(numGridPoints, Allocator.Persistent);
            candidatePositionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            projectedPositionWSBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            directionBuffer = new NativeArray<float3>(numGridPoints, Allocator.Persistent);
            stepCountBuffer = new NativeArray<int>(numGridPoints, Allocator.Persistent);
        }


        public Mesh ConstructWaterGridMesh(){
            // get the position of the game object
            Mesh meshOut = new Mesh();
            float vertexOffset = sideLength / gridFidelity;
            int rows = gridFidelity + 1;
            int cols = gridFidelity + 1;
            
            Vector3[] vertices = new Vector3[rows*cols];
            int[] triangles = new int[(rows-1)*(cols-1)*6];
            
            for (var i = 0; i < rows; i++){
                for (var j = 0; j < cols; j++){
                    int vertexIndex = i*cols + j;
                    Vector3 currentVertex = gridOrigin + new Vector3(vertexOffset*j, (float)0.0, -vertexOffset*i);
                    vertices[vertexIndex] = currentVertex;
                }
            }
            for (var i = 0; i < rows-1; i++){
                for (var j = 0; j < cols-1; j++){
                    int idxOffset = ((i*(cols-1)) + j)*6;

                    int topLeft     = i*cols     + j;
                    int topRight    = i*cols     + (j+1);
                    int bottomLeft  = (i+1)*cols + j;
                    int bottomRight = (i+1)*cols + (j+1);

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

        public (int, int) PointToCell(Vector3 point)
        {
            Vector3 adjustedPoint = point - gridOrigin;
            int i = Mathf.FloorToInt(-adjustedPoint.z / cellSize);
            int j = Mathf.FloorToInt(adjustedPoint.x / cellSize);
            return new (i, j);
        }


        public Vector3[] GetPatchLeftTriangleVertices(int i, int j){
            int n = gridFidelity + 1;
            Vector3 topLeftVertex = patchVertices[i*n + j];
            Vector3 bottomLeftVertex = patchVertices[(i+1)*n+ j];
            Vector3 bottomRightVertex = patchVertices[(i+1)*n + (j+1)];
            return new Vector3[] {topLeftVertex, bottomLeftVertex, bottomRightVertex};
        }

        public Vector3[] GetPatchRightTriangleVertices(int i, int j){
            int n = gridFidelity + 1;
            Vector3 topLeftVertex = patchVertices[i*n + j];
            Vector3 topRightVertex = patchVertices[i*n + (j+1)];
            Vector3 bottomRightVertex = patchVertices[(i+1)*n + (j+1)];
            return new Vector3[] {topLeftVertex, topRightVertex, bottomRightVertex};
        }

        public float GetHeightAboveTriangle(Vector3 point, Vector3[] triangle){
            Vector3 AB = triangle[1]-triangle[0];
            Vector3 AC = triangle[2]-triangle[0];
            Vector3 abc = Vector3.Cross(AB, AC);
            float d = Vector3.Dot(abc, triangle[0]);
            return (point.y - (d - abc.x*point.x - abc.z*point.z)/(abc.y));
        }

        public (Vector3[], float[]) SortVerticesAgainstWaterHeight(Vector3[] vertices, float[] heights) {
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




        private float CalculateMeshArea(Mesh mesh){
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            float totalArea = 0.0f;
            for (int i = 0; i < triangles.Length - 2; i += 3){
                Vector3 v1 = vertices[triangles[i+1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i+2]] - vertices[triangles[i]];
                Vector3 cross = Vector3.Cross(v1, v2);
                totalArea += 0.5f*cross.magnitude;            
            }
            return totalArea;
        }
        private float[] CalculateTriangleAreas(Mesh mesh){
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            int L = triangles.Length;
            float triangleArea;
            List<float> triangleAreas = new List<float>(L); 
            for (int i = 0; i < L - 2; i += 3){
                Vector3 v1 = vertices[triangles[i+1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i+2]] - vertices[triangles[i]];
                triangleArea = 0.5f*(Vector3.Cross(v1, v2)).magnitude;
                triangleAreas.Add(triangleArea);
            }
            return triangleAreas.ToArray();
        }


        // //burst version
        public void Update(Transform transform)
        {
            gridOrigin = new Vector3(-sideLength / 2 + transform.position.x, 0, sideLength / 2 + transform.position.z);
            Vector3[] vertices = gridMesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                targetPositionBuffer[i] = transform.position + vertex;
                vertex.x += transform.position.x;
                vertex.z += transform.position.z;
                vertices[i] = vertex;
            }

            WaterSimSearchData simData = new WaterSimSearchData();
            if (!water.FillWaterSearchData(ref simData)){
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
            for (var i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                float waterHeight = projectedPositionWSBuffer[i].y;
                vertex.y = waterHeight;
                vertices[i] = vertex;
            }
            patchVertices = vertices;
            return;
        }

        public void DisposeRoutine(){
            targetPositionBuffer.Dispose();
            errorBuffer.Dispose();
            candidatePositionBuffer.Dispose();
            projectedPositionWSBuffer.Dispose();
            directionBuffer.Dispose();
            stepCountBuffer.Dispose();
        }


    }
    public class Submerged{
        public Mesh hullMesh;
        public Mesh mesh = new Mesh();
        float[] triangleAreas;
        public Vector3[] FaceNormalsWorld;
        public Vector3[] FaceCentersWorld;
        public float[] FaceCenterHeightsAboveWater;

        public Submerged(Mesh simplifiedHullMesh){
            hullMesh = simplifiedHullMesh;
        }

        public void Update(Patch patch, Transform transform){
            mesh.Clear();
            // For storing the new resulting submerged mesh
            int L = hullMesh.vertices.Length;
            List<Vector3> submergedVerticesLocal = new List<Vector3>(L); // Upper bound = vertices in the hull mesh
            List<int> submergedTrianglesLocal = new List<int>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
            List<Vector3> submergedNormalsWorld = new List<Vector3>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
            List<Vector3> submergedCentersWorld = new List<Vector3>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
            List<float> submergedCenterHeightsAboveWater = new List<float>(L * 2); // Upper bound = twice the number of triangles in the hull mesh

            float[] hullSubmergedAreas = new float[L];

            int numTriangles = hullMesh.triangles.Length;
            Vector3[] patchVerticesCache = patch.patchVertices;
            Vector3[] hullVerticesCache = hullMesh.vertices;
            int[] hullTrianglesCache = hullMesh.triangles;
            for (var i = 0; i < numTriangles-2; i += 3){
                Vector3[] triangleVerticesLocal = new Vector3[] {
                    hullVerticesCache[hullTrianglesCache[i]], 
                    hullVerticesCache[hullTrianglesCache[i+1]], 
                    hullVerticesCache[hullTrianglesCache[i+2]]
                };
                Vector3[] triangleVerticesWorld = new Vector3[] {
                    transform.TransformPoint(triangleVerticesLocal[0]),
                    transform.TransformPoint(triangleVerticesLocal[1]),
                    transform.TransformPoint(triangleVerticesLocal[2])
                };
                Vector3[] patchTriangleVerticesWorld = new Vector3[3];
                Vector3 originalFaceNormalWorld = Utils.GetFaceNormal(triangleVerticesWorld[0], triangleVerticesWorld[1], triangleVerticesWorld[2]);
                float[] vertexHeights = new float[3];

                for (var j = 0; j < 3; j++){
                    Vector3 vertexLocal = triangleVerticesLocal[j];
                    Vector3 vertexWorld = transform.TransformPoint(vertexLocal);
                    (int iCell, int jCell) = patch.PointToCell(vertexWorld);
                    float xInCell = (vertexWorld.x - patch.gridOrigin.x)-patch.cellSize*jCell;
                    float zInCell = (vertexWorld.z - patch.gridOrigin.z)-(patch.cellSize*(-iCell));
                    if (xInCell >= -zInCell){
                        patchTriangleVerticesWorld = patch.GetPatchRightTriangleVertices(iCell, jCell);
                    }
                    else{
                        patchTriangleVerticesWorld = patch.GetPatchLeftTriangleVertices(iCell, jCell);
                    }
                    vertexHeights[j] = patch.GetHeightAboveTriangle(vertexWorld, patchTriangleVerticesWorld);
                }

                // check whether the triangle has 3, 2, 1 or 0 submerged triangles
                int submergedCount = 0;
                int numExistingVertices = submergedVerticesLocal.Count;
                Vector3 centerWorld;
                Vector3 faceNormalWorld;
                if (vertexHeights[0] < 0) submergedCount ++;
                if (vertexHeights[1] < 0) submergedCount ++;
                if (vertexHeights[2] < 0) submergedCount ++;
                switch(submergedCount){
                    case 0:
                        break;
                    case 1: {
                        //sort the triangles, L, M, H from lowest to highest
                        (Vector3[] sortedVerticesLocal, float[] sortedHeights) = patch.SortVerticesAgainstWaterHeight(triangleVerticesLocal, vertexHeights);
                        Vector3 LH = sortedVerticesLocal[2] - sortedVerticesLocal[0];
                        Vector3 LM = sortedVerticesLocal[1] - sortedVerticesLocal[0];

                        float t_M = -sortedHeights[0]/(sortedHeights[1]-sortedHeights[0]);
                        float t_H = -sortedHeights[0]/(sortedHeights[2]-sortedHeights[0]);
                        //approximate where the water cuts the triangle (along LM and LH)
                        Vector3 LJ_H = t_H*LH;
                        Vector3 LJ_M = t_M*LM;

                        Vector3 J_H = sortedVerticesLocal[0] + LJ_H;
                        Vector3 J_M = sortedVerticesLocal[0] + LJ_M;

                        submergedVerticesLocal.Add(sortedVerticesLocal[0]);
                        submergedVerticesLocal.Add(J_H);
                        submergedVerticesLocal.Add(J_M);

                        submergedTrianglesLocal.Add(numExistingVertices);
                        submergedTrianglesLocal.Add(numExistingVertices+1);
                        submergedTrianglesLocal.Add(numExistingVertices+2);

                        centerWorld = transform.TransformPoint((sortedVerticesLocal[0] + J_H + J_M)/3);
                        faceNormalWorld = Utils.GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                        transform.TransformPoint(J_H),
                                                        transform.TransformPoint(J_M));
                        faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;
                        hullSubmergedAreas[i/3] = faceNormalWorld.magnitude/2;
                        submergedNormalsWorld.Add(faceNormalWorld);
                        submergedCentersWorld.Add(centerWorld);
                        submergedCenterHeightsAboveWater.Add(patch.GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                        break;
                    }
                    case 2: {
                        //sort the triangles, L, M, H from lowest to highest
                        (Vector3[] sortedVerticesLocal, float[] sortedHeights) = patch.SortVerticesAgainstWaterHeight(triangleVerticesLocal, vertexHeights);
                        Vector3 LH = sortedVerticesLocal[2] - sortedVerticesLocal[0];
                        Vector3 MH = sortedVerticesLocal[2] - sortedVerticesLocal[1];

                        //approximate parameters t_M and t_L, which approximate how far the submerged parts of the traingel edges extend
                        // LI_L ~= t_L*LH, where LI_L is the vector from the lowest vertex to the estimated interesection of the water and LH
                        float t_M = -sortedHeights[1]/(sortedHeights[2]-sortedHeights[1]);
                        float t_L = -sortedHeights[0]/(sortedHeights[2]-sortedHeights[0]);
                        Vector3 LI_L = t_L*LH;
                        Vector3 LI_M = t_M*MH;

                        Vector3 I_L = sortedVerticesLocal[0] + LI_L;
                        Vector3 I_M = sortedVerticesLocal[1] + LI_M;

                        submergedVerticesLocal.Add(sortedVerticesLocal[1]);
                        submergedVerticesLocal.Add(I_M);
                        submergedVerticesLocal.Add(sortedVerticesLocal[0]);
                        centerWorld = transform.TransformPoint((sortedVerticesLocal[1] + I_M + sortedVerticesLocal[0])/3);
                        faceNormalWorld = Utils.GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                        transform.TransformPoint(I_M),
                                                        transform.TransformPoint(sortedVerticesLocal[1]));
                        faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;

                        hullSubmergedAreas[i/3] = faceNormalWorld.magnitude/2;
                        submergedNormalsWorld.Add(faceNormalWorld);
                        submergedCentersWorld.Add(centerWorld);
                        submergedCenterHeightsAboveWater.Add(patch.GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                        
                        submergedVerticesLocal.Add(sortedVerticesLocal[0]);
                        submergedVerticesLocal.Add(I_M);
                        submergedVerticesLocal.Add(I_L);
                        centerWorld = transform.TransformPoint((sortedVerticesLocal[0] + I_M + I_L)/3);
                        faceNormalWorld = Utils.GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                        transform.TransformPoint(I_M),
                                                        transform.TransformPoint(I_L));
                        faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;
                        hullSubmergedAreas[i/3] += faceNormalWorld.magnitude/2;
                        submergedNormalsWorld.Add(faceNormalWorld);
                        submergedCentersWorld.Add(centerWorld);
                        submergedCenterHeightsAboveWater.Add(patch.GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));


                        for (int k = 0; k < 6; k++){
                            submergedTrianglesLocal.Add(numExistingVertices+k);
                        }
                        break;
                    }
                    case 3: {
                        (Vector3 A, Vector3 B, Vector3 C) = (triangleVerticesLocal[0], 
                                                            triangleVerticesLocal[1], 
                                                            triangleVerticesLocal[2]);
                        submergedVerticesLocal.Add(A);
                        submergedVerticesLocal.Add(B);
                        submergedVerticesLocal.Add(C);
                        centerWorld = transform.TransformPoint((A + B + C)/3);
                        submergedNormalsWorld.Add(originalFaceNormalWorld);
                        submergedCentersWorld.Add(centerWorld);
                        submergedCenterHeightsAboveWater.Add(patch.GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                        submergedTrianglesLocal.Add(numExistingVertices);
                        submergedTrianglesLocal.Add(numExistingVertices+1);
                        submergedTrianglesLocal.Add(numExistingVertices+2);
                        hullSubmergedAreas[i/3] = originalFaceNormalWorld.magnitude/2;
                        break;
                    }
                }
            }
            triangleAreas = hullSubmergedAreas;
            FaceNormalsWorld = submergedNormalsWorld.ToArray();
            FaceCentersWorld = submergedCentersWorld.ToArray();
            FaceCenterHeightsAboveWater = submergedCenterHeightsAboveWater.ToArray();
            mesh.vertices = submergedVerticesLocal.ToArray();
            mesh.triangles = submergedTrianglesLocal.ToArray();
        }

    public float GetResistanceCoefficient(float speed, float hullZmin, float hullZmax){
        float submergedArea = Utils.CalculateMeshArea(mesh);
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        float Rn = CalculateReynoldsNumber(speed, Math.Abs(hullZmax - hullZmin));

        float onePlusK = 0;
        for (int i = 0; i < triangles.Length - 2; i +=3){
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i+1]];
            Vector3 v2 = vertices[triangles[i+2]];
            float Si = (0.5f)*Vector3.Cross((v1-v0),(v2-v0)).magnitude;
            float Ki = GetTriangleK((v0.z+v1.z+v2.z)/3, hullZmin, hullZmax);
            onePlusK += (1+Ki)*Si;
        }
        onePlusK = onePlusK/submergedArea;
        float Cf = 0.075f/((Mathf.Log10(Rn)-2)*(Mathf.Log10(Rn)-2));
        float Cfr = onePlusK*Cf;
        return Cfr;
    }

    private float CalculateReynoldsNumber(float velocity, float L, float viscosity=Constants.waterViscosity){
        return (velocity*L)/viscosity;
    }

    
    private float GetTriangleK(float z, float hullZmin, float hullZmax){
        float f = (-3/(hullZmax-hullZmin))*z + 3*hullZmax/(hullZmax-hullZmin) - 1;
        return f;
    }

    }
    public class Utils{
        public static Vector3 GetFaceNormal(Vector3 A, Vector3 B, Vector3 C){
            Vector3 normal = 0.5f*Vector3.Cross((B-A), (C-A));
            return normal;
        }

        public static void DebugDrawTriangle(Vector3[] triangle, Color color){
            Debug.DrawLine(triangle[0], triangle[1], color);
            Debug.DrawLine(triangle[0], triangle[2], color);
            Debug.DrawLine(triangle[1], triangle[2], color);
        }
        public static float CalculateMeshArea(Mesh mesh){
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            float totalArea = 0.0f;
            for (int i = 0; i < triangles.Length - 2; i += 3){
                Vector3 v1 = vertices[triangles[i+1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i+2]] - vertices[triangles[i]];
                Vector3 cross = Vector3.Cross(v1, v2);
                totalArea += 0.5f*cross.magnitude;            
            }
            return totalArea;
        }

        public static float[] CalculateTriangleAreas(Mesh mesh){
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            int L = triangles.Length;
            float triangleArea;
            List<float> triangleAreas = new List<float>(L); 
            for (int i = 0; i < L - 2; i += 3){
                Vector3 v1 = vertices[triangles[i+1]] - vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i+2]] - vertices[triangles[i]];
                triangleArea = 0.5f*(Vector3.Cross(v1, v2)).magnitude;
                triangleAreas.Add(triangleArea);
            }
            return triangleAreas.ToArray();
        }
    }
    public static class Forces{
        public static Vector3 BuoyancyForce(float height, Vector3 normal){
            Vector3 F = Constants.rho*Constants.g*height*normal;
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);
            return FVertical;
        }
    }
}