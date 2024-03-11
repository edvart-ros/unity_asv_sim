using UnityEngine;

public enum sensorEnum{
    RGB,
    Depth
}

public class ProcessRenderTexture : MonoBehaviour
{
    public sensorEnum sensorType;
    public RenderTexture inputRenderTexture;
    public RenderTexture normalsRenderTexture;
    public RenderTexture outputRenderTexture;
    private Camera cam;

    [Range(0.0f, 1.0f)]
    public float flatNoise = 0.0f;
    [Range(0.0f, 1.0f)]
    public float depthAngleNoiseGain = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float K1 = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float K2 = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float K3 = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float T1 = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float T2 = 0.0f;
    private Material mat;
    private Vector3[] frustumCorners = new Vector3[4];
    private Vector3[] normCorners = new Vector3[4];

    void Start(){
        if (sensorType == sensorEnum.Depth){
            mat = new Material(Shader.Find("Unlit/NoiseDistortDepth"));
            cam = GetComponent<Camera>();
        }
        else if (sensorType == sensorEnum.RGB){
            mat = new Material(Shader.Find("Unlit/NoiseDistortRGB"));
        }
    }

    void Update()
    {
        if (sensorType == sensorEnum.Depth){
            mat.SetTexture("_NormalsTex", normalsRenderTexture);
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
            for (int i = 0; i < 4; i++)
            {
                normCorners[i] = cam.transform.TransformVector(frustumCorners[i]);
                normCorners[i] = normCorners[i].normalized;
                Debug.DrawRay(cam.transform.position, normCorners[i], Color.red);
            }
            mat.SetVector("_BL", normCorners[0]);
            mat.SetVector("_TL", normCorners[1]);
            mat.SetVector("_TR", normCorners[2]);
            mat.SetVector("_BR", normCorners[3]);
            mat.SetFloat("_depth_angle_noise_gain", depthAngleNoiseGain);
        }
        mat.SetTexture("_MainTex", inputRenderTexture);
        mat.SetFloat("_K1", K1);
        mat.SetFloat("_K2", K2);
        mat.SetFloat("_K3", K3);
        mat.SetFloat("_T1", T1);
        mat.SetFloat("_T2", T2);
        mat.SetFloat("_flat_noise", flatNoise);
        Graphics.Blit(inputRenderTexture, outputRenderTexture, mat);
    }
}