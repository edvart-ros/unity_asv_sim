using System.Diagnostics;
using System.IO;
using WaterInteraction;
using UnityEngine;


public class Buoyancy : MonoBehaviour
{
    public bool buoyancyForceActive = true;
    private Vector3 buoyancyCenter = new Vector3();
    private Submerged submerged;
    private Rigidbody rigidBody;
    
    
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
    }

    
    void FixedUpdate()
    {
        if (!buoyancyForceActive) return;
        
        //rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
        ApplyBuoyancyVolume();
    }


    private void ApplyBuoyancyVolume() 
    {
        buoyancyCenter = submerged.data.centroid;
        float displacedVolume = submerged.data.volume;
        float buoyancyForce = Constants.waterDensity*Constants.gravity*displacedVolume;
        Vector3 forceVector = new Vector3(0f, buoyancyForce, 0f);
        rigidBody.AddForceAtPosition(forceVector, buoyancyCenter);
    }

    
    void OnDrawGizmos()
    {
        // debug
        /*
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(buoyancyCenter, 0.1f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.1f);
        */
    }
}