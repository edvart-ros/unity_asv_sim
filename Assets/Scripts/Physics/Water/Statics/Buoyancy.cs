using UnityEngine;
using WaterInteraction;

public class Buoyancy : MonoBehaviour
{

    public bool buoyancyForceActive = true;
    public bool debugBuoyancy;
    private Submerged submerged;
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