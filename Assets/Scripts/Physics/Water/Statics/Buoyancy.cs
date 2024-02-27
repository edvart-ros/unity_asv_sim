using WaterInteraction;
using UnityEngine;


public class Buoyancy : MonoBehaviour
{
    public bool buoyancyForceActive = true;
    
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
        Vector3 buoyancyCenter = submerged.data.centroid;
        float displacedVolume = submerged.data.volume;
        Debug.Log(displacedVolume);
        float buoyancyForce = Constants.waterDensity*Constants.gravity*displacedVolume;
        Vector3 forceVector = new Vector3(0f, buoyancyForce, 0f);
        rigidBody.AddForceAtPosition(forceVector, buoyancyCenter);
        Debug.DrawRay(buoyancyCenter, Vector3.up);
    }

    
}