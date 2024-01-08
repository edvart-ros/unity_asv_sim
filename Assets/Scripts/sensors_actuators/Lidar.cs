using UnityEngine;

public class Lidar : MonoBehaviour
{
    [Range(1.0f, 45.0f)]
    public float horizontalFidelityDegrees = 5.0f; 

    [Range(1.0f, 45.0f)]
    public float verticalFidelityDegrees = 10.0f; 

    [Range(0.1f, 5.0f)]
    public float minDistance = 0.3f;

    [Range(0.0f, 2.0f * Mathf.PI)]
    public float horizontalFOV = 2.0f * Mathf.PI; 

    [Range(0.0f, Mathf.PI)]
    public float verticalFOV = 0.5f * Mathf.PI;

    private Vector3[] scanDirVectors;

    void Start()
    {
        scanDirVectors = GenerateScanVectors();
    }

    void FixedUpdate()
    {
        scanDirVectors = GenerateScanVectors();
        RaycastHit hit;
        Vector3 point;

        foreach (Vector3 dir in scanDirVectors)
        {
            if (Physics.Raycast(transform.position, transform.rotation*dir, out hit))
            {
                point = hit.point;
                if ((transform.position-point).magnitude > minDistance){
                    Debug.DrawRay(transform.position, point - transform.position, Color.red);
                }
            }
        }
    }

    private Vector3[] GenerateScanVectors()
    {
        int numRaysHor = Mathf.RoundToInt(horizontalFOV / (horizontalFidelityDegrees*Mathf.Deg2Rad));
        int numRaysVert = Mathf.RoundToInt(verticalFOV / (verticalFidelityDegrees*Mathf.Deg2Rad));

        float fidelityHorizontal = horizontalFOV / numRaysHor;
        float fidelityVertical = verticalFOV / numRaysVert;

        Vector3[] scanVectors = new Vector3[numRaysHor * numRaysVert];
        int index = 0;

        for (int i = 0; i < numRaysHor; i++)
        {
            float hRot = 0.5f*(Mathf.PI - horizontalFOV) + fidelityHorizontal * i;

            for (int j = 0; j < numRaysVert/2; j++)
            {
                float vRot = 0.5f*Mathf.PI-(fidelityVertical * j);

                float x = Mathf.Sin(vRot) * Mathf.Cos(hRot);
                float y = Mathf.Cos(vRot);
                float z = Mathf.Sin(vRot) * Mathf.Sin(hRot);

                scanVectors[index] = new Vector3(x, y, z);
                scanVectors[index+1] = new Vector3(x, -y, z);
                index += 2;
            }
        }
        return scanVectors;
    }
}
