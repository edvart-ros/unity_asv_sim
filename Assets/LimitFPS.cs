using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LimitFPS : MonoBehaviour
{
    public int targetFPS = 60;
    // Start is called before the first frame update
    void Start()
    {
        QualitySettings.vSyncCount = 2;
        Application.targetFrameRate = targetFPS;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
