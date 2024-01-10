using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceController : MonoBehaviour
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
        if (Input.GetKey(KeyCode.W))
        {
            rb.AddForceAtPosition(transform.forward*forceAmount, transform.TransformPoint(new Vector3(0.0f, -0.5f, -4.0f)));
        
        }
        if (Input.GetKey(KeyCode.S))
        {
            rb.AddRelativeForce(-Vector3.forward * forceAmount);
        }
        if (Input.GetKey(KeyCode.A))
        {
            rb.AddRelativeTorque(-Vector3.up * torqueAmount);
        }
        if (Input.GetKey(KeyCode.D))
        {
            rb.AddRelativeTorque(Vector3.up * torqueAmount);
        }
        if (Input.GetKey(KeyCode.E)) {
            rb.AddRelativeForce(new Vector3(1, 0, 1).normalized * forceAmount);
        }
        if (Input.GetKey(KeyCode.Q)) {
            rb.AddRelativeForce(new Vector3(-1, 0, 1).normalized * forceAmount);
        }
        if (Input.GetKey(KeyCode.Z)) {
            rb.AddRelativeForce(new Vector3(-1, 0, -1).normalized * forceAmount);
        }
        if (Input.GetKey(KeyCode.C)) {
            rb.AddRelativeForce(new Vector3(1, 0, -1).normalized * forceAmount);
        }
    }
}
