using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;

public class KernerDynamics : MonoBehaviour
{

    public WaterSurface targetSurface = null;
    public float sideLength;
    public GameObject waterPatch;
    public GameObject simplifiedMesh;
    public GameObject submergedMesh;
    public Rigidbody rigidBody;
    public bool buoyancyForceActive;
    public bool viscousResistActive;
    public bool pressureDragActive;
    public bool debugBuoyancy;
    public bool debugPressureDrag;
    public bool debugResist;

    [Range(0.0f, 1000.0f)]
    public float pressureDragLinearCoefficient = 500.0f;
    [Range(0.0f, 1000.0f)]
    public float pressureDragQuadraticCoefficient = 200.0f;
    [Range(0.01f, 5.0f)]
    public float pressureDragVelocityRef= 1.0f;
    [Range(0.1f, 4.0f)]
    public float pressureDragFalloffPower = 0.7f;
    

    private MeshFilter waterPatchMeshFilter;
    private MeshFilter simplifiedMeshFilter;
    private MeshFilter submergedMeshFilter;
    private Patch patch;
    private Submerged submerged;
    private float[] submergedFaceAreas;


    private const int gridFidelity = 4;
    private const float hullZMin = -2.5f;
    private const float hullZMax = 2.9f;
    private const float boatLength = hullZMax-hullZMin;

    void Start()
    {
        waterPatchMeshFilter = waterPatch.GetComponent<MeshFilter>(); // the water patch used for fast water height look-up
        simplifiedMeshFilter = simplifiedMesh.GetComponent<MeshFilter>(); // the simplified hull used for submerged mesh calculation
        submergedMeshFilter = submergedMesh.GetComponent<MeshFilter>(); // the calculated submerged parts of the hull- used to calculate the buoyancy forces
        Vector3 gridOrigin = new Vector3(-sideLength/2, 0, sideLength/2);
        patch = new Patch(targetSurface, sideLength, gridFidelity, gridOrigin);
        submerged = new Submerged(simplifiedMeshFilter.mesh); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height

    }

    void FixedUpdate(){
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        waterPatchMeshFilter.mesh.vertices = patch.patchVertices; // assign the resulting patch vertices
        submerged.Update(patch, transform);
        submergedMeshFilter.mesh = submerged.mesh;
        float submergedMeshArea = Utils.CalculateMeshArea(submergedMeshFilter.mesh);
        submergedFaceAreas = Utils.CalculateTriangleAreas(submergedMeshFilter.mesh);
                
        if (buoyancyForceActive){
            ApplyBuoyancy();
            //ApplyBuoyancyCP();

        }
        if (viscousResistActive){
            float Cfr = submerged.GetResistanceCoefficient(rigidBody.velocity.magnitude, hullZMin, hullZMax);
            ApplyViscousResistance(Cfr);
        }
        if (pressureDragActive){
            ApplyPressureDrag(pressureDragLinearCoefficient, pressureDragQuadraticCoefficient, pressureDragVelocityRef, pressureDragFalloffPower);
        }

    }


    private void ApplyBuoyancy(){
        float[] heights = submerged.FaceCenterHeightsAboveWater;
        Vector3[] centersWorld = submerged.FaceCentersWorld;
        Vector3[] normalsWorld = submerged.FaceNormalsWorld;
        for (var i = 0; i < centersWorld.Length; i++){
            if (normalsWorld[i].y >  0){
                continue;
            }
            Vector3 F = Forces.BuoyancyForce(heights[i], normalsWorld[i]);
            if (debugBuoyancy){
                Debug.DrawRay(centersWorld[i], F, Color.green);
            }
            rigidBody.AddForceAtPosition(F, centersWorld[i]);
        }
    }

    private void ApplyViscousResistance(float Cfr, float density=Constants.rho){
        Vector3[] vertices = submergedMeshFilter.mesh.vertices;
        int[] triangles = submergedMeshFilter.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        for (int i = 0; i < submerged.FaceCentersWorld.Length; i++){
            Vector3 n = submerged.FaceNormalsWorld[i].normalized;
            Vector3 Ci = submerged.FaceCentersWorld[i];
            Vector3 GCi = Ci - G;
            //something below here becomes NaN. need to debug
            Vector3 vi = vG + Vector3.Cross(omegaG, GCi);
            Vector3 viTan = vi-(Vector3.Dot(vi, n))*n;
            Vector3 ufi = -viTan/(viTan.magnitude);
            if (float.IsNaN(ufi.x)){
                continue;
            }
            Vector3 vfi = vi.magnitude*ufi;
            Vector3 Fvi = (0.5f)*density*Cfr*submergedFaceAreas[i]*vfi.magnitude*vfi;
            rigidBody.AddForceAtPosition(Fvi, Ci);
            if (debugResist){
                Debug.DrawRay(Ci, Fvi, Color.red);
            }
        }
        return;
    }

    private void ApplyPressureDrag(float Cpd1, float Cpd2, float vRef, float fp){
        Vector3[] vertices = submerged.mesh.vertices;
        int[] triangles = submerged.mesh.triangles;
        Vector3 vG = rigidBody.velocity;
        Vector3 omegaG = rigidBody.angularVelocity;
        Vector3 G = rigidBody.position;
        Vector3 Fpd;
        for (int i = 0; i < triangles.Length - 2; i +=3){
            (Vector3 v0, Vector3 v1, Vector3 v2) = (vertices[triangles[i]], vertices[triangles[i+1]], vertices[triangles[i+2]]);
            Vector3 ni = submerged.FaceNormalsWorld[i/3].normalized;
            Vector3 Ci = submerged.FaceCentersWorld[i/3];
            float Si = (0.5f)*Vector3.Cross((v1-v0),(v2-v0)).magnitude;

            Vector3 GCi = Ci - G;
            Vector3 vi = vG + Vector3.Cross(omegaG, GCi);
            Vector3 ui = vi.normalized;
            float cosThetai = Vector3.Dot(ui, ni);

            float viMag = vi.magnitude;
            if (viMag == 0.0f){
                continue;
            }
            if (cosThetai <= 0.0f){
                Fpd = (Cpd1*(viMag/vRef) + Cpd2*((viMag*viMag)/(viMag*viMag)))*Si*Mathf.Pow(Mathf.Abs(cosThetai), fp)*ni;
            }
            else{
                Fpd = -(Cpd1*(viMag/vRef) + Cpd2*((viMag*viMag)/(vRef*vRef)))*Si*Mathf.Pow(cosThetai, fp)*ni;
            }
            rigidBody.AddForceAtPosition(Fpd, Ci);
            if (debugPressureDrag){
                Debug.DrawRay(Ci, Fpd, Color.white);
            }
        }
        return;
    }
    private void OnDestroy()
        {
            patch.DisposeRoutine();
        }
}