using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject ship; // The ship that the camera will follow
    public float rotationSpeed = 100.0f; // Speed of camera rotation

    // Update is called once per frame
    void Update()
    {
        // Calculate the new rotation as a Quaternion
        float horizontalInput = Input.GetAxis("Mouse X");
        Quaternion rotation = Quaternion.Euler(0, horizontalInput * rotationSpeed * Time.deltaTime, 0);

        // Apply the rotation to the camera's parent (the node)
        transform.parent.transform.Rotate(rotation.eulerAngles, Space.World);

        // Make sure the camera is always looking at the ship
        transform.LookAt(ship.transform);
    }
}