using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;

public class BuoyancyCP : MonoBehaviour
{

    public WaterSurface targetSurface = null;
    public float sideLength = 10;
    public GameObject waterPatch;
    public GameObject simplifiedMesh;
    public GameObject submergedMesh;
    public Rigidbody rigidBody;
    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    [Range(0.0f, 1.0f)]

    private MeshFilter waterPatchMeshFilter;
    private MeshFilter simplifiedMeshFilter;
    private MeshFilter submergedMeshFilter;
    private Patch patch;
    private Submerged submerged;

    private const int gridFidelity = 4;
    private const float hullZMin = -2.5f;
    private const float hullZMax = 2.9f;
    private const float boatLength = hullZMax-hullZMin;

    void Start()
    {
        waterPatchMeshFilter = waterPatch.GetComponent<MeshFilter>(); // the water patch used for fast water height look-up
        simplifiedMeshFilter = simplifiedMesh.GetComponent<MeshFilter>(); // the simplified hull used for submerged mesh calculation
        submergedMeshFilter = submergedMesh.GetComponent<MeshFilter>(); // the calculated submerged parts of the hull- used to calculate the buoyancy forces
        Vector3 gridOrigin = new Vector3(-sideLength/2, 0, sideLength/2);
        patch = new Patch(targetSurface, sideLength, gridFidelity, gridOrigin);
        submerged = new Submerged(simplifiedMeshFilter.mesh); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height

    }

    void FixedUpdate(){
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        waterPatchMeshFilter.mesh.vertices = patch.patchVertices; // assign the resulting patch vertices
        submerged.Update(patch, transform);
        submergedMeshFilter.mesh = submerged.mesh;
        
        if (buoyancyForceActive){
            ApplyBuoyancyCP();
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

            Vector3 normalWorld = submerged.FaceNormalsWorld[i/3];
            (Vector3[] topTri, Vector3[] botTri) = SplitSubmergedTriangleHorizontally(triangleVerticesWorld);

            Vector3 topCP = CalculateBuoyancyCenterTopTriangle(topTri);
            Vector3 bottomCP = CalculateBuoyancyCenterBottomTriangle(botTri);

            // get the water depth at the points of buoyancy
            // top triangle
            ///////////////
            (int iCell, int jCell) = patch.PointToCell(topCP);
            float xInCell = (topCP.x - patch.gridOrigin.x)-patch.cellSize*jCell;
            float zInCell = (topCP.z -patch. gridOrigin.z)-(patch.cellSize*(-iCell));

                    if (xInCell >= -zInCell){
                        patch.GetPatchRightTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
                    }
                    else{
                        patch.GetPatchLeftTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
                    }
            float topDepth = patch.GetHeightAboveTriangle(topCP, patchTriangleVerticesWorld);

            // bottom triangle
            ///////////////
            (iCell, jCell) = patch.PointToCell(bottomCP);
            xInCell = (bottomCP.x - patch.gridOrigin.x)-patch.cellSize*jCell;
            zInCell = (bottomCP.z - patch.gridOrigin.z)-(patch.cellSize*(-iCell));
            if (xInCell >= -zInCell){
                patch.GetPatchRightTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
            }
            else{
                patch.GetPatchLeftTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
            }
            float bottomDepth = patch.GetHeightAboveTriangle(bottomCP, patchTriangleVerticesWorld);

            float buoyancyForceTop = Constants.rho*Constants.g*topDepth   *Utils.GetFaceNormal(topTri[0], topTri[1], topTri[2]).magnitude;
            float buoyancyForceBot = Constants.rho*Constants.g*bottomDepth*Utils.GetFaceNormal(botTri[0], botTri[1], botTri[2]).magnitude;
            
            Vector3 F1 = -new Vector3(0.0f, buoyancyForceTop, 0.0f);
            Vector3 F2 = -new Vector3(0.0f, buoyancyForceBot, 0.0f);
            rigidBody.AddForceAtPosition(F1, topCP);
            rigidBody.AddForceAtPosition(F2, bottomCP);
            // Debug.Log(buoyancyForceTop);
            // Debug.DrawRay(bottomCP, Vector3.up, Color.green);
            // if (i % 5 == 0){
            //     Debug.DrawRay(bottomCP, 0.005f*Vector3.right, Color.green);
            //     DebugDrawTriangle(botTri, Color.red);
            //     Debug.DrawRay((botTri[0] + botTri[1] + botTri[2])/3, 0.005f*Vector3.right);
            // }
            // DebugDrawTriangle(botTri, Color.green);
        }
    }

        private (Vector3[], Vector3[]) SplitSubmergedTriangleHorizontally(Vector3[] triangleWorld){
        float[] vertexHeights = new float[3] { triangleWorld[0].y, triangleWorld[1].y, triangleWorld[2].y };
        (Vector3[] sortedVerticesWorld, float[] sortedHeights) = patch.SortVerticesAgainstWaterHeight(triangleWorld, vertexHeights);
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

    private Vector3 CalculateBuoyancyCenterTopTriangle(Vector3[] triangle){
        // takes in a triangle in world coordinates (with a horizontal base) and calculates its center of pressure/buoyancy
        Vector3 A = triangle[0];
        Vector3 B = triangle[1];
        Vector3 C = triangle[2];

        // Get the vertex depths and sort
        Vector3[] patchVerticesCache = waterPatchMeshFilter.mesh.vertices;
        float[] vertexHeights = new float[3];
        Vector3[] patchTriangleVerticesWorld = new Vector3[3];

        (int iCell, int jCell) = patch.PointToCell(A);
        float xInCell = (A.x - patch.gridOrigin.x)-patch.cellSize*jCell;
        float zInCell = (A.z - patch.gridOrigin.z)-(patch.cellSize*(-iCell));
        if (xInCell >= -zInCell){
            patch.GetPatchRightTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
        }
        else{
            patch.GetPatchLeftTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
        }
        float y0 = -patch.GetHeightAboveTriangle(A, patchTriangleVerticesWorld);
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

        (int iCell, int jCell) = patch.PointToCell(A);
        float xInCell = (A.x - patch.gridOrigin.x)-patch.cellSize*jCell;
        float zInCell = (A.z - patch.gridOrigin.z)-(patch.cellSize*(-iCell));
        if (xInCell >= -zInCell){
            patch.GetPatchRightTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
        }
        else{
            patch.GetPatchLeftTriangleVertices(ref patchTriangleVerticesWorld, iCell, jCell);
        }
        float y0 = -patch.GetHeightAboveTriangle(A, patchTriangleVerticesWorld);
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


    private void OnDestroy()
        {
            // patch.DisposeRoutine();
        }
}