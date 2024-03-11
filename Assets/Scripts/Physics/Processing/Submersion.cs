using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;
using System.Diagnostics;
using Unity.Collections;
using WaterInteraction;
using UnityEngine;
using System.IO;


public class Submersion : MonoBehaviour
{
    [ReadOnly]
    public Submerged submerged;
    
    public WaterSurface targetSurface = null;
    public Mesh simplifiedMesh;
    public float patchSize = 10;
    public int patchResolution = 4;
    
    private Patch patch;
    //public bool drawWaterLine;

    // Test frameworks
    public bool drawPatch;
    public bool drawSubmerged;
    public bool logVolumeData;
    public bool logTimeData;
    
    private string path = "Assets/Data/Triangles/";
    private string depthLogFile;
    private string timeLogFile;
    private float displacedVolume;
    private int iteration;
    private Stopwatch stopwatch = new Stopwatch();
    
    
    void Start()
    {
        Vector3 gridOrigin = new Vector3(-patchSize/2, 0, patchSize/2);
        patch = new Patch(targetSurface, patchSize, patchResolution, gridOrigin);
        submerged = new Submerged(simplifiedMesh, debug:true); // set up submersion by providing the simplified hull mesh
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        
        iteration = 0;
        InitializeLogs();
    }

    
    void FixedUpdate()
    {
        stopwatch.Start();
        
        patch.Update(transform); // updates the patch to follow the boat and queried water height
        submerged.Update(patch, transform);
        
        stopwatch.Stop();
        
        displacedVolume = submerged.data.volume;
        
        if (logVolumeData) Utils.LogDataToFile(depthLogFile, -(transform.position.y-0.5f), displacedVolume);
        if (logTimeData && iteration < 100) Utils.LogDataToFile(timeLogFile, iteration++, stopwatch.Elapsed.TotalMilliseconds * 1000.0);
        
        if (drawPatch) DebugPatch();
        if (drawSubmerged) DebugSubmerged();
        stopwatch.Reset();
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
        Debug.Log("volume:" + submerged.data.volume + ", object: " + name);

    }

/*
    private void DebugWaterLine(){
        Vector3[] verts = submerged.waterLineVerts;
        for (int i = 0; i < verts.Length-1; i+=2
        ){
            Debug.DrawLine(transform.TransformPoint(verts[i]), transform.TransformPoint(verts[i+1]), Color.magenta);
        }
    }
*/


    private void InitializeLogs()
    {
        depthLogFile = path + "VolumeData-" + transform.name + ".csv";
        timeLogFile = path + "TimeData-Submersion-" + transform.name + ".csv";

        if (!File.Exists(depthLogFile) && logVolumeData)
        {
            print("Beginning to log volume data");
            Utils.LogDataToFile(depthLogFile,"depth","volume");
            // Add a constant downward force 
            GetComponent<Rigidbody>().velocity = new Vector3(0f, -0.1f, 0f);
        }
        
        if (!File.Exists(timeLogFile) && logTimeData)
        {
            print("Beginning to log time data");
            Utils.LogDataToFile(timeLogFile,"iteration_number","time");
        }
    }
    
    
    private void OnDrawGizmos() 
    {
        //Gizmos.color = Color.magenta;
        //Gizmos.DrawSphere(submerged.centroid, 4f);
    }
}
