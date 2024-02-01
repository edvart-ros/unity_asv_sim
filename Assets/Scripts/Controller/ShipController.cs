using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Animations;

// TODO: Add a toggle between set engine angle or hold engine angle


public class ShipController : MonoBehaviour
{
    public float forceMultiplier = 100f;
    public float rotationMultiplier = 10f;
    public bool hasRudder = true;
    
    public TextMeshProUGUI rotationText;
    public TextMeshProUGUI forceText;
    public TextMeshProUGUI currentAngleText;
    public TextMeshProUGUI currentForceText;
    
    private InputActions inputActions;
    private Vector2 rotateValue;
    private Rigidbody rb;
    
    private float currentAngle  = 0.0f;
    private float currentThrust = 0.0f;
    
    // Propeller setup
    private GameObject engineJoint;
    private GameObject propellerJoint;
    
    
    private void Awake()
    {
        inputActions = new InputActions();
        inputActions.Ship.Rudder.performed += ctx => rotateValue = ctx.ReadValue<Vector2>();
        inputActions.Ship.Rudder.canceled += ctx => rotateValue = Vector2.zero;
    }
    
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    
    private void FixedUpdate()
    {
        GameObject propulsionRoot = GameObject.Find("Propulsion");
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();
        float leftStickValue = inputActions.Ship.Rudder.ReadValue<Vector2>().x;
        
        // Net force is in the range of -1 to 1.
        float netForce = rightTriggerValue - leftTriggerValue;
        
        if (propulsionRoot != null)
        {
            SearchForChildAndApplyForces(propulsionRoot, leftStickValue, netForce);
        }
        
        // Update TextMeshPro objects
        forceText.text = "Force: " + netForce.ToString("F2");
        // Update TextMeshPro objects
        rotationText.text = "Rotation: " + leftStickValue.ToString("F2");
    }
    
    
    private void SearchForChildAndApplyForces(GameObject firstTargetChild, float leftStickValue, float netForce)
    {
        // Find all engine joints as children of the propulsion object
        foreach (Transform secondTargetChild in firstTargetChild.transform)
        {
            Transform propellerJoint = secondTargetChild.Find("PropellerJoint");
            // Do something with the engine joints
            if (secondTargetChild.name == "EngineJointRear" && propellerJoint) 
            {
                ApplyRotation(leftStickValue, secondTargetChild.transform);
                ApplyForce(netForce, currentAngle, propellerJoint.position);
            }
            else if (secondTargetChild.name == "EngineJointFront" && propellerJoint)
            {
                ApplyRotation(leftStickValue, secondTargetChild.transform);
                ApplyForce(netForce, -currentAngle , propellerJoint.position);
            }
        }
    }
    
    
    private void ApplyForce(float force, float angle, Vector3 position)
    {
        float finalForce = force * forceMultiplier;
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        Vector3 direction = rotation * transform.forward;
        
        currentForceText.text = "Current Force: " + finalForce.ToString("F2");
        
        rb.AddForceAtPosition(direction * finalForce, position);
        
    }
    
    
    private void ApplyRotation(float rotationValue, Transform joint)
    {
        float desiredRotation = - rotationValue * rotationMultiplier;
        
        float newRotation = joint.localEulerAngles.y + desiredRotation;
        
        newRotation = NormalizeAngle(newRotation);
        
        if (newRotation > 300) newRotation -= 360; // To avoid snapping at 0/360 degrees
        
        // Clamp the new rotation
        currentAngle = Mathf.Clamp(newRotation, -45f, 45f);
        //currentAngle = Mathf.Clamp(desiredRotation, -45f, 45f);
        
        //Vector3 torque = new Vector3(0, currentAngle, 0);
        //Quaternion positiveRotation = Quaternion.Euler(0, currentAngle, 0); // * Time.deltaTime
        //Quaternion negativeRotation = Quaternion.Euler(0, -currentAngle, 0);
        //rb.AddTorque(torque);
        // Apply the rotation around the joint
        //joint.gameObject.transform.RotateAround(joint.position, Vector3.up, currentAngle);
        
        Quaternion targetRotation = Quaternion.Euler(0, currentAngle, 0);
        joint.localRotation = targetRotation;
        currentAngleText.text = "Current Angle: " + currentAngle;
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
