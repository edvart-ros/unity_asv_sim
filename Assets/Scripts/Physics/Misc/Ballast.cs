using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ballast : MonoBehaviour
{
    public float mass = 30.0f;
    private Rigidbody rb;
    const float g = 9.8067f;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        rb.AddForceAtPosition(Vector3.down * g * mass, transform.position);
    }
}
