using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleDrag : MonoBehaviour
{
    public Vector3 linearCoefficients = new Vector3(1f, 1f, 1f);
    public Vector3 quadraticCoefficients = new Vector3(1f, 1f, 1f);
    public Vector3 cubicCoefficients = new Vector3(1f, 1f, 1f);

    public Vector3 angularLinearCoefficients = new Vector3(1f, 1f, 1f);
    public Vector3 angularQuadraticCoefficients = new Vector3(1f, 1f, 1f);
    public Vector3 angularCubicCoefficients = new Vector3(1f, 1f, 1f);

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ApplyDrag();
    }

    void ApplyDrag()
    {
        // Translational drag
        Vector3 velocity = transform.InverseTransformDirection(rb.velocity);
        Vector3 dragForce = CalculateDragForce(velocity, linearCoefficients, quadraticCoefficients, cubicCoefficients);
        rb.AddRelativeForce(-dragForce, ForceMode.Force);

        // Rotational drag
        Vector3 angularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 angularDragTorque = CalculateDragForce(angularVelocity, angularLinearCoefficients, angularQuadraticCoefficients, angularCubicCoefficients);
        rb.AddTorque(transform.TransformDirection(-angularDragTorque), ForceMode.Force);
    }

    Vector3 CalculateDragForce(Vector3 velocity, Vector3 linear, Vector3 quadratic, Vector3 cubic)
    {
        Vector3 force = Vector3.zero;
        force.x = CalculateDragForAxis(velocity.x, linear.x, quadratic.x, cubic.x);
        force.y = CalculateDragForAxis(velocity.y, linear.y, quadratic.y, cubic.y);
        force.z = CalculateDragForAxis(velocity.z, linear.z, quadratic.z, cubic.z);
        return force;
    }

    float CalculateDragForAxis(float speed, float linear, float quadratic, float cubic)
    {
        return linear * speed + quadratic * speed*Math.Abs(speed) + cubic * Mathf.Pow(speed, 3);
    }
}
