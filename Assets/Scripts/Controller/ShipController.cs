using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShipController : MonoBehaviour
{
    
    public float forceMultiplier = 100f;
    public float rotationMultiplier = 10f;
    
    public TextMeshProUGUI rotationText;
    public TextMeshProUGUI forceText;
    
    private InputActions inputActions;
    private Vector2 rotateValue;
    private Rigidbody rb;
    
    private void Awake()
    {
        inputActions = new InputActions();

        inputActions.Ship.Rudder.performed += ctx => rotateValue = ctx.ReadValue<Vector2>();
        inputActions.Ship.Rudder.canceled += ctx => rotateValue = Vector2.zero;

    }
    
    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        float rightTriggerValue = inputActions.Ship.PositivePropulsion.ReadValue<float>();
        float leftTriggerValue = inputActions.Ship.NegativePropulsion.ReadValue<float>();

        // Calculate net force. This will be in the range of -1 to 1.
        float netForce = rightTriggerValue - leftTriggerValue;
        
        ApplyForce(netForce);
        
        // Read the horizontal value from the left stick
        float horizontalInput = inputActions.Ship.Rudder.ReadValue<Vector2>().x;

        ApplyRotation(horizontalInput);


        // Update TextMeshPro objects
        forceText.text = "Force: " + netForce.ToString("F2");
        // Update TextMeshPro objects
        rotationText.text = "Rotation: " + horizontalInput.ToString("F2");
    }
    
    void ApplyForce(float force)
    {
        float finalForce = force * forceMultiplier;
        rb.AddForce(transform.forward * finalForce);
        
        
        
    }
    
    void ApplyRotation(float rotationValue)
    {
        float desiredRotation = rotationValue * rotationMultiplier;
        
        desiredRotation = Mathf.Clamp(desiredRotation, -45f, 45f);
        Vector3 torque = new Vector3(0, desiredRotation, 0);
        Quaternion rotation = Quaternion.Euler(0, desiredRotation * Time.deltaTime, 0);
        rb.AddTorque(torque);
        //rb.MoveRotation(rb.rotation * rotation);
        
        
        
    }
}
