using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Diagnostics;

public class WindForce : MonoBehaviour
{

    public Wind wind;
    public Rigidbody rb;
    public Mesh mesh;
    public bool debug;

    private Vector3[] verts;
    private Vector3 windVector;
    private Vector3[] normals;

    private int[] tris;
    private int numTris;
    private Vector3 totalWindForce;
    private Buoyancy buoyancy;
    private KernerDynamics kernerDynamics;
    

    void Start()
    {
        verts = mesh.vertices;
        tris = mesh.triangles;
        normals = mesh.normals;
        numTris = tris.Length / 3;
        windVector = new Vector3(-wind.speed*Mathf.Sin(wind.direction), 0, wind.speed*Mathf.Cos(wind.direction));
        buoyancy = GetComponent<Buoyancy>();
        kernerDynamics = GetComponent<KernerDynamics>();
    }

    void FixedUpdate() {
        totalWindForce = Vector3.zero;
        windVector = new Vector3(-wind.speed * Mathf.Sin(wind.direction), 0, wind.speed * Mathf.Cos(wind.direction));

        for (int i = 0; i < numTris; i++) {
            Vector3[] triangle = new Vector3[3] { 
                verts[tris[(i * 3) + 0]], 
                verts[tris[(i * 3) + 1]], 
                verts[tris[(i * 3) + 2]] 
            };

            Vector3 triangleCenter = (triangle[0] + triangle[1] + triangle[2]) / 3;
            Vector3 triangleNormal = WaterInteraction.Utils.GetFaceNormal(triangle[0], triangle[1], triangle[2]);
            float triangleArea = triangleNormal.magnitude;

            Vector3 triangleCenterWorld = transform.TransformPoint(triangleCenter);
            Vector3 triangleNormalWorld = transform.TransformDirection(triangleNormal);

            if (Vector3.Dot(windVector, triangleNormalWorld) < 0) {
                Vector3 force = windVector * (-Vector3.Dot(windVector.normalized, triangleNormalWorld.normalized) * triangleArea);
                totalWindForce += force;
                rb.AddForceAtPosition(force, triangleCenterWorld);
                
                if (debug) { 
                    Debug.DrawRay(triangleCenterWorld, force);
                }
            }
            if (debug) {
                //Debug.DrawRay(Vector3.zero, windVector, Color.red);
            }
        }
        //rb.AddForce(totalWindForce);
        Debug.DrawRay(transform.position, totalWindForce/100);

    }
}
