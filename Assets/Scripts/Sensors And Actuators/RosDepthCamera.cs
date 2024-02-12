using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using System;

public class RosDepthCamera : MonoBehaviour
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
    public RenderTexture depthTexture;

    void Start()
    {
        timeSincePublish = 0.0f;
        sensorCamera = gameObject.GetComponent<Camera>();
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CompressedImageMsg>(topicName);
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (!publish) return;
        if (timeSincePublish > 1.0f / Hz)
        {
            SendImageCompressed();
            //SendImage();
            timeSincePublish = 0.0f;
        }
    }

    void SendImageCompressed()
    {
        var oldRT = RenderTexture.active;
        RenderTexture.active = depthTexture;
        sensorCamera.Render();

        // Create or reuse Texture2D in RGB format
        if (camText == null)
        {
            camText = new Texture2D(depthTexture.width, depthTexture.height, TextureFormat.RGB24, false);
        }
        camText.ReadPixels(new Rect(0, 0, depthTexture.width, depthTexture.height), 0, 0);
        camText.Apply();
        RenderTexture.active = oldRT;
        byte[] imageBytes = camText.EncodeToJPG(50);
        var message = new CompressedImageMsg(new HeaderMsg() { frame_id = frameId }, "jpeg", imageBytes);
        var publishTime = Clock.Now;
        var sec = publishTime;
        var nanosec = ((publishTime - Math.Floor(publishTime)) * Clock.k_NanoSecondsInSeconds);
        message.header.stamp.sec = (int)sec;
        message.header.stamp.nanosec = (uint)nanosec;
        ros.Publish(topicName, message);
    }
}
