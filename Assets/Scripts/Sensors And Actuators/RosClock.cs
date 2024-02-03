using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Rosgraph;
using Unity.Robotics.Core;
     
public class ClockPublisher : MonoBehaviour
{   
    [SerializeField] private string _topicName = "clock";
     
    private ROSConnection _ros;
    private ClockMsg _message;
    public double sec, nanosec;    
    double m_PublishRateHz = 200f;
    double m_LastPublishTimeSeconds;
    double PublishPeriodSeconds => 1.0f / m_PublishRateHz;
    bool ShouldPublishMessage => Clock.FrameStartTimeInSeconds - PublishPeriodSeconds > m_LastPublishTimeSeconds;
     
    void Start()
    {
        // setup ROS
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<ClockMsg>(_topicName);
        _message = new ClockMsg();
        _message.clock.sec = 0;
        _message.clock.nanosec = 0;
    }
     
     
       
    void PublishMessage()
    {
        var publishTime = Clock.Now;
        sec = publishTime;
        nanosec = ((publishTime - Math.Floor(publishTime)) * Clock.k_NanoSecondsInSeconds);
        _message.clock.sec = (int)sec;
        _message.clock.nanosec = (uint)nanosec;
        m_LastPublishTimeSeconds = publishTime;
        _ros.Publish(_topicName, _message);
    }
     
    private void FixedUpdate()
    {
        if (ShouldPublishMessage)
        {
            PublishMessage();
        }
    }
     
}
