using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform ship; // Reference to the ship's transform
    private InputActions inputActions;
    private Vector2 panInput;
    public float rotationSpeed = 100.0f; // Speed of camera rotation

    private void Awake()
    {
        inputActions = new InputActions();

        inputActions.Camera.Pan.performed += ctx => panInput = ctx.ReadValue<Vector2>();
        inputActions.Camera.Pan.canceled += ctx => panInput = Vector2.zero;
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    
    void Update()
    {
        if (panInput != Vector2.zero)
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

            transform.localRotation = currentRotation;
        }

        // Update the camera's position to follow the ship
        transform.position = ship.position;
    }
}