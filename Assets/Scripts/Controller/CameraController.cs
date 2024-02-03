using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

//  TODO: Add a camera follow mode with curve back to default if moved
// TODO: Add a camera zoom mode

public class CameraController : MonoBehaviour
{
    public Vector3 cameraOffset = new Vector3(0, 0, 0);
    public Transform followTarget; // Reference to the ship's transform
    public float rotationSpeed = 100.0f; // Speed of camera rotation
    public bool enableMousePan = true;
    public bool cameraFollow = false;
    
    private InputActions inputActions;
    private Vector2 panInput;

    
    private void Awake()
    {
        inputActions = new InputActions();
        inputActions.Camera.Pan.performed += ctx => panInput = ctx.ReadValue<Vector2>();
        inputActions.Camera.Pan.canceled += ctx => panInput = Vector2.zero;
        //inputActions.Debug.ToggleFollowMode.performed += _ => ToggleFollowMode();
        if (!enableMousePan) DisableMouseInput();
    }

    
    void Update()
    {
        if (panInput != Vector2.zero) 
            FreeAim();
        else if (cameraFollow) 
            FollowTarget(); 
        
        // Update the camera's position to follow the ship
        transform.position = followTarget.position - cameraOffset;
    }
    
    
    /// Free camera rotation
    private void FreeAim()
    {
        // Calculate the new rotation as a Quaternion
        float horizontalInput = panInput.x;
        float verticalInput = panInput.y;

        Quaternion horizontalRotation = Quaternion.Euler(0, horizontalInput * rotationSpeed * Time.deltaTime, 0);

        // Calculate and clamp the x rotation separately
        float xRotation = transform.localEulerAngles.x - verticalInput * rotationSpeed * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, 1, 70);

        // Apply the rotation to the camera's transform
        Quaternion currentRotation = transform.localRotation;
        currentRotation *= horizontalRotation;
        currentRotation.eulerAngles = new Vector3(xRotation, currentRotation.eulerAngles.y, 0);

        // Apply the rotation to the camera's transform
        transform.localRotation = currentRotation;
    }
    
    
    /// Follow camera mode
    private void FollowTarget()
    {
        // Calculate the target rotation based on the followTarget's forward direction
        Quaternion targetRotation = Quaternion.Euler(10f, followTarget.eulerAngles.y, 0);
        // Smoothly interpolate the camera's rotation towards the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 0.02f* rotationSpeed);
    }
    
    
    private void DisableMouseInput()
    {
        // Get all mouse devices
        var mouseDevices = InputSystem.devices.Where(device => device is Mouse);

        foreach (var mouse in mouseDevices)
        {
            InputSystem.DisableDevice(mouse);
        }
    }
    
    
    private void ToggleFollowMode()
    {
        cameraFollow = !cameraFollow;
        // Optionally reset the camera to its default position behind the target when entering follow mode
    }
    
    
    private void OnEnable()
    {
        inputActions.Enable();
    }

    
    private void OnDisable()
    {
        inputActions.Disable();
    }
}