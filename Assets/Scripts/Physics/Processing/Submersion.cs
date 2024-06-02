using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;
using System.Diagnostics;
using Unity.Collections;
using WaterInteraction;
using UnityEngine;
using System.IO;
using PlasticPipe.PlasticProtocol.Client;


public class Submersion : MonoBehaviour
{
    [ReadOnly]
    public Submerged submerged;
    
    [Tooltip("HDRP water surface used for height querying")]
    public WaterSurface waterSurface = null;
    [Tooltip("A simplified mesh for physics calculations")]
    public Mesh simplifiedMesh;
    [Tooltip("Side length of square water surface approximation patch. Much be large enough to fit entire vessel")]
    public float patchSize = 10;
    [Tooltip("Higher number gives a better approximation of water surface")]
    public int patchResolution = 4;
    
    private Patch patch;
    //public bool drawWaterLine;

    public bool drawPatch;
    public bool drawSubmerged;
    private float displacedVolume;
    
    
    void Start()
    {
        Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
        patch = new Patch(waterSurface, patchSize, patchResolution, gridOrigin);
        submerged = new Submerged(simplifiedMesh, debug:true); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        
    }

    void FixedUpdate()
    {
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        submerged.Update(patch, transform);
        
        displacedVolume = submerged.data.volume;
        
        if (drawPatch) DebugPatch();
        if (drawSubmerged) DebugSubmerged();
    }
    
    
    private void DebugPatch()
    {
        int[] tris = patch.patchTriangles;
        Vector3[] verts = patch.patchVertices;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Vector3[] tri = new Vector3[] { verts[tris[i]], verts[tris[i+1]], verts[tris[i+2]] };
            Utils.DebugDrawTriangle(tri, Color.red);
        }
    }
    
    
    private void DebugSubmerged() 
    {
        int[] tris = submerged.data.triangles;
        Vector3[] verts = submerged.data.vertices;

        for (int i = 0; i < submerged.data.maxTriangleIndex - 2; i += 3) 
        {
            Vector3[] tri = new Vector3[]
            {
            transform.TransformPoint(verts[tris[i]]),
            transform.TransformPoint(verts[tris[i + 1]]),
            transform.TransformPoint(verts[tris[i + 2]])
            };

            Utils.DebugDrawTriangle(tri, Color.green);
        }
    }
}
