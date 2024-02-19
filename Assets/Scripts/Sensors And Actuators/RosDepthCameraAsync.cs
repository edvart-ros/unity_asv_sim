using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

public class RosDepthCameraAsync : MonoBehaviour
{
    public  RenderTexture depthRenderTexture;
    public string topicName = "camera/depth/image";
    public string frameId = "camera_link";
    public bool publish = true;
    [Range(1.0f, 60.0f)]
    public float Hz;
    private ROSConnection ros;
    private float timeSincePublish;
    private Texture2D depthTex2D;
    private HeaderMsg headerMsg = new HeaderMsg();
    private ImageMsg msg;

    void Start()
    {
        timeSincePublish = 0.0f;
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImageMsg>(topicName);
        headerMsg.frame_id = frameId;
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (timeSincePublish > 1.0f / Hz)
        {
            RequestReadback(depthRenderTexture);
            timeSincePublish = 0.0f;
        }
    }

    void RequestReadback(RenderTexture targetTexture)
    {
        AsyncGPUReadback.Request(targetTexture, 0, TextureFormat.RFloat, OnReadbackComplete);
    }

    void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("Error on GPU readback, depth");
            return;
        }

        if (depthTex2D == null || depthTex2D.width != depthRenderTexture.width || depthTex2D.height != depthRenderTexture.height)
        {
            depthTex2D = new Texture2D(depthRenderTexture.width, depthRenderTexture.height, TextureFormat.RFloat, false);
        }

        depthTex2D.LoadRawTextureData(request.GetData<byte>());
        depthTex2D.Apply();
        if (!publish) return; 
        SendImage();
    }

    void SendImage()
    {
        var publishTime = Clock.Now;
        var sec = publishTime;
        var nanosec = (publishTime - Math.Floor(publishTime)) * Clock.k_NanoSecondsInSeconds;
        headerMsg.stamp.sec = (int)sec;
        headerMsg.stamp.nanosec = (uint)nanosec;
        

        msg = depthTex2D.ToImageMsg(headerMsg);
        msg.encoding = "32FC1";
        ros.Publish(topicName, msg);
    }
}
