using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

public class CameraInfo : MonoBehaviour
{
    public string topicName = "camera/info";
    public string frameId = "camera_link_optical_frame";
    public bool publish = true;
    private float Hz = 5.0f;
    private ROSConnection ros;
    private Camera sensorCamera;
    private float timeSincePublish;
    private HeaderMsg headerMsg = new HeaderMsg();
    private CameraInfoMsg msg;

    void Start()
    {
        timeSincePublish = 0.0f;
        sensorCamera = gameObject.GetComponent<Camera>();
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CameraInfoMsg>(topicName);
        headerMsg.frame_id = frameId;
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (!publish) return;
        if (timeSincePublish > 1.0f / Hz)
        {
            SendInfo();
            timeSincePublish = 0.0f;
        }
    }



    void SendInfo()
    {
        var publishTime = Clock.Now;
        var sec = publishTime;
        var nanosec = (publishTime - Math.Floor(publishTime)) * Clock.k_NanoSecondsInSeconds;
        headerMsg.stamp.sec = (int)sec;
        headerMsg.stamp.nanosec = (uint)nanosec;

        msg = CameraInfoGenerator.ConstructCameraInfoMessage(sensorCamera, headerMsg, 0f, 1.0f);
        ros.Publish(topicName, msg);
    }
}
