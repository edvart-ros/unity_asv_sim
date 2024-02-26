using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


public class FPS_csv : MonoBehaviour {
    int m_frameCounter = 0;
    float m_timeCounter = 0.0f;
    float m_lastFramerate = 0.0f;
    public float m_refreshTime = 1f;
    public string fileName = "Fps.csv";
    public float maxLogTime = 30f;

void Update()
{
    if( m_timeCounter < m_refreshTime )
    {
        m_timeCounter += Time.deltaTime;
        m_frameCounter++;
    }
    else
    {
        //This code will break if you set your m_refreshTime to 0, which makes no sense.
        m_lastFramerate = (float)m_frameCounter/m_timeCounter;
        m_frameCounter = 0;
        m_timeCounter = 0.0f;
        if (Time.time < maxLogTime) LogFpsData(); 
    }
}


    private void LogFpsData(){
        string dataString = string.Format("{0}, {1}\n", Time.time, m_lastFramerate);
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(filePath)) {
            File.WriteAllText(filePath, "Time, fps \n");
        }
        File.AppendAllText(filePath, dataString);
    }
}