using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

// TODO: Improve rudder behavior
// TODO: Spin propeller based on force
// TODO: Add a swirly water effect behind the propeller + white lines rotating around the propeller

public class ShipController : MonoBehaviour
{
    public float forceMultiplier = 100f;
    public float rotationMultiplier = 10f;
    public float propellerRotationMinimizer = 0.01f;
    public bool hasRudder = true;
    public bool holdingRudder = false;
    
    public TextMeshProUGUI rotationText;
    public TextMeshProUGUI forceText;
    public TextMeshProUGUI currentAngleText;
    public TextMeshProUGUI currentForceText;
    public TextMeshProUGUI currentSpinText;
    
    private InputActions inputActions;
    private Vector2 rotateValue;
    [SerializeField] private float returnSpeed = 10f;
    private Rigidbody rb;
    private GameObject propulsionRoot;
    
    // Global placeholders
    private float currentAngle  = 0.0f;
    private float currentThrust = 0.0f;
    
    // Propeller setup
    //private GameObject engineJoint;
    //private GameObject propellerJoint;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    

    
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
    
    // List to hold all engine-propeller pairs
    private List<EnginePropellerPair> enginePropellerPairs = new List<EnginePropellerPair>();

    
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
        // Save the initial position and rotation
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        propulsionRoot = GameObject.Find("Propulsion");
        rb = GetComponent<Rigidbody>();
        SearchAndAssignChild(propulsionRoot);
        print(enginePropellerPairs.Count);
        foreach (EnginePropellerPair pair in enginePropellerPairs)
        {
            print(pair.EngineJoint.name);
            print(pair.PropellerJoint.name);
        }
        //print("current gamepad:" + Gamepad.current);
    }

    
    private void FixedUpdate()
    {
        //resetButton = inputActions.Debug.ResetPosition;
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();
        float netForce = rightTriggerValue - leftTriggerValue; // Net force is in the range of -1 to 1.
        float leftStickValue = inputActions.Ship.Rudder.ReadValue<Vector2>().x;
        
        if (propulsionRoot) WorkOnJoints(leftStickValue, netForce);
        
        forceText.text = "Force: " + netForce.ToString("F2");
        rotationText.text = "Rotation: " + leftStickValue.ToString("F2");
        currentAngleText.text = "Current Angle: " + currentAngle;
    }
    
    
    private void WorkOnJoints(float leftStickValue, float netForce)
    {
        // Iterate over each pair in the list
        foreach (EnginePropellerPair pair in enginePropellerPairs)
        {
            Transform engineJoint = pair.EngineJoint.transform;
            Transform propellerJoint = pair.PropellerJoint.transform;
            
            if (engineJoint.name == "EngineJoint")
            {
                RotateRudder(GetDesiredRotation(leftStickValue, engineJoint));
                ApplyForce(netForce, currentAngle, propellerJoint.position);
                //RotatePropeller(propellerJoint);
            }
            else if (engineJoint.name == "EngineJointBow")
            {
                RotateRudder(GetDesiredRotation(leftStickValue, engineJoint));
                //RotateRudder(leftStickValue, engineJoint);
                ApplyForce(netForce, -currentAngle , propellerJoint.position);
            }
        }
    }
    
    
    private void SearchAndAssignChild(GameObject firstTargetChild)
    {
        // Find all engine joints as children of the propulsion object and populate the list
        foreach (Transform engineJoint in firstTargetChild.transform)
        {
            Transform propellerJoint = engineJoint.Find("PropellerJoint");
            if (propellerJoint != null)
            {
                // Add the pair to the list
                enginePropellerPairs.Add(new EnginePropellerPair(engineJoint, propellerJoint));
            }
        }
    }
    
    
    private void ApplyForce(float force, float angle, Vector3 position)
    {
        float finalForce = force * forceMultiplier;
        finalForce = Mathf.Clamp(finalForce, -500f, 2750f);
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        Vector3 direction = rotation * transform.forward;
        currentThrust = finalForce;
        currentForceText.text = "Current Force: " + finalForce.ToString("F2");
        
        rb.AddForceAtPosition(direction * finalForce, position);
    }
    
    
    private void RotateRudder((Quaternion targetRotation, Transform joint, float desiredRotation) data)
    {
        if (holdingRudder)
            data.joint.localRotation = data.targetRotation;
        else if (data.desiredRotation < -0.001 || data.desiredRotation > 0.001)
            data.joint.localRotation = Quaternion.Lerp(data.joint.localRotation, data.targetRotation, returnSpeed); //Time.deltaTime *
        else
            ResetRotation(data.joint.transform);
    }
    
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
        float desiredRotation =  Mathf.Abs(currentThrust * rotationMultiplier);
        desiredRotation *= propellerRotationMinimizer;
        // Get the current rotation
        Quaternion currentRotation = joint.localRotation;
        
        // Calculate the desired rotation
        Quaternion desiredQuaternion = Quaternion.Euler(desiredRotation, currentRotation.eulerAngles.y, currentRotation.eulerAngles.z);

        // Interpolate between the current rotation and the desired rotation
        Quaternion smoothRotation = Quaternion.Lerp(currentRotation, desiredQuaternion, Time.deltaTime * rotationMultiplier);
        
        // Apply the new rotation to the joint
        joint.localRotation = smoothRotation;
        currentSpinText.text = "Current Spin: " + desiredRotation.ToString("F2");
    }
    
    
    private void ResetRotation(Transform joint)
    {
        float currentYRotation = NormalizeAngle(joint.localEulerAngles.y);
        //if (currentYRotation > 180) currentYRotation -= 360; // Normalize to -180 to 180
        float newAngle = Mathf.MoveTowardsAngle(currentYRotation, 0, returnSpeed);//Time.deltaTime * 
        joint.localRotation = Quaternion.Euler(0, newAngle, 0);
    }
    
    
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


    private void ResetPosition() // Callback function for the reset button
    {
        float positionThreshold = 2.0f; 
        float rotationThreshold = 10.0f; 
        float positionDifference = Vector3.Distance(transform.position, initialPosition);
        float rotationDifference = Quaternion.Angle(transform.rotation, initialRotation);
        if (positionDifference > positionThreshold || rotationDifference > rotationThreshold)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.ResetInertiaTensor(); // Optional, use if we need to reset rotational velocities due to inertia changes
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
