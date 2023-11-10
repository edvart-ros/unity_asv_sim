using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class foam_regulator : MonoBehaviour
{
    public GameObject targetObject;
    public float maxVelocity;
    private float vel;
    private Rigidbody rb;
    public WaterFoamGenerator[] foamGenerators;
    private float foamGain;
    // Start is called before the first frame update
    void Start()
    {
        rb = targetObject.GetComponent<Rigidbody>(); 
        vel = rb.velocity.magnitude;
    }

    // Update is called once per frame
    void Update()
    {
        if (foamGenerators.Length != 0){
            vel = rb.velocity.magnitude;
            foamGain = Mathf.Min(vel/maxVelocity, 1);
            foreach (WaterFoamGenerator generator in foamGenerators){
                generator.deepFoamDimmer = foamGain;
                generator.surfaceFoamDimmer = foamGain;
            }
        }
    }
}
