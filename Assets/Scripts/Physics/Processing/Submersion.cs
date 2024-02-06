using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using WaterInteraction;
using Unity.Collections;

public class Submersion : MonoBehaviour
{
    public WaterSurface targetSurface = null;
    public Mesh simplifiedMesh;
    public float patchSize = 10;
    public int patchResolution = 4;
    public bool drawPatch;
    public bool drawSubmerged;
    private Patch patch;

    [ReadOnly]
    public Submerged submerged;
    
    // Start is called before the first frame update
    void Start()
    {
        Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
        patch = new Patch(targetSurface, patchSize, patchResolution, gridOrigin);
        submerged = new Submerged(simplifiedMesh); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height
    }

    // Update is called once per frame
    void Update()
    {
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        submerged.Update(patch, transform);
        if (drawPatch) DebugPatch();
        if (drawSubmerged) DebugSubmerged();
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

    private void DebugSubmerged()
    {
        int[] tris = submerged.mesh.triangles;
        Vector3[] verts = submerged.mesh.vertices;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Vector3[] tri = new Vector3[] { 
                transform.TransformPoint(verts[tris[i]]), 
                transform.TransformPoint(verts[tris[i+1]]), 
                transform.TransformPoint(verts[tris[i+2]])};
            Utils.DebugDrawTriangle(tri, Color.green);
        }
    }




/*
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(submerged.centroid, 0.2f);
    }
*/
}
