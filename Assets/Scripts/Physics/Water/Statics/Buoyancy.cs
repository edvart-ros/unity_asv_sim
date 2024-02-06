using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;
using Unity.Collections;
using UnityEditor.Search;
using Unity.VisualScripting;

public class Buoyancy : MonoBehaviour
{

    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    public bool CP;
    private Submerged submerged;
    private Patch patch;
    private Rigidbody rigidBody;



    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
    }

    void FixedUpdate(){
        rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
        if (buoyancyForceActive){
            ApplyBuoyancyVolume();
        }
    }


    private void ApplyBuoyancy() {
        float[] heights = submerged.FaceCenterHeightsAboveWater;
        Vector3[] pressureCentersWorld = submerged.pressureCenters;
        Vector3[] normalsLocal = submerged.FaceNormalsL;
        float[] areas = submerged.triangleAreas;

        Vector3 totalBuoyancyForce = Vector3.zero;
        Vector3 totalTorque = Vector3.zero;

        for (var i = 0; i < pressureCentersWorld.Length; i++) {
            Vector3 applicationPoint = pressureCentersWorld[i];
            float area = areas[i];
            Vector3 normal = transform.TransformDirection(normalsLocal[i]);
            Vector3 F = Forces.BuoyancyForce(heights[i], normal, area);

            // Calculate the force to apply at the center of mass
            totalBuoyancyForce += F;

            // Calculate the torque
            Vector3 leverArm = applicationPoint - rigidBody.worldCenterOfMass;

            totalTorque += Vector3.Cross(leverArm, F);
        }
        Debug.Log(totalTorque);
        // Apply the total force and torque to the rigidbody
        rigidBody.AddForce(totalBuoyancyForce);
        rigidBody.AddTorque(totalTorque);
    }

    private void ApplyBuoyancyVolume() {
        Vector3 buoyancyCenter = submerged.centroid;
        float displacedVolume = submerged.volume;
        float F = Constants.rho*Constants.g*displacedVolume;
        Vector3 FVec = new Vector3(0f, F, 0f);
        rigidBody.AddForceAtPosition(FVec, buoyancyCenter);
    }



    private void OnDestroy()
        {
            // patch.DisposeRoutine();
        }
}