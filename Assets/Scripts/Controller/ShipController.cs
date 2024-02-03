using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// TODO: Spin propeller based on force
// TODO: Add a swirly water effect behind the propeller + white lines rotating around the propeller

public class ShipController : MonoBehaviour
{
    public float forceMultiplier = 100f;
    public float rotationMultiplier = 10f;
    private float propellerRotationCoefficient = 1.0f;
    public bool hasRudder = true;
    public bool holdingRudder = false;
    
    public TextMeshProUGUI forceText;
    public TextMeshProUGUI rotationText;
    public TextMeshProUGUI currentSpinText;
    public TextMeshProUGUI currentAngleText;
    public TextMeshProUGUI currentThrustText;
    
    private List<EnginePropellerPair> enginePropellerPairs = new List<EnginePropellerPair>();
    private GameObject savedPropulsionRoot;
    private InputActions inputActions;
    private float returnSpeed = 10f;
    private Vector2 rotateValue;
    private Rigidbody parentRigidbody;
    
    private Vector3 savedInitialPosition;
    private float currentSpin  = 0;
    private float currentAngle  = 0.0f;
    private float currentThrust = 0.0f;
    private Quaternion savedInitialRotation;

    
    struct EnginePropellerPair
    {
        public GameObject EngineJoint;
        public GameObject PropellerJoint;

        public EnginePropellerPair(Transform engine, Transform propeller)
        {
            EngineJoint = engine.GameObject();
            PropellerJoint = propeller.GameObject();
        }
    }
    
    
    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        inputActions = new InputActions();
        inputActions.Ship.Rudder.performed += ctx => rotateValue = ctx.ReadValue<Vector2>();
        inputActions.Ship.Rudder.canceled += ctx => rotateValue = Vector2.zero;
        inputActions.Debug.ResetButton.performed += ctx => ResetPosition();
    }
    
    
    private void Start()
    {
        savedInitialPosition = transform.position;
        savedInitialRotation = transform.rotation;
        savedPropulsionRoot = GameObject.Find("Propulsion");
        parentRigidbody = GetComponent<Rigidbody>();
        SearchAndAssignChild(savedPropulsionRoot);
        print(enginePropellerPairs.Count); // Debugging
    }

    
    private void FixedUpdate()
    {
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();
        float netForce = rightTriggerValue - leftTriggerValue; // Net force is in the range of -1 to 1.
        float leftStickValue = inputActions.Ship.Rudder.ReadValue<Vector2>().x;
        
        if (savedPropulsionRoot) WorkOnJoints(leftStickValue, netForce);
        
        forceText.text = "Force: " + netForce.ToString("F2");
        rotationText.text = "Rotation: " + leftStickValue.ToString("F2");
        currentAngleText.text = "Current Angle: " + currentAngle.ToString("F0");
        currentThrustText.text = "Current Force: " + currentThrust.ToString("F0");
        currentSpinText.text = "Current Spin: " + (currentSpin / Time.deltaTime).ToString("F0") + " degrees/frame";
    }
    
    
    /// Iterate over each pair in the enginePropellerPairs list, and apply transformations and force on the joints
    private void WorkOnJoints(float leftStickValue, float netForce)
    {
        foreach (EnginePropellerPair pair in enginePropellerPairs)
        {
            Transform engineJoint = pair.EngineJoint.transform;
            Transform propellerJoint = pair.PropellerJoint.transform;
            
            if (engineJoint.name == "EngineJoint")
            {
                RotateRudder(GetDesiredRotation(leftStickValue, engineJoint));
                ApplyForce(netForce, currentAngle, propellerJoint.position);
                RotatePropeller(propellerJoint);
            }
            else if (engineJoint.name == "EngineJointBow")
            {
                RotateRudder(GetDesiredRotation(-leftStickValue, engineJoint));
                ApplyForce(netForce, -currentAngle , propellerJoint.position);
                RotatePropeller(propellerJoint);
            }
        }
    }
    
    
    /// Find all engine joints as children of the propulsion object and populate the list
    private void SearchAndAssignChild(GameObject firstTargetChild)
    {
        foreach (Transform engineJoint in firstTargetChild.transform)
        {
            Transform propellerJoint = engineJoint.Find("PropellerJoint");
            if (propellerJoint != null)
                enginePropellerPairs.Add(new EnginePropellerPair(engineJoint, propellerJoint));
        }
    }
    
    
    /// Apply force to the global parent rigidbody at the propeller joint position 
    private void ApplyForce(float force, float angle, Vector3 position)
    {
        float finalForce = force * forceMultiplier;
        finalForce = Mathf.Clamp(finalForce, -500f, 2750f);
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        Vector3 direction = rotation * transform.forward;
        currentThrust = finalForce;
        parentRigidbody.AddForceAtPosition(direction * finalForce, position);
    }
    
    
    /// Rotate the rudder based on the GetDesiredRotation function return values 
    private void RotateRudder((Quaternion targetRotation, Transform joint, float desiredRotation) data)
    {
        if (holdingRudder)
            data.joint.localRotation = data.targetRotation;
        else if (data.desiredRotation < -0.001 || data.desiredRotation > 0.001)
            data.joint.localRotation = Quaternion.Lerp(data.joint.localRotation, data.targetRotation, returnSpeed);
        else
            ResetRotation(data.joint.transform);
    }
    
    
    /// Get the desired rotation based on the input value and the joint's current rotation
    private (Quaternion, Transform, float) GetDesiredRotation(float rotationValue, Transform joint) 
    {
        float desiredRotation = - rotationValue * rotationMultiplier;
        float newRotation = joint.localEulerAngles.y + desiredRotation;
        newRotation = NormalizeAngle(newRotation);
        if (newRotation > 300) newRotation -= 360; // To avoid snapping at 0/360 degrees
        currentAngle = Mathf.Clamp(newRotation, -45f, 45f);
        Quaternion targetRotation = Quaternion.Euler(0, currentAngle, 0);
        return (targetRotation, joint, desiredRotation);
    }
    
    
    private void RotatePropeller(Transform joint)
    {
        // Determine the amount to rotate this frame based on thrust and time
        float rotationThisFrame = currentThrust * rotationMultiplier * Time.deltaTime;
        rotationThisFrame *= propellerRotationCoefficient;

        // Get the current rotation
        Quaternion currentRotation = joint.localRotation;

        // Calculate the new rotation by adding the rotationThisFrame to the current z-axis rotation
        Quaternion newRotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y, currentRotation.eulerAngles.z + rotationThisFrame);

        // Apply the new rotation to the joint
        joint.localRotation = newRotation;
        currentSpin = rotationThisFrame;
    }
    
    
    /// Smoothly reset the rotation of the joint to 0
    private void ResetRotation(Transform joint)
    {
        float currentYRotation = NormalizeAngle(joint.localEulerAngles.y);
        float newAngle = Mathf.MoveTowardsAngle(currentYRotation, 0, returnSpeed);//Time.deltaTime * 
        joint.localRotation = Quaternion.Euler(0, newAngle, 0);
    }
    
    
    /// Normalize an angle to [0, 360) degrees
    private float NormalizeAngle(float angle)
    {
        // while (angle < 0.0f) angle += 360.0f;
        // while (angle >= 360.0f) angle -= 360.0f;
        angle = angle % 360;
        if (angle < 0)
        {
            angle += 360;
        }
        return angle;
    }


    /// Callback function for the reset button
    private void ResetPosition() 
    {
        float positionThreshold = 2.0f; 
        float rotationThreshold = 10.0f; 
        float positionDifference = Vector3.Distance(transform.position, savedInitialPosition);
        float rotationDifference = Quaternion.Angle(transform.rotation, savedInitialRotation);
        if (positionDifference > positionThreshold || rotationDifference > rotationThreshold)
        {
            transform.position = savedInitialPosition;
            transform.rotation = savedInitialRotation;
            if (parentRigidbody != null)
            {
                parentRigidbody.velocity = Vector3.zero;
                parentRigidbody.angularVelocity = Vector3.zero;
                parentRigidbody.ResetInertiaTensor(); // Optional, use if we need to reset rotational velocities due to inertia changes
            }
            print("Position and rotation reset. Forces removed.");
        }
        else
            print("Reset hit, but position and rotation are within thresholds.");
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
