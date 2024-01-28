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

    // Update is called once per frame
    void Update()
    {
        // Calculate the new rotation as a Quaternion
        float horizontalInput = panInput.x;
        float verticalInput = panInput.y;
        Quaternion horizontalRotation = Quaternion.Euler(0, horizontalInput * rotationSpeed * Time.deltaTime, 0);
        Quaternion verticalRotation = Quaternion.Euler(-verticalInput * rotationSpeed * Time.deltaTime, 0, 0);

        // Apply the rotation to the camera's transform
        Quaternion currentRotation = transform.localRotation;
        currentRotation *= horizontalRotation;
        currentRotation *= verticalRotation;

        // Clamp the x rotation between -45 and 90 degrees
        Vector3 eulerAngles = currentRotation.eulerAngles;
        eulerAngles.x = Mathf.Clamp(eulerAngles.x, 1, 70);
        eulerAngles.z = 0;
        currentRotation.eulerAngles = eulerAngles;

        // Apply the clamped rotation to the camera's transform
        transform.localRotation = currentRotation;

        // Update the camera's position to follow the ship
        transform.position = ship.position;
    }
}