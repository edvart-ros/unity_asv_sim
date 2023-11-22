using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;

public class Buoyancy : MonoBehaviour
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
            ApplyBuoyancy();
        }
    }


    private void ApplyBuoyancy(){
        float[] heights = submerged.FaceCenterHeightsAboveWater;
        Vector3[] centersWorld = submerged.FaceCentersWorld;
        Vector3[] normalsWorld = submerged.FaceNormalsWorld;
        for (var i = 0; i < centersWorld.Length; i++){
            if (normalsWorld[i].y >  0){
                continue;
            }
            Vector3 F = Forces.BuoyancyForce(heights[i], normalsWorld[i]);
            if (debugBuoyancy){
                Debug.DrawRay(centersWorld[i], F, Color.green);
            }
            rigidBody.AddForceAtPosition(F, centersWorld[i]);
        }
    }

    private void OnDestroy()
        {
            patch.DisposeRoutine();
        }
}