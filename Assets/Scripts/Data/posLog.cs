using System;
using System.IO;
using UnityEngine;

public class PositionLogger : MonoBehaviour
{
    // Path to save the CSV file. Adjust the path as necessary.
    private string filePath;
    
    // Time interval between position logs.
    public float logInterval = 1.0f;
    private float timer;

    void Start()
    {
        filePath = Application.persistentDataPath + "/PositionLog.csv";
        // Start with a fresh file each time
        File.WriteAllText(filePath, "Time,X,Y,Z\n");
        timer = logInterval;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            LogPosition();
            timer = logInterval;
        }
    }

    void LogPosition()
    {
        // Get the current position.
        Vector3 position = transform.position;

        // Create a string to represent the current time and position.
        string logLine = string.Format("{0},{1},{2},{3}\n", DateTime.Now.ToString("o"), position.x, position.y, position.z);

        // Append the log line to the file.
        File.AppendAllText(filePath, logLine);
    }
}
