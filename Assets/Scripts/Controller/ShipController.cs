using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// TODO: More realistic propeller rotation
// TODO: Add a swirly water effect behind the propeller + white lines rotating around the propeller


public class ShipController : MonoBehaviour
{
    [System.Serializable]
    public struct UIText
    {
        public TextMeshProUGUI forceText;
        public TextMeshProUGUI rotationText;
        public TextMeshProUGUI currentSpinText;
        public TextMeshProUGUI currentAngleText;
        public TextMeshProUGUI currentThrustText;
    }
    
    public Vector2 forceClamp = new Vector2(-500f, 2750f);
    public float forceMultiplier = 100f;
    public float rotationMultiplier = 10f;
    public bool holdingRudder = false;
    public bool debug = false;
    
    public UIText UI;
    
    private List<EnginePropellerPair> enginePropellerPairs = new List<EnginePropellerPair>();
    private Transform savedPropulsionRoot;
    private InputActions inputActions;
    private float returnSpeed = 10f;
    private Vector2 rotateValue;
    private Rigidbody parentRigidbody;
    //private float propellerRotationCoefficient = 1.0f;

    
    private Vector3 savedInitialPosition;
    private float currentSpin  = 0;
    private float currentAngle  = 0.0f;
    private float currentThrust = 0.0f;
    private Quaternion savedInitialRotation;
    
    private float engineRotationLimit  = 25;
    private Vector3 startDirection;
    
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
        savedPropulsionRoot = transform.Find("PropulsionOutboard") != null ? 
            transform.Find("PropulsionOutboard") : transform.Find("PropulsionRudder");        
        parentRigidbody = GetComponent<Rigidbody>();
        SearchAndAssignChild(savedPropulsionRoot.GameObject());
        //print("Number of EnginePropeller pairs" + enginePropellerPairs.Count); // Debugging
        if (enginePropellerPairs.Count > 0)
        {
            Transform engineJoint = enginePropellerPairs[0].EngineJoint.transform;
            startDirection =  -engineJoint.forward;//Quaternion.Euler(0, engineRotationLimit, 0)*
        }
    }

    
    private void FixedUpdate()
    {
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();
        float netForce = rightTriggerValue - leftTriggerValue; // Net force is in the range of -1 to 1.
        float leftStickValue = inputActions.Ship.Rudder.ReadValue<Vector2>().x;
        
        if (savedPropulsionRoot) WorkOnJoints(leftStickValue, netForce);
        
        if (UI.forceText) UI.forceText.text = "Force: " + netForce.ToString("F2");
        if (UI.rotationText) UI.rotationText.text = "Rotation: " + leftStickValue.ToString("F2");
        if (UI.currentAngleText) UI.currentAngleText.text = "Current Angle: " + currentAngle.ToString("F0");
        if (UI.currentThrustText) UI.currentThrustText.text = "Current Force: " + currentThrust.ToString("F0");
        if (UI.currentSpinText) UI.currentSpinText.text = "Current Spin: " + (currentSpin / Time.deltaTime).ToString("F0") + " degrees/frame";
        }
    
    
    /// Find all engine joints as children of the propulsion object and populate the list
    private void SearchAndAssignChild(GameObject firstTargetChild)
    {
        foreach (Transform engineJoint in firstTargetChild.transform)
        {
            if (firstTargetChild.name == "PropulsionOutboard")
            {
                Transform propellerJoint = engineJoint.Find("PropellerJoint");
                if (propellerJoint != null)
                    enginePropellerPairs.Add(new EnginePropellerPair(engineJoint, propellerJoint));
            }
            else if (firstTargetChild.name == "PropulsionRudder")
            {
                Transform propellerJoint = firstTargetChild.transform.Find("PropellerJoint");
                if (propellerJoint != null)
                    enginePropellerPairs.Add(new EnginePropellerPair(engineJoint, propellerJoint));
            }
            else print("No propulsion object found.");
        }
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
                ApplyForce(netForce, pair);
                RotatePropeller(propellerJoint);
            }
            else if (engineJoint.name == "EngineJointBow")
            {
                RotateRudder(GetDesiredRotation(-leftStickValue, engineJoint));
                ApplyForce(netForce, pair);
                RotatePropeller(propellerJoint);
            }
        }
    }
    
    
    /// Apply force to the global parent rigidbody at the propeller joint position 
    private void ApplyForce(float force, EnginePropellerPair pair)
    {
        float finalForce = force * forceMultiplier;
        currentThrust = finalForce = Mathf.Clamp(finalForce, forceClamp.x, forceClamp.y);
        
        Vector3 direction = pair.EngineJoint.transform.forward; 
        Vector3 position = pair.PropellerJoint.transform.position;
        
        parentRigidbody.AddForceAtPosition(direction * finalForce, position);
        
        // Draw force vector
        if (debug)
        {
            Debug.DrawRay(position, direction * finalForce / forceMultiplier, Color.yellow);
        }
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
        currentAngle = Mathf.Clamp(newRotation, -engineRotationLimit, engineRotationLimit);
        Quaternion targetRotation = Quaternion.Euler(0, currentAngle, 0);
        return (targetRotation, joint, desiredRotation);
    }
    
    
    /// Rotate the propeller based on the current thrust and time
    private void RotatePropeller(Transform joint)
    {
        float rotationThisFrame = currentThrust * rotationMultiplier * Time.deltaTime;
        rotationThisFrame = Mathf.Clamp(rotationThisFrame, -1000f, 5000f);
        
        Quaternion currentRotation = joint.localRotation;
        Quaternion newRotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y, currentRotation.eulerAngles.z + rotationThisFrame);
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
        while (angle < 0.0f) angle += 360.0f;
        while (angle >= 360.0f) angle -= 360.0f;
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
        if (inputActions != null) inputActions.Enable();
    }
    
    
    private void OnDisable()
    {
        if (inputActions != null) inputActions.Disable();
    }
    
    
    private void OnDrawGizmos()
    {
        if (!debug) return;
        if (enginePropellerPairs == null) return;

        foreach (EnginePropellerPair pair in enginePropellerPairs)
        {
            Transform engineJoint = pair.EngineJoint.transform;

            // Draw semicircle
            for (int i = 0; i <= 2*engineRotationLimit; i++)
            {
                Vector3 rotatedStartDirection = Quaternion.Euler(0, -89, 0) *transform.rotation * startDirection; // Apply parent rotation to startDirection
                Vector3 lineStart = engineJoint.position + Quaternion.Euler(0, i -engineRotationLimit, 0) * rotatedStartDirection;
                Vector3 lineEnd = engineJoint.position + Quaternion.Euler(0, i -engineRotationLimit-1, 0) * rotatedStartDirection;
                Color lineColor = i < engineRotationLimit ? Color.red : Color.green; // Change color based on angle
                //Debug.DrawLine(lineStart, lineEnd, lineColor);
            }

            float currentAngle = engineJoint.localRotation.y;
            
            // Draw line for current rotation
            Vector3 currentDirection = Quaternion.Euler(0, currentAngle, 0) * -engineJoint.forward;
            //Debug.DrawLine(engineJoint.position, engineJoint.position + currentDirection, Color.magenta);
        }
    }
}
