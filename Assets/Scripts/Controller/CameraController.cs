using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// TODO: Add toggle follow mode button
// TODO: Add a camera zoom mode

public class CameraController : MonoBehaviour
{
    public Vector3 pivotOffset = new Vector3(0, 0, 0);
    public Transform followTarget; // Reference to the ship's transform
    public float rotationSpeed = 100.0f; // Speed of camera rotation
    public bool enableMousePan = true;
    public bool cameraFollow = false;
    public LayerMask collisionLayerMask;
    
    private InputActions inputActions;
    private Vector2 panInput;
    private Transform cameraTransform;
    private Vector3 defaultCameraPosition;
    
    private float delayBeforeFollow = 1.0f; // 2 seconds delay before returning to follow mode
    private float timer = 0f; // Timer to keep track of input delay

    
    private void Awake()
    {
        inputActions = new InputActions();
        inputActions.Camera.Pan.performed += ctx => panInput = ctx.ReadValue<Vector2>();
        inputActions.Camera.Pan.canceled += ctx => panInput = Vector2.zero;
        //inputActions.Debug.ToggleFollowMode.performed += _ => ToggleFollowMode();
        if (!enableMousePan) DisableMouseInput();
        cameraTransform = transform.Find("Camera");
        defaultCameraPosition = cameraTransform.localPosition;
    }
    
    
    void Update()
    {
        if (panInput != Vector2.zero)
        {
            FreeAim();
            timer = delayBeforeFollow;
        }
        else if (timer > 0)
        {
            // If no input and timer is running, countdown
            timer -= Time.deltaTime;
        }
        if (cameraFollow && timer <= 0)
            FollowTarget(); 
        AdjustCameraPosition();
    }
    
    
    /// Free camera rotation
    private void FreeAim()
    {
        Quaternion horizontalRotation = Quaternion.Euler(0, panInput.x * rotationSpeed * Time.deltaTime, 0);
        float verticalRotation = transform.localEulerAngles.x - panInput.y * rotationSpeed * Time.deltaTime;
        verticalRotation = Mathf.Clamp(verticalRotation, 1, 70);

        // Apply the rotation to the camera's transform
        Quaternion currentRotation = transform.localRotation;
        currentRotation *= horizontalRotation;
        currentRotation.eulerAngles = new Vector3(verticalRotation, currentRotation.eulerAngles.y, 0);

        // Apply the rotation to the camera's transform
        transform.localRotation = currentRotation;
    }
    
    
    /// Follow camera mode
    private void FollowTarget()
    {
        Quaternion targetRotation = Quaternion.Euler(10f, followTarget.eulerAngles.y, 0);
        // Smoothly interpolate the camera's rotation towards the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 0.02f* rotationSpeed);
    }
    
    
    /// Move the camera pivot to the followTarget origin.
    /// If there is a collider between the camera and the target, adjust the camera position to avoid clipping
    private void AdjustCameraPosition()
    {
        Vector3 idealCameraPoint = followTarget.position + transform.localRotation * defaultCameraPosition;
        Vector3 pivotPosition = followTarget.position + pivotOffset;
        //Debug.DrawLine(pivotPosition,idealCameraPoint, Color.green);
        transform.position = pivotPosition; 
        Vector3 rayDirection = idealCameraPoint - pivotPosition;
        RaycastHit hit;
        // What not to collide with
        LayerMask exclusionMask = ~collisionLayerMask;
        
        if (Physics.Raycast(pivotPosition, rayDirection.normalized, out hit, rayDirection.magnitude + 2f,exclusionMask))
        {
            // Shift the hitpoint along the ray by a small amount to avoid z-fighting
            hit.point -= rayDirection.normalized * 0.25f;
            //Debug.DrawLine(pivotPosition,  hit.point, Color.cyan);
            //Debug.DrawRay(hit.point,Vector3.up*10f, Color.magenta);
            cameraTransform.position = hit.point;
            //print("Collision detected.");
        }
        else
        {
            // No collision, position the camera at the default offset
            cameraTransform.localPosition = defaultCameraPosition;
        }
    }
    
    
    private void DisableMouseInput()
    {
        var mouseDevices = InputSystem.devices.Where(device => device is Mouse);
        foreach (var mouse in mouseDevices) {InputSystem.DisableDevice(mouse);} 
    }
    
    
    private void ToggleFollowMode()
    {
        cameraFollow = !cameraFollow;
        // Optionally reset the camera to its default position behind the target when entering follow mode
    }
    
    
    private void OnEnable()
    {
        if (inputActions != null) inputActions.Enable();
    }

    
    private void OnDisable()
    {
        if (inputActions != null) inputActions.Disable();
    }
}