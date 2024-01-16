using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;
using Unity.Collections;

public class Buoyancy : MonoBehaviour
{

    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    private Submersion submersion;
    private Submerged submerged;
    private Patch patch;
    private Rigidbody rigidBody;



    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        submersion = GetComponent<Submersion>();
    }

    void FixedUpdate(){
        submerged = submersion.submerged;
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
                //Debug.DrawRay(centersWorld[i], normalsWorld[i], Color.red);
            }
            rigidBody.AddForceAtPosition(F, centersWorld[i]);
        }
    }
    

    private void OnDestroy()
        {
            // patch.DisposeRoutine();
        }
}