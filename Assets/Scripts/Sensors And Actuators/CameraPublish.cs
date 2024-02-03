using System;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.Serialization;
using RosMessageTypes.Std;
using RosMessageTypes.Sensor;
using RosMessageTypes.BuiltinInterfaces;
using Unity.Robotics.Core;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using UnityEngine.Rendering;
/// <summary>
///
/// </summary>
public class RosImagePublisher : MonoBehaviour
{
    ROSConnection ros;
    public string ImagetopicName = "unity_camera/rgb/image_raw/compressed";
    public string cameraInfoTopicName = "unity_camera/rgb/camera_info";
    // The game object
    public Camera ImageCamera;
    public string FrameId = "unity_camera/rgb_frame";
    public int resolutionWidth = 640;
    public int resolutionHeight = 480;
    [Range(0, 100)]
    public int qualityLevel = 50;
    private Texture2D texture2D;
    private Rect rect;
    // Publish the cube's position and rotation every N seconds
    public float publishMessageFrequency = 0.005f;

    // Used to determine how much time has elapsed since the last message was published
    private float timeElapsed;
    private uint seq_num = 0;
    private CompressedImageMsg message;

    void Start()
    {
        // start the ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CompressedImageMsg>(ImagetopicName);
        ros.RegisterPublisher<CameraInfoMsg>(cameraInfoTopicName);

        // Initialize game Object
        texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        rect = new Rect(0, 0, resolutionWidth, resolutionHeight);
        ImageCamera.targetTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24);

        RenderPipelineManager.endCameraRendering += RenderPipelineManager_endCameraRendering;
    }
    private void UpdateImage(Camera _camera)
    {
        if (texture2D != null && _camera == this.ImageCamera)
            UpdateMessage();
    }
    private void RenderPipelineManager_endCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        OnPostRender();
    }
    private void OnPostRender()
    {
        UpdateImage(this.ImageCamera);
    }

    private void UpdateMessage()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > publishMessageFrequency)
        {
            texture2D.ReadPixels(rect, 0, 0);
            // var timestamp = new TimeStamp(Clock.time);
            // Message
            CompressedImageMsg message = new CompressedImageMsg
            {
                format = "jpeg",
                data = texture2D.EncodeToJPG(qualityLevel)
            };
            seq_num += 1;

            // Finally send the message to server_endpoint.py running in ROS
            ros.Publish(ImagetopicName, message);

            // Camera Info message
            CameraInfoMsg cameraInfoMessage = CameraInfoGenerator.ConstructCameraInfoMessage(ImageCamera, message.header, 0.0f, 0.01f);
            ros.Publish(cameraInfoTopicName, cameraInfoMessage);

            timeElapsed = 0;
            Debug.Log("published");
        }
    }
}