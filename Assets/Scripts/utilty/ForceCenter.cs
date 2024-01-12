using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceControllerCenter : MonoBehaviour
{
    public float forceAmount = 5.0f;  // Amount of force to apply
    public float torqueAmount = 5.0f; // Amount of torque to apply

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {

        // Apply Forces based on WASD input
        if (Input.GetKey(KeyCode.W)) {
            rb.AddForce(transform.forward*forceAmount);
        
        }

    }
}