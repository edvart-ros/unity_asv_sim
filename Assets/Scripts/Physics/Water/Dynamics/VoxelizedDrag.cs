using System;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.IO;
using WaterInteraction;
using TMPro;

// TODO: Implement damping, critical damping




namespace Physics.Water.Dynamics
{
    public class VoxelizedDrag
    {
        public Vector3 CalculateDragForce(Vector3 velocity, float area, float dragCoefficient) 
        {
            Vector3 dragForce = 0.5f * Constants.waterDensity * velocity.magnitude * velocity * dragCoefficient * area;
            return -dragForce; // Drag force is applied in the opposite direction of velocity
        }
        
        public void ApplyDrag(Rigidbody rb, float area, float dragCoefficient) 
        {
            Vector3 velocity = rb.velocity;
            Vector3 dragForce = CalculateDragForce(velocity, area, dragCoefficient);
            rb.AddForce(dragForce);
        }
    }
}