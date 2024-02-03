using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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
    
    // TODO: See following list:
    // 1. Read points from file
    // 2. Convert points from local to global coordinates
    // 3. Iterate over the points, finding the distance from water surface.
    // If above water, discard and continue
    // If below water, add to list of points to be used for buoyancy calculation
    // 4. Determine the centre of mass of the points
    // 5. Apply force to this point based on the amount of points total and the volume
    // which is constant for each point, so it is just the number of points * volume * gravity
    
    
    
    
    
    
    
    // Start is called before the first frame update
    void Start()
    {
        // Populate local list with saved points
        relativePositions = LoadPoints("Assets/pointsData.txt");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public List<Vector3> LoadPoints(string path)
    {
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(path, FileMode.Open))
            {
                return formatter.Deserialize(stream) as List<Vector3>;
            }
        }
        else
        {
            Debug.LogError("Save file not found in " + path);
            return null;
        }
    }
    
    private void FixedUpdate()
    {
        
    }
}
