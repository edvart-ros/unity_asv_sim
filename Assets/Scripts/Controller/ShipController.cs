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
        inputActions = new InputActions();
        inputActions.Ship.Rudder.performed += ctx => rotateValue = ctx.ReadValue<Vector2>();
        inputActions.Ship.Rudder.canceled += ctx => rotateValue = Vector2.zero;
    }
    
    
    private void Start()
    {
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
        
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();
        float netForce = rightTriggerValue - leftTriggerValue; // Net force is in the range of -1 to 1.
        float leftStickValue = inputActions.Ship.Rudder.ReadValue<Vector2>().x;
        
        
        

        if (propulsionRoot) WorkOnJoints(leftStickValue, netForce);
        
        // Update TextMeshPro objects
        forceText.text = "Force: " + netForce.ToString("F2");
        // Update TextMeshPro objects
        rotationText.text = "Rotation: " + leftStickValue.ToString("F2");
    }
    
    
    private void WorkOnJoints(float leftStickValue, float netForce)
    {
        // Iterate over each pair in the list
        foreach (EnginePropellerPair pair in enginePropellerPairs)
        {
            // You can access the engine joint and propeller joint like this:
            Transform engineJoint = pair.EngineJoint.transform;
            Transform propellerJoint = pair.PropellerJoint.transform;

            //print("hit pair");
            
            RotateRudder(leftStickValue, engineJoint);
            ApplyForce(netForce, currentAngle, propellerJoint.position);
            // Now apply your logic based on the engine and propeller joints
            if (engineJoint.name == "EngineJoint")
            {
                RotatePropeller(propellerJoint);
            }
            else if (engineJoint.name == "EngineJointBow")
            {
                //RotateRudder(GetDesiredRotation(leftStickValue, engineJoint));
                RotateRudder(leftStickValue, engineJoint);
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
    
    
    private void RotateRudder(float rotationValue, Transform joint)
    {
        float desiredRotation = - rotationValue * rotationMultiplier;
        float newRotation = joint.localEulerAngles.y + desiredRotation;
        newRotation = NormalizeAngle(newRotation);
        if (newRotation > 300) newRotation -= 360; // To avoid snapping at 0/360 degrees
        currentAngle = Mathf.Clamp(newRotation, -45f, 45f);
        
        Quaternion targetRotation = Quaternion.Euler(0, currentAngle, 0);
        if (holdingRudder)
        {
            joint.localRotation = targetRotation;
        }
        else if (desiredRotation < -0.001 || desiredRotation > 0.001)
        {
            //if (newRotation > 180) newRotation -= 360;
            joint.localRotation = Quaternion.Lerp(joint.localRotation, targetRotation, returnSpeed); //Time.deltaTime *
        }
        else ReturnToDefault(joint.transform);
        
        currentAngleText.text = "Current Angle: " + currentAngle;
    }
    
    private (Quaternion, Transform) GetDesiredRotation(float rotationValue, Transform joint) 
    {
        float desiredRotation = - rotationValue * rotationMultiplier;
        float newRotation = joint.localEulerAngles.y + desiredRotation;
        newRotation = NormalizeAngle(newRotation);
        if (newRotation > 300) newRotation -= 360; // To avoid snapping at 0/360 degrees
        currentAngle = Mathf.Clamp(newRotation, -45f, 45f);
        Quaternion targetRotation = Quaternion.Euler(0, currentAngle, 0);
        return (targetRotation, joint);
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
    
    
    private void ReturnToDefault(Transform joint)
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
    
    
    private void OnEnable()
    {
        inputActions.Enable();
    }
    
    
    private void OnDisable()
    {
        inputActions.Disable();
    }
    
}
