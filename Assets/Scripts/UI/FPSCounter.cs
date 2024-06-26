using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class showFPS : MonoBehaviour
{
    
    /*
    public TextMeshProUGUI fpsText;
    private float deltaTime;

    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        
        //print("FPS is:" + fps);
    }
    */
    
    public TextMeshProUGUI fpsText;
    private float deltaTime = 0.0f;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        fpsText.text = "FPS: " + Mathf.Ceil(fps).ToString();
    }
}
