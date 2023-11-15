using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class Imu : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "imu/raw";
    private Rigidbody imuBody;
    [Range(0.1f, 100.0f)]
    public float Hz = 50.0f;
    private ImuMsg Msg;
    private float timeSincePublish;


    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImuMsg>(topicName);
        timeSincePublish = 0.0f;
        imuBody = gameObject.GetComponent<Rigidbody>();
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (timeSincePublish < 1.0f/Hz){
            return;
        }

        Msg = new ImuMsg()
        {
            orientation = imuBody.transform.rotation.To<FLU>(),
            angular_velocity = imuBody.angularVelocity.To<FLU>()
        };
        ros.Publish(topicName, Msg);
        timeSincePublish = 0.0f;
    }
}
