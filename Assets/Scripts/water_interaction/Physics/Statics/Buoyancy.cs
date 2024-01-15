using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;
using Unity.Collections;

public class Buoyancy : MonoBehaviour
{

    public WaterSurface targetSurface = null;
    public float sideLength = 10;
    public int gridFidelity = 4;
    public Mesh simplifiedMesh;
    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    [ReadOnly]
    public Submerged submerged;
    private Mesh submergedMesh;
    private Patch patch;
    private Rigidbody rigidBody;



    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        Vector3 gridOrigin = new Vector3(-sideLength/2, 0, sideLength/2);
        patch = new Patch(targetSurface, sideLength, gridFidelity, gridOrigin);
        submerged = new Submerged(simplifiedMesh); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height
    }

    void FixedUpdate(){
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        submerged.Update(patch, transform);
        
        if (buoyancyForceActive){
            ApplyBuoyancy();
        }

        DebugPatch();
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
                //Debug.DrawRay(centersWorld[i], F, Color.green);
                Debug.DrawRay(centersWorld[i], normalsWorld[i], Color.red);
            }
            rigidBody.AddForceAtPosition(F, centersWorld[i]);
        }
    }
    
    private void DebugPatch()
    {
        int[] tris = patch.baseGridMesh.triangles;
        Vector3[] verts = patch.patchVertices;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Vector3[] tri = new Vector3[] { verts[tris[i]], verts[tris[i+1]], verts[tris[i+2]] };
            Utils.DebugDrawTriangle(tri, Color.red);
        }
    }

    private void OnDestroy()
        {
            // patch.DisposeRoutine();
        }
}