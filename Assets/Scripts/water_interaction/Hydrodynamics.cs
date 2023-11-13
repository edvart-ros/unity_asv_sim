using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class Hydrodynamics : MonoBehaviour
{

    public WaterSurface targetSurface = null;
    public float sideLength;
    public GameObject waterPatch;
    public GameObject simplifiedMesh;
    public GameObject submergedMesh;
    public Rigidbody rigidBody;
    public bool buoyancyForceActive;
    public bool debugBuoyancy;
    [Range(0.0f, 1.0f)]
    public float buoyancyRayLength= 0.1f;
    public bool simpleDampingActive;


    [Range(0.01f, 50.0f)]
    public float linearDamping = 1.0f;
    [Range(0.01f, 50.0f)]
    public float quadraticDamping = 1.0f;
    public bool pressureDragActive;
    public bool debugPressureDrag;


    [Range(0.0f, 1000.0f)]
    public float pressureDragLinearCoefficient = 500.0f;
    [Range(0.0f, 1000.0f)]
    public float pressureDragQuadraticCoefficient = 200.0f;
    [Range(0.01f, 5.0f)]
    public float pressureDragVelocityRef= 1.0f;
    [Range(0.1f, 4.0f)]
    public float pressureDragFalloffPower = 0.7f;
    public bool viscousResistActive;
    public bool debugResist;
    

    public bool slammingForcesActive;
    public bool debugSlamming;



    // params for water height query
    WaterSearchParameters searchParameters = new WaterSearchParameters();
    WaterSearchResult searchResult = new WaterSearchResult();
    
    private MeshFilter waterPatchMeshFilter;
    private MeshFilter simplifiedMeshFilter;
    private MeshFilter submergedMeshFilter;
    private Mesh basePatchMeshGrid;
    private Vector3[] submergedFaceNormalsWorld;
    private Vector3[] submergedFaceCentersWorld;
    private float[] submergedFaceAreas;
    private float[] submergedFaceCenterHeightsAboveWater;
    private float[] hullTrianglesSubmergedAreas;
    private float[] hullTrianglesSubmergedAreasPrev;


    private float cellSize;
    private Vector3 gridOrigin;
    private float hullSurfaceArea;
    private float submergedSurfaceArea;

    private Rigidbody boatRigidBodyPrev;


    private const float g = 9.80665f;
    private const float rho = 0.5f*997;
    private const float waterViscosity = 1.0016f;

    private const int gridFidelity = 4;
    private const float hullZMin = -2.5f;
    private const float hullZMax = 2.9f;
    private const float boatLength = hullZMax-hullZMin;

    // water height querying (burst)
    // Input job parameters
    NativeArray<float3> targetPositionBuffer;
    // Output job parameters
    NativeArray<float> errorBuffer;
    NativeArray<float3> candidatePositionBuffer;
    NativeArray<float3> projectedPositionWSBuffer;
    NativeArray<float3> directionBuffer;
    NativeArray<int> stepCountBuffer;


    void Start()
    {
        // practical calculations
        cellSize = sideLength / gridFidelity;
        gridOrigin = new Vector3(-sideLength/2, 0, sideLength/2);
        // retrieve the objects filters that we need
        waterPatchMeshFilter = waterPatch.GetComponent<MeshFilter>(); // the water patch used for fast water height look-up
        simplifiedMeshFilter = simplifiedMesh.GetComponent<MeshFilter>(); // the simplified hull used for submerged mesh calculation
        hullTrianglesSubmergedAreas = new float[simplifiedMeshFilter.mesh.triangles.Length/3];
        hullTrianglesSubmergedAreasPrev = hullTrianglesSubmergedAreas;

        submergedMeshFilter = submergedMesh.GetComponent<MeshFilter>(); // the calculated submerged parts of the hull- used to calculate the buoyancy forces
        // Construct a base mesh grid
        basePatchMeshGrid = ConstructBaseMeshGrid();
        // assign mesh to waterPatch
        waterPatchMeshFilter.mesh = basePatchMeshGrid;
        int numItems = basePatchMeshGrid.vertices.Length;

        // Allocate the buffers
        targetPositionBuffer = new NativeArray<float3>(numItems, Allocator.Persistent);
        errorBuffer = new NativeArray<float>(numItems, Allocator.Persistent);
        candidatePositionBuffer = new NativeArray<float3>(numItems, Allocator.Persistent);
        projectedPositionWSBuffer = new NativeArray<float3>(numItems, Allocator.Persistent);
        directionBuffer = new NativeArray<float3>(numItems, Allocator.Persistent);
        stepCountBuffer = new NativeArray<int>(numItems, Allocator.Persistent);

        hullSurfaceArea = CalculateMeshArea(simplifiedMeshFilter.mesh);
        boatRigidBodyPrev = rigidBody;
    }

    void FixedUpdate(){ 
        UpdateWaterPatch();        
        UpdateSubmerged();
        submergedSurfaceArea = CalculateMeshArea(submergedMeshFilter.mesh);
        submergedFaceAreas = CalculateTriangleAreas(submergedMeshFilter.mesh);
        
        if (buoyancyForceActive){
            ApplyBuoyancy();
        }        
        if (simpleDampingActive){
            ApplySimpleDamping(rigidBody.velocity, linearDamping, quadraticDamping, hullSurfaceArea, submergedSurfaceArea);
            ApplySimpleDampingAngular(rigidBody.angularVelocity, linearDamping, quadraticDamping, hullSurfaceArea, submergedSurfaceArea);
        }
        if (viscousResistActive){
            float Cfr = CalculateResistanceCoefficient(submergedSurfaceArea);
            ApplyViscousResistance(Cfr);
        }
        if (pressureDragActive){
            ApplyPressureDrag(pressureDragLinearCoefficient, pressureDragQuadraticCoefficient, pressureDragVelocityRef, pressureDragFalloffPower);
        }
        if (slammingForcesActive){
            ApplySlammingForce();
        }

        // cache state for next cycle
        hullTrianglesSubmergedAreasPrev = hullTrianglesSubmergedAreas;
        boatRigidBodyPrev = rigidBody;
    }



    private Mesh ConstructBaseMeshGrid(){
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

    // private void UpdateWaterPatch(){
    //     Vector3[] vertices = basePatchMeshGrid.vertices;
    //     for (var i = 0; i < vertices.Length; i++){
    //         Vector3 vertex = vertices[i];
    //         float waterHeight = GetWaterHeight(vertex+transform.position);
    //         vertex.x += transform.position.x;
    //         vertex.z += transform.position.z;
    //         vertex.y = waterHeight;
    //         vertices[i] = vertex;
    //     }
    //     gridOrigin = new Vector3(-sideLength/2 + transform.position.x, 0, sideLength/2 + transform.position.z);
    //     waterPatchMeshFilter.mesh.vertices = vertices;
    // }

    // burst version
    private void UpdateWaterPatch(){
        Vector3[] vertices = basePatchMeshGrid.vertices;

        WaterSimSearchData simData = new WaterSimSearchData();
        if (!targetSurface.FillWaterSearchData(ref simData))
            return;

        for (int i = 0; i < vertices.Length;     i++){
            targetPositionBuffer[i] = transform.position + vertices[i];
        }
        // Prepare the first band
        WaterSimulationSearchJob searchJob = new WaterSimulationSearchJob();
        // Assign the simulation data
        searchJob.simSearchData = simData;
        // Fill the input data
        searchJob.targetPositionWSBuffer = targetPositionBuffer;
        searchJob.startPositionWSBuffer = targetPositionBuffer;
        searchJob.maxIterations = 8;
        searchJob.error = 0.01f;
        searchJob.includeDeformation = true;
        searchJob.excludeSimulation = false;
        
        searchJob.errorBuffer = errorBuffer;
        searchJob.candidateLocationWSBuffer = candidatePositionBuffer;
        searchJob.projectedPositionWSBuffer = projectedPositionWSBuffer;
        searchJob.directionBuffer = directionBuffer;
        searchJob.stepCountBuffer = stepCountBuffer;
        // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
        JobHandle handle = searchJob.Schedule(vertices.Length, 1);
        handle.Complete();

        // update vertex positions
        for (var i = 0; i < vertices.Length; i++){
            Vector3 vertex = vertices[i];
            float waterHeight = projectedPositionWSBuffer[i].y;
            vertex.x += transform.position.x;
            vertex.z += transform.position.z;
            vertex.y = waterHeight;
            vertices[i] = vertex;
        }
        gridOrigin = new Vector3(-sideLength/2 + transform.position.x, 0, sideLength/2 + transform.position.z);
        waterPatchMeshFilter.mesh.vertices = vertices;
    }


    private void UpdateSubmerged(){
        submergedMeshFilter.mesh.Clear();
        // For storing the new resulting submerged mesh
        int L = simplifiedMeshFilter.mesh.vertices.Length;
        List<Vector3> submergedVerticesLocal = new List<Vector3>(L); // Upper bound = vertices in the hull mesh
        List<int> submergedTrianglesLocal = new List<int>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
        List<Vector3> submergedNormalsWorld = new List<Vector3>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
        List<Vector3> submergedCentersWorld = new List<Vector3>(L * 2); // Upper bound = twice the number of triangles in the hull mesh
        List<float> submergedCenterHeightsAboveWater = new List<float>(L * 2); // Upper bound = twice the number of triangles in the hull mesh

        float[] hullSubmergedAreas = new float[L];

        int numTriangles = simplifiedMeshFilter.mesh.triangles.Length;
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        Vector3[] hullVerticesCache = simplifiedMeshFilter.mesh.vertices;
        int[] hullTrianglesCache = simplifiedMeshFilter.mesh.triangles;
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
            Vector3 originalFaceNormalWorld = GetFaceNormal(triangleVerticesWorld[0], triangleVerticesWorld[1], triangleVerticesWorld[2]);
            float[] vertexHeights = new float[3];

            for (var j = 0; j < 3; j++){
                Vector3 vertexLocal = triangleVerticesLocal[j];
                Vector3 vertexWorld = transform.TransformPoint(vertexLocal);
                (int iCell, int jCell) = PointToCell(vertexWorld, gridOrigin, cellSize);
                float xInCell = (vertexWorld.x - gridOrigin.x)-cellSize*jCell;
                float zInCell = (vertexWorld.z - gridOrigin.z)-(cellSize*(-iCell));
                if (xInCell >= -zInCell){
                    patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
                }
                else{
                    patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
                }
                vertexHeights[j] = GetHeightAboveTriangle(vertexWorld, patchTriangleVerticesWorld);
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
                    (Vector3[] sortedVerticesLocal, float[] sortedHeights) = SortVerticesAgainstWaterHeight(triangleVerticesLocal, vertexHeights);
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
                    faceNormalWorld = GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                    transform.TransformPoint(J_H),
                                                    transform.TransformPoint(J_M));
                    faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;
                    hullSubmergedAreas[i/3] = faceNormalWorld.magnitude/2;
                    submergedNormalsWorld.Add(faceNormalWorld);
                    submergedCentersWorld.Add(centerWorld);
                    submergedCenterHeightsAboveWater.Add(GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                    break;
                }
                case 2: {
                    //sort the triangles, L, M, H from lowest to highest
                    (Vector3[] sortedVerticesLocal, float[] sortedHeights) = SortVerticesAgainstWaterHeight(triangleVerticesLocal, vertexHeights);
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
                    faceNormalWorld = GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                    transform.TransformPoint(I_M),
                                                    transform.TransformPoint(sortedVerticesLocal[1]));
                    faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;

                    hullSubmergedAreas[i/3] = faceNormalWorld.magnitude/2;
                    submergedNormalsWorld.Add(faceNormalWorld);
                    submergedCentersWorld.Add(centerWorld);
                    submergedCenterHeightsAboveWater.Add(GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                    
                    submergedVerticesLocal.Add(sortedVerticesLocal[0]);
                    submergedVerticesLocal.Add(I_M);
                    submergedVerticesLocal.Add(I_L);
                    centerWorld = transform.TransformPoint((sortedVerticesLocal[0] + I_M + I_L)/3);
                    faceNormalWorld = GetFaceNormal(transform.TransformPoint(sortedVerticesLocal[0]),
                                                    transform.TransformPoint(I_M),
                                                    transform.TransformPoint(I_L));
                    faceNormalWorld = faceNormalWorld.magnitude*originalFaceNormalWorld.normalized;
                    hullSubmergedAreas[i/3] += faceNormalWorld.magnitude/2;
                    submergedNormalsWorld.Add(faceNormalWorld);
                    submergedCentersWorld.Add(centerWorld);
                    submergedCenterHeightsAboveWater.Add(GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));


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
                    submergedCenterHeightsAboveWater.Add(GetHeightAboveTriangle(centerWorld, patchTriangleVerticesWorld));
                    submergedTrianglesLocal.Add(numExistingVertices);
                    submergedTrianglesLocal.Add(numExistingVertices+1);
                    submergedTrianglesLocal.Add(numExistingVertices+2);
                    hullSubmergedAreas[i/3] = originalFaceNormalWorld.magnitude/2;
                    break;
                }
            }
        }
        hullTrianglesSubmergedAreas = hullSubmergedAreas;
        submergedFaceNormalsWorld = submergedNormalsWorld.ToArray();
        submergedFaceCentersWorld = submergedCentersWorld.ToArray();
        submergedFaceCenterHeightsAboveWater = submergedCenterHeightsAboveWater.ToArray();
        submergedMeshFilter.mesh.vertices = submergedVerticesLocal.ToArray();
        submergedMeshFilter.mesh.triangles = submergedTrianglesLocal.ToArray();
    }

    private void ApplyBuoyancy(){
        float[] heights = submergedFaceCenterHeightsAboveWater;
        Vector3[] centersWorld = submergedFaceCentersWorld;
        Vector3[] normalsWorld = submergedFaceNormalsWorld;
        int numFaces = normalsWorld.Length;
        for (var i = 0; i < numFaces; i++){
            if (normalsWorld[i].y >  0){
                continue;
            }
            Vector3 F = rho*g*heights[i]*normalsWorld[i];
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);
            if (debugBuoyancy){
                Debug.DrawRay(centersWorld[i], FVertical*buoyancyRayLength, Color.green);
            }
            rigidBody.AddForceAtPosition(FVertical, centersWorld[i]);
        }
    }

    private void ApplyBuoyancyCP(){
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        Vector3[] patchTriangleVerticesWorld = new Vector3[3];
        Vector3[] vertices = submergedMeshFilter.mesh.vertices;
        int[] triangles = submergedMeshFilter.mesh.triangles;

        for (int i = 0; i < triangles.Length-2; i += 3){
            // get the triangle world coordinates and calculate the center of buoyancy for it
            Vector3[] triangleVerticesWorld = new Vector3[] {
                transform.TransformPoint(vertices[triangles[i]]),
                transform.TransformPoint(vertices[triangles[i+1]]), 
                transform.TransformPoint(vertices[triangles[i+2]])
                };

            Vector3 normalWorld = submergedFaceNormalsWorld[i/3];
            (Vector3[] topTri, Vector3[] botTri) = SplitSubmergedTriangleHorizontally(triangleVerticesWorld);

            Vector3 topCP = CalculateBuoyancyCenterTopTriangle(topTri);
            Vector3 bottomCP = CalculateBuoyancyCenterBottomTriangle(botTri);

            //get the water depth at the points of buoyancy
            
            // top triangle
            ///////////////
            (int iCell, int jCell) = PointToCell(topCP, gridOrigin, cellSize);
            float xInCell = (topCP.x - gridOrigin.x)-cellSize*jCell;
            float zInCell = (topCP.z - gridOrigin.z)-(cellSize*(-iCell));

            if (xInCell >= -zInCell){
                patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            else{
                patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            float topDepth = GetHeightAboveTriangle(topCP, patchTriangleVerticesWorld);

            // bottom triangle
            ///////////////
            (iCell, jCell) = PointToCell(bottomCP, gridOrigin, cellSize);
            xInCell = (bottomCP.x - gridOrigin.x)-cellSize*jCell;
            zInCell = (bottomCP.z - gridOrigin.z)-(cellSize*(-iCell));
            if (xInCell >= -zInCell){
                patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            else{
                patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            float bottomDepth = GetHeightAboveTriangle(bottomCP, patchTriangleVerticesWorld);

            float buoyancyForceTop = rho*g*topDepth   *GetFaceNormal(topTri[0], topTri[1], topTri[2]).magnitude;
            float buoyancyForceBot = rho*g*bottomDepth*GetFaceNormal(botTri[0], botTri[1], botTri[2]).magnitude;
            if (buoyancyForceTop == 0 & buoyancyForceBot == 0){
                continue;
            }

            //sum (original triangle)
            
            Vector3 triangleCP = (topCP*buoyancyForceTop + bottomCP*buoyancyForceBot)/(buoyancyForceTop + buoyancyForceBot);
            (iCell, jCell) = PointToCell(triangleCP, gridOrigin, cellSize);
            xInCell = (triangleCP.x - gridOrigin.x)-cellSize*jCell;
            zInCell = (triangleCP.z - gridOrigin.z)-(cellSize*(-iCell));
            if (xInCell >= -zInCell){
                patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            else{
                patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            float triangleCPDepth = GetHeightAboveTriangle(triangleCP, patchTriangleVerticesWorld);
            Vector3 F = rho*g*triangleCPDepth*normalWorld;
            Vector3 FVertical = new Vector3(0.0f, F.y, 0.0f);

            rigidBody.AddForceAtPosition(FVertical, triangleCP);
            // Debug.DrawRay(triangleCP, Vector3.up, Color.red);
            // DebugDrawTriangle(triangleVerticesWorld, Color.white);
        }
    }
    private void ApplySimpleDamping(Vector3 velocity, float linear_factor, float quad_factor, float area, float submergedArea){
        float areaRatio = submergedArea/area;
        Vector3 linearF = -linear_factor*areaRatio*velocity;
        rigidBody.AddForce(linearF);
    }

    private void ApplySimpleDampingAngular(Vector3 angular_velocity, float linear_factor, float quad_factor, float area, float submergedArea){
        float areaRatio = submergedArea/area;
        Vector3 linearT = -linear_factor*areaRatio*angular_velocity;
        rigidBody.AddTorque(linearT);
    }

    private void ApplyViscousResistance(float Cfr, float density=rho){
        Vector3[] vertices = submergedMeshFilter.mesh.vertices;
        int[] triangles = submergedMeshFilter.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        for (int i = 0; i < submergedFaceCentersWorld.Length; i++){
            Vector3 n = submergedFaceNormalsWorld[i].normalized;
            Vector3 Ci = submergedFaceCentersWorld[i];
            Vector3 GCi = Ci - G;
            //something below here becomes NaN. need to debug
            Vector3 vi = vG + Vector3.Cross(omegaG, GCi);
            Vector3 viTan = vi-(Vector3.Dot(vi, n))*n;
            Vector3 ufi = -viTan/(viTan.magnitude);
            if (float.IsNaN(ufi.x)){
                continue;
            }
            Vector3 vfi = vi.magnitude*ufi;
            Vector3 Fvi = (0.5f)*density*Cfr*submergedFaceAreas[i]*vfi.magnitude*vfi;
            rigidBody.AddForceAtPosition(Fvi, Ci);
            if (debugResist){
                Debug.DrawRay(Ci, Fvi, Color.red);
            }
        }
        return;
    }

    private void ApplyPressureDrag(float Cpd1, float Cpd2, float vRef, float fp){
        Vector3[] vertices = submergedMeshFilter.mesh.vertices;
        int[] triangles = submergedMeshFilter.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 Fpd;
        for (int i = 0; i < triangles.Length - 2; i +=3){
            (Vector3 v0, Vector3 v1, Vector3 v2) = (vertices[triangles[i]], vertices[triangles[i+1]], vertices[triangles[i+2]]);
            Vector3 ni = submergedFaceNormalsWorld[i/3].normalized;
            Vector3 Ci = submergedFaceCentersWorld[i/3];
            float Si = (0.5f)*Vector3.Cross((v1-v0),(v2-v0)).magnitude;

            Vector3 GCi = Ci - G;
            Vector3 vi = vG + Vector3.Cross(omegaG, GCi);
            Vector3 ui = vi.normalized;
            float cosThetai = Vector3.Dot(ui, ni);

            float viMag = vi.magnitude;
            if (viMag == 0.0f){
                continue;
            }
            if (cosThetai <= 0.0f){
                Fpd = (Cpd1*(viMag/vRef) + Cpd2*((viMag*viMag)/(viMag*viMag)))*Si*Mathf.Pow(Mathf.Abs(cosThetai), fp)*ni;
            }
            else{
                Fpd = -(Cpd1*(viMag/vRef) + Cpd2*((viMag*viMag)/(vRef*vRef)))*Si*Mathf.Pow(cosThetai, fp)*ni;
            }
            rigidBody.AddForceAtPosition(Fpd, Ci);
            if (debugPressureDrag){
                Debug.DrawRay(Ci, Fpd, Color.white);
            }
        }
        return;
    }

    private void ApplySlammingForce(){
        Vector3[] vertices = simplifiedMeshFilter.mesh.vertices;
        int[] triangles = simplifiedMeshFilter.mesh.triangles;
        // Vector3[] normals = simplifiedMeshFilter.mesh.normals; //dont use these normals (theyre not accurate), compute manually? or prepare normals for the mesh before hand, accurately.
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;

        Vector3 vGPrev = boatRigidBodyPrev.velocity;
        Vector3 omegaGPrev = boatRigidBodyPrev.angularVelocity;
        Vector3 GPrev = boatRigidBodyPrev.position;
        float m = rigidBody.mass;

        for (int i = 0; i < triangles.Length - 2 ; i += 3){
            (Vector3 v0, Vector3 v1, Vector3 v2) = (vertices[triangles[i]], vertices[triangles[i+1]], vertices[triangles[i+2]]);
            Vector3 normalWorld = GetFaceNormal(transform.TransformPoint(v0), transform.TransformPoint(v1), transform.TransformPoint(v2));
            float triangleArea = 0.5f*normalWorld.magnitude;
            Vector3 GCi = (v0+v1+v2)/3;
            Vector3 vi = vG + Vector3.Cross(omegaG, GCi);
            Vector3 viPrev = vGPrev + Vector3.Cross(omegaGPrev, GCi);
            float cosThetai = Vector3.Dot(vi.normalized, normalWorld.normalized);

            float submergedTriangleArea = hullTrianglesSubmergedAreas[i/3];
            float submergedTriangleAreaPrev = hullTrianglesSubmergedAreasPrev[i/3];

            Vector3 VSwept = submergedTriangleArea*vi; //unsure about this calculation. Should VSwept be a vector?
            Vector3 VSweptPrev = submergedTriangleAreaPrev*viPrev; //unsure about this calculation. 
            Vector3 gamma = (VSwept-VSweptPrev)/(triangleArea*Time.fixedDeltaTime);

            Vector3 FStopping = (2*submergedTriangleArea)*m*vi/hullSurfaceArea;
            if (cosThetai > 0){ 

                if (debugSlamming){
                    Debug.DrawRay(transform.TransformPoint(GCi), FStopping);
                }
            }
        }
        return;
    }

    private float CalculateResistanceCoefficient(float submergedArea){
        Vector3[] vertices = submergedMeshFilter.mesh.vertices;
        int[] triangles = submergedMeshFilter.mesh.triangles;
        float Rn = CalculateReynoldsNumber(rigidBody.velocity.magnitude);

        float onePlusK = 0;
        for (int i = 0; i < triangles.Length - 2; i +=3){
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i+1]];
            Vector3 v2 = vertices[triangles[i+2]];
            float Si = (0.5f)*Vector3.Cross((v1-v0),(v2-v0)).magnitude;
            float Ki = GetTriangleK((v0.z+v1.z+v2.z)/3);
            onePlusK += (1+Ki)*Si;
        }
        onePlusK = onePlusK/submergedArea;
        float Cf = 0.075f/((Mathf.Log10(Rn)-2)*(Mathf.Log10(Rn)-2));
        float Cfr = onePlusK*Cf;
        return Cfr;
    }

    private float CalculateReynoldsNumber(float velocity, float L=boatLength, float viscosity=waterViscosity){
        return (velocity*L)/viscosity;
    }

    private float GetWaterHeight(Vector3 pos){
        // Build the search parameters
        searchParameters.startPositionWS = searchResult.candidateLocationWS;
        searchParameters.targetPositionWS = pos;
        searchParameters.error = 0.01f;
        searchParameters.maxIterations = 8;

        // Do the search
        if (targetSurface.ProjectPointOnWaterSurface(searchParameters, out searchResult)){
            return searchResult.projectedPositionWS.y;
        }
        else {
            Debug.LogWarning("Can't Find Height");
            return 0.0f;
        }
    }
    private (int, int) PointToCell(Vector3 point, Vector3 gridOrigin, float cellSize)
    {
        Vector3 adjustedPoint = point - gridOrigin;
        int i = Mathf.FloorToInt(-adjustedPoint.z / cellSize);
        int j = Mathf.FloorToInt(adjustedPoint.x / cellSize);
        return new (i, j);
    }


    private Vector3[] GetPatchLeftTriangleVertices(int i, int j, Vector3[] patchMeshVertices){
        int n = gridFidelity + 1;
        Vector3 topLeftVertex = patchMeshVertices[i*n + j];
        Vector3 bottomLeftVertex = patchMeshVertices[(i+1)*n+ j];
        Vector3 bottomRightVertex = patchMeshVertices[(i+1)*n + (j+1)];
        return new Vector3[] {topLeftVertex, bottomLeftVertex, bottomRightVertex};
    }

    private Vector3[] GetPatchRightTriangleVertices(int i, int j, Vector3[] patchMeshVertices){
        int n = gridFidelity + 1;
        Vector3 topLeftVertex = patchMeshVertices[i*n + j];
        Vector3 topRightVertex = patchMeshVertices[i*n + (j+1)];
        Vector3 bottomRightVertex = patchMeshVertices[(i+1)*n + (j+1)];
        return new Vector3[] {topLeftVertex, topRightVertex, bottomRightVertex};
    }

    private float GetHeightAboveTriangle(Vector3 point, Vector3[] triangle){
        Vector3 AB = triangle[1]-triangle[0];
        Vector3 AC = triangle[2]-triangle[0];
        Vector3 abc = Vector3.Cross(AB, AC);
        float d = Vector3.Dot(abc, triangle[0]);
        return (point.y - (d - abc.x*point.x - abc.z*point.z)/(abc.y));
    }

    private (Vector3[], float[]) SortVerticesAgainstWaterHeight(Vector3[] vertices, float[] heights) {
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


    private Vector3 GetFaceNormal(Vector3 A, Vector3 B, Vector3 C){
        Vector3 normal = 0.5f*Vector3.Cross((B-A), (C-A));
        return normal;
    }
    private Vector3[] AddToEach(Vector3[] array, Vector3 toAdd){
    for (int i = 0; i < array.Length; i++)
    {
        array[i] += toAdd;
    }
    return array;
    }

    private float GetTriangleK(float z, float zMin = hullZMin, float zMax = hullZMax){
        float f = (-3/(zMax-zMin))*z + 3*zMax/(zMax-zMin) - 1;
        return f;
    }

    private (Vector3[], Vector3[]) SplitSubmergedTriangleHorizontally(Vector3[] triangleWorld){
        // Get the vertex depths and sort
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        float[] vertexHeights = new float[3];
        Vector3[] patchTriangleVerticesWorld = new Vector3[3];

        for (var i = 0; i < 3; i++){
            Vector3 vertexWorld = triangleWorld[i];
            (int iCell, int jCell) = PointToCell(vertexWorld, gridOrigin, cellSize);
            float xInCell = (vertexWorld.x - gridOrigin.x)-cellSize*jCell;
            float zInCell = (vertexWorld.z - gridOrigin.z)-(cellSize*(-iCell));
            if (xInCell >= -zInCell){
                patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            else{
                patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
            }
            vertexHeights[i] = GetHeightAboveTriangle(vertexWorld, patchTriangleVerticesWorld);
        }
        (Vector3[] sortedVerticesWorld, float[] sortedHeights) = SortVerticesAgainstWaterHeight(triangleWorld, vertexHeights);
        (Vector3 L, Vector3 M, Vector3 H) = (sortedVerticesWorld[0], sortedVerticesWorld[1], sortedVerticesWorld[2]);

        Vector3 D;
        Vector3[] upperTriangle;
        Vector3[] lowerTriangle;
        if (Math.Abs(H.x - L.x) < 1e-6)
        {
            // If LH is approximately vertical, set D.x directly to L.x or H.x
            D = new Vector3(L.x, M.y, (L.z + H.z) / 2); // Assuming an average z for vertical lines

            upperTriangle = new Vector3[] { H, M, D };
            lowerTriangle = new Vector3[] { L, D, M };

            return (upperTriangle, lowerTriangle);
        }

        float m = (H.y - L.y) / (H.x - L.x);
        float x = (M.y - L.y) / m + L.x;

        float alpha = (x - L.x) / (H.x - L.x);
        float z = L.z + alpha * (H.z - L.z);
        D = new Vector3(x, M.y, z);

        upperTriangle = new Vector3[]{H, M, D};
        lowerTriangle = new Vector3[]{L, D, M};

        return (upperTriangle, lowerTriangle);
    }

    private Vector3 CalculateBuoyancyCenterTopTriangle(Vector3[] triangle){
        // takes in a triangle in world coordinates (with a horizontal base) and calculates its center of pressure/buoyancy
        Vector3 A = triangle[0];
        Vector3 B = triangle[1];
        Vector3 C = triangle[2];

        // Get the vertex depths and sort
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        float[] vertexHeights = new float[3];
        Vector3[] patchTriangleVerticesWorld = new Vector3[3];

        (int iCell, int jCell) = PointToCell(A, gridOrigin, cellSize);
        float xInCell = (A.x - gridOrigin.x)-cellSize*jCell;
        float zInCell = (A.z - gridOrigin.z)-(cellSize*(-iCell));
        if (xInCell >= -zInCell){
            patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
        }
        else{
            patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
        }
        float y0 = -GetHeightAboveTriangle(A, patchTriangleVerticesWorld);
        float h = A.y-B.y;
        float tc = (4*y0+3*h)/(6*y0+4*h);

        if ((6*y0+4*h) == 0){
            tc = 0.75f;
        }

        Vector3 centerBuoyancy = A + tc*((B+C)/2-A);
        return centerBuoyancy;
    }

    private Vector3 CalculateBuoyancyCenterBottomTriangle(Vector3[] triangle){
        // takes in a triangle in world coordinates (with a horizontal base) and calculates its center of pressure/buoyancy
        Vector3 A = triangle[0];
        Vector3 B = triangle[1];
        Vector3 C = triangle[2];

        // Get the vertex depths and sort
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        float[] vertexHeights = new float[3];
        Vector3[] patchTriangleVerticesWorld = new Vector3[3];

        (int iCell, int jCell) = PointToCell(A, gridOrigin, cellSize);
        float xInCell = (A.x - gridOrigin.x)-cellSize*jCell;
        float zInCell = (A.z - gridOrigin.z)-(cellSize*(-iCell));
        if (xInCell >= -zInCell){
            patchTriangleVerticesWorld = GetPatchRightTriangleVertices(iCell, jCell, patchVerticesCache);
        }
        else{
            patchTriangleVerticesWorld = GetPatchLeftTriangleVertices(iCell, jCell, patchVerticesCache);
        }
        float y0 = -GetHeightAboveTriangle(A, patchTriangleVerticesWorld);
        float h = A.y-B.y;
        float tc = (4*y0+3*h)/(6*y0+4*h);


        Vector3 centerBuoyancy = A + tc*((B+C)/2-A);
        return centerBuoyancy;
    }




    private void DebugDrawTriangle(Vector3[] triangle, Color color){
        Debug.DrawLine(triangle[0], triangle[1], color);
        Debug.DrawLine(triangle[0], triangle[2], color);
        Debug.DrawLine(triangle[1], triangle[2], color);
    }
}

