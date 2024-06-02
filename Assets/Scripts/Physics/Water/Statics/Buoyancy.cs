using System.Diagnostics;
using System.IO;
using WaterInteraction;
using UnityEngine;

[RequireComponent(typeof(Submersion))]
public class Buoyancy : MonoBehaviour
{
    public bool buoyancyForceActive = true;
    private Vector3 buoyancyCenter = new Vector3();
    private Submersion submersion;
    private Rigidbody rigidBody;
    
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        submersion = GetComponent<Submersion>();
    }

    
    void FixedUpdate()
    {
        if (!buoyancyForceActive) return;
        ApplyBuoyancyVolume();
    }


    private void ApplyBuoyancyVolume() 
    {
        buoyancyCenter = submersion.submerged.data.centroid;
        float displacedVolume = submersion.submerged.data.volume;
        float buoyancyForce = Constants.waterDensity*Constants.gravity*displacedVolume;
        Vector3 forceVector = new Vector3(0f, buoyancyForce, 0f);
        rigidBody.AddForceAtPosition(forceVector, buoyancyCenter);
    }
}