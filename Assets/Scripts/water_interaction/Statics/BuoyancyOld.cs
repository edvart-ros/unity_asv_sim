using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using WaterInteraction;
using Unity.Collections;

public class BuoyancyOld : MonoBehaviour
{
    // Toggles
    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    
    public WaterSurface targetSurface = null;
    public float sideLength = 10;
    public int gridFidelity = 4;
    public GameObject waterPatch;
    public GameObject simplifiedMesh;
    public GameObject submergedMesh;
    public Rigidbody rigidBody;
    [ReadOnly]
    public Submerged submerged;

    private MeshFilter waterPatchMeshFilter;
    private MeshFilter simplifiedMeshFilter;
    private MeshFilter submergedMeshFilter;
    private Patch patch;


    private void Start()
    {
        // We use the water patch for fast water height look-up
        waterPatchMeshFilter = waterPatch.GetComponent<MeshFilter>(); 
        // The simplified hull is used for submerged mesh calculation
        simplifiedMeshFilter = simplifiedMesh.GetComponent<MeshFilter>(); 
        // The calculated submerged parts of the hull is used to calculate the buoyancy forces
        submergedMeshFilter = submergedMesh.GetComponent<MeshFilter>(); 
        
        Vector3 gridOrigin = new Vector3(-sideLength/2, 0, sideLength/2);
        // Sample the WaterSurface to create a patch
        patch = new Patch(targetSurface, sideLength, gridFidelity, gridOrigin);
        // Set up submersion by providing the simplified hull mesh
        submerged = new Submerged(simplifiedMeshFilter.mesh); 
        // Updates the patch to follow the boat and queried water height
        patch.Update(transform); 

    }

    
    private void FixedUpdate()
    {
        // Updates the patch to follow the boat and queried water height
        patch.Update(transform); 
        // Assign the resulting patch vertices
        waterPatchMeshFilter.mesh.vertices = patch.patchVertices; 
        submerged.Update(patch, transform);
        submergedMeshFilter.mesh = submerged.mesh;
        
        if (buoyancyForceActive){
            ApplyBuoyancy();
        }

        DebugPatch();
    }


    private void ApplyBuoyancy()
    {
        float[] heights = submerged.FaceCenterHeightsAboveWater;
        Vector3[] centersWorld = submerged.FaceCentersWorld;
        Vector3[] normalsWorld = submerged.FaceNormalsWorld;
        for (var i = 0; i < centersWorld.Length; i++){
            if (normalsWorld[i].y >  0){
                continue;
            }
            Vector3 F = Forces.BuoyancyForce(heights[i], normalsWorld[i]);
            if (debugBuoyancy){
                //Debug.DrawRay(centersWorld[i], F, Color.green);
                Debug.DrawRay(centersWorld[i], normalsWorld[i], Color.red);
            }
            rigidBody.AddForceAtPosition(F, centersWorld[i]);
        }
    }

    
    private void DebugPatch()
    {
        Mesh patchMesh = waterPatchMeshFilter.mesh;
        Vector3[] verts = patchMesh.vertices;
        for (var i = 0; i < verts.Length; i++)
        {
            Debug.DrawRay(verts[i], Vector3.up);
        }
    }

    
    private void OnDestroy()
        {
            //patch.DisposeRoutine();
        }
}
