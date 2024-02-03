using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class InsideChecker : MonoBehaviour {
    private MeshCollider meshCollider;
    private Mesh mesh;
    private Bounds bounds;
    public int subdivisionsX = 40;
    public int subdivisionsY = 10;
    public int subdivisionsZ = 10;
    public bool drawAllVoxels = false;
    public bool drawInside = true;
    private float W;
    private float H;
    private float D;
    private Vector3 origin;
    private int numBoxes;
    private Vector3[] boxPositions;

    public void Test() {
        meshCollider = GetComponent<MeshCollider>();
        mesh = meshCollider.sharedMesh;
        // Get the local bounds (unscaled)
        bounds = mesh.bounds;
        bounds = ChangeBoundsSize(bounds, 1.0f);

        W = bounds.size.x / subdivisionsX;
        H = bounds.size.y / subdivisionsY;
        D = bounds.size.z / subdivisionsZ;
        numBoxes = subdivisionsX * subdivisionsY * subdivisionsZ;
        boxPositions = new Vector3[numBoxes];

        origin = bounds.min + 0.5f * new Vector3(W, H, D);
        int index = 0;
        for (int i = 0; i < subdivisionsX; i++) {
            for (int j = 0; j < subdivisionsY; j++) {
                for (int k = 0; k < subdivisionsZ; k++) {
                    boxPositions[index] = origin + new Vector3(i * W, j * H, k * D);
                    
                    index++;
                }
            }
        }
    }


    public void Update() {
    }

    public void OnDrawGizmos() {
        //Matrix4x4 rotationMatrix = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.lossyScale);
        //Gizmos.matrix = rotationMatrix;
        Gizmos.color = Color.green;
        if (boxPositions != null) {
            for (int i = 0; i < boxPositions.Length; i++) {
                Vector3 worldPos = transform.TransformPoint(boxPositions[i]);
                Gizmos.DrawLine(transform.TransformPoint(bounds.min), transform.TransformPoint(bounds.max));
                if (drawAllVoxels) {
                    Gizmos.DrawWireCube(worldPos, new Vector3(W, H, D));
                }
                if (drawInside) {
                    if (!Physics.Raycast(worldPos, transform.TransformPoint(bounds.center) - worldPos, 10.0f)) {
                        Gizmos.DrawWireCube(worldPos, new Vector3(W, H, D));
                        //Gizmos.DrawRay(worldPos, transform.TransformPoint(bounds.center) - worldPos);
                    }
                }
            }
        }
    }

    public Bounds ChangeBoundsSize(Bounds boundsIn, float k) {
        Bounds increased = boundsIn;
        increased.extents = boundsIn.extents * k;
        increased.max = boundsIn.center + increased.extents;
        increased.min = boundsIn.center - increased.extents;
        increased.size = increased.extents * 2;

        return increased;
    }
}
