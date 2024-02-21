using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

public class RosCameraSimple : MonoBehaviour
{
    public string topicName = "camera/image";
    public string frameId = "camera_link";
    public bool publish = true;
    [Range(1.0f, 60.0f)]
    public float Hz;
    private ROSConnection ros;
    private Camera sensorCamera;
    private Texture2D camText;
    private float timeSincePublish;
    private HeaderMsg headerMsg = new HeaderMsg();
    private CompressedImageMsg msg;
    private Rect rectangle;

    void Start()
    {
        timeSincePublish = 0.0f;
        sensorCamera = gameObject.GetComponent<Camera>();
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CompressedImageMsg>(topicName);
        headerMsg.frame_id = frameId;
        // Create or reuse Texture2D in RGB format
        rectangle = new Rect(0, 0, sensorCamera.targetTexture.width, sensorCamera.targetTexture.height);
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (!publish) return;
        if (timeSincePublish > 1.0f / Hz)
        {
            SendImageCompressed();
            timeSincePublish = 0.0f;
        }
    }

    void SendImageCompressed()
    {
        var oldRT = RenderTexture.active;
        RenderTexture.active = sensorCamera.targetTexture;
        sensorCamera.Render();

        if (camText == null || camText.width != sensorCamera.targetTexture.width || camText.height != sensorCamera.targetTexture.height)
        {
            camText = new Texture2D(sensorCamera.targetTexture.width, sensorCamera.targetTexture.height, TextureFormat.RGB24, false);
        }
        camText.ReadPixels(rectangle, 0, 0);
        camText.Apply();
        RenderTexture.active = oldRT;

        var publishTime = Clock.Now;
        var sec = publishTime;
        var nanosec = (publishTime - Math.Floor(publishTime)) * Clock.k_NanoSecondsInSeconds;
        headerMsg.stamp.sec = (int)sec;
        headerMsg.stamp.nanosec = (uint)nanosec;

        msg = camText.ToCompressedImageMsg_JPG(headerMsg);
        ros.Publish(topicName, msg);
    }
}
