using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

public class VoxelizedBuoyancy : MonoBehaviour
{
    private List<Vector3> globalPositions = new List<Vector3>();
    private List<Vector3> relativePositions = new List<Vector3>();
    private Vector3 parentPosition;
    private int voxelSize = 6;
    private int voxelVolume = 1;
    
    private Rigidbody rb;
    
    public void Initialize()
    {
        voxelVolume = voxelSize * voxelSize * voxelSize;
        
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    private void FixedUpdate()
    {
        
    }
}
