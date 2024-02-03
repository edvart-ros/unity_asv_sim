using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;


/// <summary>
///
/// </summary>
public class RosPublisherExample : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "from_unity_test";
    public float publishMessageFrequency = 0.5f;
    public string msgContent = "Hello";
    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(topicName);
    }

    private void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > publishMessageFrequency)
        {
            StringMsg cubePos = new StringMsg(
                msgContent
            );

            // Finally send the message to server_endpoint.py running in ROS
            ros.Publish(topicName, cubePos);
            Debug.Log("published");
            timeElapsed = 0;
        }
    }
}