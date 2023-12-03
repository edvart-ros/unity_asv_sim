using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Nav;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class Odom : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "odometry";
    public Rigidbody rigidBody;
    public float Hz = 50.0f;
    private OdometryMsg msg;
    private float timeSincePublish;


    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(topicName);
        timeSincePublish = 0.0f;
    }

    void Update()
    {
        timeSincePublish += Time.deltaTime;
        if (timeSincePublish < 1.0f/Hz){
            return;
        }
        Vector3 pos = transform.position;
        msg = new OdometryMsg();
        msg.header.frame_id = "odom";
        msg.child_frame_id = "base_link";
        msg.pose.pose.position = new PointMsg(){
            x = pos.z, 
            y = -pos.x, 
            z = pos.y
        };
        msg.pose.pose.orientation = rigidBody.transform.rotation.To<FLU>();
        ros.Publish(topicName, msg);
        timeSincePublish = 0.0f;
    }
}
