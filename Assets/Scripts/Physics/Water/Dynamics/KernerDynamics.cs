using UnityEngine;
using System;
using WaterInteraction;

public class KernerDynamics : MonoBehaviour
{

    public bool viscousResistActive;
    public bool pressureDragActive;
    public bool debugPressureDrag;
    public bool debugResist;

    [Range(0.0f, 5.0f)]
    public float viscousForceScale = 1f;
    [Range(0.0f, 1000.0f)]
    public float pressureDragLinearCoefficient = 100;
    [Range(0.0f, 1000.0f)]
    public float pressureDragQuadraticCoefficient = 30.0f;
    [Range(0.01f, 5.0f)]
    public float pressureDragVelocityRef = 1.0f;
    [Range(0.1f, 4.0f)]
    public float pressureDragFalloffPower = 1.0f;    

    [Range(0.0f, 1000.0f)]
    public float suctionDragLinearCoefficient = 100;
    [Range(0.0f, 1000.0f)]
    public float suctionDragQuadraticCoefficient = 30.0f;
    [Range(0.1f, 4.0f)]
    public float suctionDragFalloffPower = 1.0f;


    private Submerged submerged;
    private float[] submergedFaceAreas;
    private Buoyancy buoyancy;
    private Rigidbody rigidBody;
    private int[] submergedMeshTriangles;
    private Vector3[] submergedMeshVertices;


    public float hullZMin = -2.5f;
    public float hullZMax = 2.9f;


    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
    }

    void FixedUpdate()
    {
        rigidBody = GetComponent<Rigidbody>();
        submerged = GetComponent<Submersion>().submerged;
        submergedMeshTriangles = submerged.data.triangles;
        submergedMeshVertices = submerged.data.vertices;
        submergedFaceAreas = Utils.CalculateTriangleAreas(submerged.data);

        if (viscousResistActive)
        {
            float Cfr = submerged.GetResistanceCoefficient(rigidBody.velocity.magnitude, hullZMin, hullZMax, submerged.data);
            ApplyViscousResistance(Cfr);
        }
        if (pressureDragActive) 
        {
            ApplyPressureDrag(pressureDragLinearCoefficient, 
                              pressureDragQuadraticCoefficient, 
                              suctionDragLinearCoefficient,
                              suctionDragQuadraticCoefficient,
                              pressureDragVelocityRef, 
                              pressureDragFalloffPower,
                              suctionDragFalloffPower);
        }

    }

    private void ApplyViscousResistance(float Cfr, float density = Constants.waterDensity)
    {
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 n, Ci, GCi, vi, viTan, ufi, vfi, Fvi;
        Transform t = submerged.data.transform;
        for (int i = 0; i < submerged.data.maxTriangleIndex/3; i++)
        {
            n = t.InverseTransformDirection(submerged.data.normals[i]).normalized;
            Ci = submerged.data.faceCentersWorld[i];
            GCi = Ci - G;
            vi = vG + Vector3.Cross(omegaG, GCi); // - vCurrent;

            viTan = vi - (Vector3.Dot(vi, n)) * n;
            ufi = -viTan / (viTan.magnitude);
            if (float.IsNaN(ufi.x))
            {
                continue;
            }
            vfi = vi.magnitude * ufi;
            Fvi = (0.5f) * density * Cfr * submergedFaceAreas[i] * vfi.magnitude * vfi;
            rigidBody.AddForceAtPosition(Fvi*viscousForceScale, Ci);
            if (debugResist)
            {
                Debug.DrawRay(Ci, Fvi, Color.red);
            }
        }
        if (debugResist) {
                // Debug.DrawRay(transform.position, totalViscousForce/100, Color.red);
        }
        return;
    }

    private void ApplyPressureDrag(float Cpd1, float Cpd2, float Csd1, float Csd2, float vRef, float fp, float fd)
    {
        Vector3[] vertices = submergedMeshVertices;
        int[] triangles = submergedMeshTriangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 Fpd, v0, v1, v2, ni, Ci, GCi, vi, ui;
        Transform t = submerged.data.transform;
        float Si, cosThetai, viMag;
        for (int i = 0; i < submerged.data.maxTriangleIndex - 2; i += 3)
        {
            (v0, v1, v2) = (vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
            ni = t.InverseTransformDirection(submerged.data.normals[i/3]);
            Ci = t.TransformPoint((v0+v1+v2)/3);
            Si = (0.5f) * Vector3.Cross((v1 - v0), (v2 - v0)).magnitude;

            GCi = Ci - G;
            vi = vG + Vector3.Cross(omegaG, GCi);
            ui = vi.normalized;
            cosThetai = Vector3.Dot(ui, ni);

            viMag = vi.magnitude;
            if (viMag == 0.0f)
            {
                continue;
            }
            if (cosThetai <= 0.0f)
            {
                Fpd = (Cpd1 * (viMag / vRef) + Cpd2 * ((viMag * viMag) / (viMag * viMag))) * Si * Mathf.Pow(Mathf.Abs(cosThetai), fp) * ni;
            }
            else
            {
                Fpd = -(Csd1 * (viMag / vRef) + Csd2 * ((viMag * viMag) / (vRef * vRef))) * Si * Mathf.Pow(cosThetai, fp) * ni;
            }
            rigidBody.AddForceAtPosition(Fpd, Ci);
            if (debugPressureDrag) Debug.DrawRay(Ci, Fpd, Color.white);
        }
        return;
    }
}