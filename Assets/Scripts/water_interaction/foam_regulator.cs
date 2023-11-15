using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class foam_regulator : MonoBehaviour
{
    public GameObject targetObject;
    public float maxVelocity;
    [Range(0.0f, 1.0f)]
    public float maxFoamGain = 0.5f;
    public WaterFoamGenerator[] foamGenerators;
    private float vel;
    private float angVel;
    private Rigidbody rb;
    private float foamGain;

    // Start is called before the first frame update
    void Start(){
        rb = targetObject.GetComponent<Rigidbody>(); 
        vel = rb.velocity.magnitude;
        angVel = rb.angularVelocity.magnitude;
    }

    void Update(){
        if (foamGenerators.Length != 0){
            vel = rb.velocity.magnitude;
            foamGain = Mathf.Min(Mathf.Min(vel/maxVelocity, 1), maxFoamGain);
            foreach (WaterFoamGenerator generator in foamGenerators){
                generator.deepFoamDimmer = foamGain;
                generator.surfaceFoamDimmer = foamGain;
            }
        }
    }
}
