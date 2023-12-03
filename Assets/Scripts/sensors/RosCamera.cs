using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

public class RosCamera : MonoBehaviour
{
    public string topicName = "wamv/camera/image";
    public string frameId = "wamv/camera_link";
    public bool publish = true;
    [Range(1.0f, 60.0f)]
    public float Hz;
    private ROSConnection ros;
    private Camera sensorCamera;
    private Texture2D camText;
    private float timeSincePublish;

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
        RenderTexture.active = sensorCamera.targetTexture;
        sensorCamera.Render();

        // Create or reuse Texture2D in RGB format
        if (camText == null || camText.width != sensorCamera.targetTexture.width || camText.height != sensorCamera.targetTexture.height)
        {
            camText = new Texture2D(sensorCamera.targetTexture.width, sensorCamera.targetTexture.height, TextureFormat.RGB24, false);
        }
        camText.ReadPixels(new Rect(0, 0, sensorCamera.targetTexture.width, sensorCamera.targetTexture.height), 0, 0);
        camText.Apply();
        RenderTexture.active = oldRT;

        byte[] imageBytes = camText.EncodeToJPG(100);
        var message = new CompressedImageMsg(new HeaderMsg() { frame_id = frameId }, "jpeg", imageBytes);
        ros.Publish(topicName, message);
    }
}
