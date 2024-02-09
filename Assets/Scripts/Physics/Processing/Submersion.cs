using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using WaterInteraction;
using Unity.Collections;
using UnityEditor.Callbacks;

public class Submersion : MonoBehaviour
{
    public WaterSurface targetSurface = null;
    public Mesh simplifiedMesh;
    public float patchSize = 10;
    public int patchResolution = 4;
    public bool drawPatch;
    public bool drawSubmerged;
    private Patch patch;
    public bool drawWaterLine;

    [ReadOnly]
    public Submerged submerged;
    
    // Start is called before the first frame update
    void Start()
    {
        Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
        patch = new Patch(targetSurface, patchSize, patchResolution, gridOrigin);
        submerged = new Submerged(simplifiedMesh, debug:true); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        submerged.Update(patch, transform);
        if (drawPatch) DebugPatch();
        if (drawSubmerged) DebugSubmerged();
        if (drawWaterLine) DebugWaterLine();

    }
    
    private void DebugPatch()
    {
        int[] tris = patch.baseGridMesh.triangles;
        Vector3[] verts = patch.patchVertices;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Vector3[] tri = new Vector3[] { verts[tris[i]], verts[tris[i+1]], verts[tris[i+2]] };
            Utils.DebugDrawTriangle(tri, Color.red);
        }
    }
    private void DebugSubmerged() {
        int[] tris = submerged.newSubmergedMesh.triangles;
        Vector3[] verts = submerged.newSubmergedMesh.vertices;

        for (int i = 0; i < tris.Length - 2; i += 3) {
            Vector3[] tri = new Vector3[]
            {
            transform.TransformPoint(verts[tris[i]]),
            transform.TransformPoint(verts[tris[i + 1]]),
            transform.TransformPoint(verts[tris[i + 2]])
            };

            Utils.DebugDrawTriangle(tri, Color.green);
        }
    }

    private void DebugWaterLine(){
        Vector3[] verts = submerged.waterLineVerts;
        for (int i = 0; i < verts.Length-1; i+=2
        ){
            Debug.DrawLine(transform.TransformPoint(verts[i]), transform.TransformPoint(verts[i+1]), Color.magenta);
        }
    }





    private void OnDrawGizmos() {
        //Gizmos.color = Color.magenta;
        //Gizmos.DrawCube(submerged.centroid, 0.28f*Vector3.one);
        /*
        Gizmos.color = Color.red;
        Vector3[] centroids = submerged.centroidsUp;
        foreach (var c in centroids) Gizmos.DrawSphere(c, 0.01f);
        Gizmos.DrawCube(submerged.centroidUp, 0.2f*Vector3.one);   
        
        Gizmos.color = Color.green;
        centroids = submerged.centroidsDown;
        foreach (var c in centroids) Gizmos.DrawSphere(c, 0.01f);
        Gizmos.DrawCube(submerged.centroidDown, 0.2f*Vector3.one);
        */

    }

}
