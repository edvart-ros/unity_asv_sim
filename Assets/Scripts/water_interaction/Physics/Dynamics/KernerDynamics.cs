using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;
using System.ComponentModel;

public class KernerDynamics : MonoBehaviour
{

    public bool viscousResistActive;
    public bool pressureDragActive;
    public bool debugPressureDrag;
    public bool debugResist;

    [Range(0.0f, 1000.0f)]
    public float pressureDragLinearCoefficient = 100;
    [Range(0.0f, 1000.0f)]
    public float pressureDragQuadraticCoefficient = 30.0f;
    [Range(0.01f, 5.0f)]
    public float pressureDragVelocityRef = 1.0f;
    [Range(0.1f, 4.0f)]
    public float pressureDragFalloffPower = 1.0f;

    public Vector3 totalViscousForce;
    public Vector3 totalPressureForce;

    private Submerged submerged;
    private float[] submergedFaceAreas;
    private Buoyancy buoyancy;
    private Rigidbody rigidBody;


    public float hullZMin = -2.5f;
    public float hullZMax = 2.9f;


    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        buoyancy = GetComponent<Buoyancy>();
        submerged = buoyancy.submerged;
    }

    void FixedUpdate()
    {
        totalViscousForce = Vector3.zero;
        totalPressureForce = Vector3.zero;

        submerged = buoyancy.submerged;
        submergedFaceAreas = Utils.CalculateTriangleAreas(submerged.mesh);

        if (viscousResistActive)
        {
            float Cfr = submerged.GetResistanceCoefficient(rigidBody.velocity.magnitude, hullZMin, hullZMax);
            ApplyViscousResistance(Cfr);
        }
        if (pressureDragActive)
        {
            ApplyPressureDrag(pressureDragLinearCoefficient, pressureDragQuadraticCoefficient, pressureDragVelocityRef, pressureDragFalloffPower);
        }

    }

    private void ApplyViscousResistance(float Cfr, float density = Constants.rho)
    {
        Vector3[] vertices = submerged.mesh.vertices;
        int[] triangles = submerged.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 n, Ci, GCi, vi, viTan, ufi, vfi, Fvi;
        Vector3 vCurrent = new Vector3(1.0f, 0.0f, 0.0f);
        for (int i = 0; i < submerged.FaceCentersWorld.Length; i++)
        {
            n = submerged.FaceNormalsWorld[i].normalized;
            Ci = submerged.FaceCentersWorld[i];
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
            rigidBody.AddForceAtPosition(Fvi, Ci);
            totalViscousForce += Fvi;
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

    private void ApplyPressureDrag(float Cpd1, float Cpd2, float vRef, float fp)
    {
        Vector3[] vertices = submerged.mesh.vertices;
        int[] triangles = submerged.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 Fpd, v0, v1, v2, ni, Ci, GCi, vi, ui;
        float Si, cosThetai, viMag;
        for (int i = 0; i < triangles.Length - 2; i += 3)
        {
            (v0, v1, v2) = (vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
            ni = submerged.FaceNormalsWorld[i / 3].normalized;
            Ci = submerged.FaceCentersWorld[i / 3];
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
                Fpd = -(Cpd1 * (viMag / vRef) + Cpd2 * ((viMag * viMag) / (vRef * vRef))) * Si * Mathf.Pow(cosThetai, fp) * ni;
            }
            rigidBody.AddForceAtPosition(Fpd, Ci);
            totalPressureForce += Fpd;
            if (debugPressureDrag)
            {
                Debug.DrawRay(Ci, Fpd, Color.white);
            }
        }
        if (debugPressureDrag) {
            // Debug.DrawRay(transform.position, totalPressureForce/100, Color.green);
        }
        return;
    }
}