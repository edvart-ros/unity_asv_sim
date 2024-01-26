using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class FossenDynamics : MonoBehaviour
{
    public bool Damping = true;
    public bool AddedMass = true;
    public bool Coriolis = true;
    
    public float XdotU = 5.0f;
    public float YdotV = 5.0f;
    public float ZdotW = 0.1f;
    public float KdotP = 0.0f;
    public float MdotQ = 0.0f;
    public float NdotR = 0.1f;

    public float Xu = 100.0f;
    public float Xuu = 150.0f;
    public float Yv = 100.0f;
    public float Yvv = 100.0f;
    public float Zw = 500.0f;
    public float Zww = 0.0f;
    public float Kp = 300.0f;
    public float Kpp = 600.0f;
    public float Mq = 900.0f;
    public float Mqq = 900.0f;
    public float Nr = 800.0f;
    public float Nrr = 800.0f;
    private const int nStates = 6;
    private float[,] Cor = new float[nStates, nStates];
    private float[,] Ma = new float[nStates,nStates];
    private float[,] D = new float[nStates,nStates];
    private float[] state = new float[nStates];
    private float[] statePrev = new float[nStates];
    private float[] stateDot = new float[nStates];

    private Rigidbody rb;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Ma[0, 0] = XdotU;
        Ma[1, 1] = YdotV;
        Ma[2, 2] = ZdotW;
        Ma[3, 3] = KdotP;
        Ma[4, 4] = MdotQ;
        Ma[5, 5] = NdotR;
        state = getState();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        state = getState();
        stateDot = getStateDot(state, statePrev, Time.deltaTime);
        Cor = CalculateCoriolisMatrix(state);
        D  = CalculateDampingMatrix(state);

        (Vector3 Fd, Vector3 Td) = CalculateDampingForceTorque(state);
        (Vector3 Fc, Vector3 Tc) = CalculateCoriolisForceTorque(Cor, state);
        (Vector3 Fma, Vector3 Tma) = CalculateAddedMassForceTorque(Ma, stateDot);
        
        float DampingFactor = Damping ? 1.0f : 0.0f;
        float CoriolisFactor = Coriolis ? 1.0f : 0.0f;
        float AddedMassFactor = AddedMass ? 1.0f : 0.0f;

        Vector3 F = DampingFactor*Fd + CoriolisFactor*Fc + AddedMassFactor*Fma;
        Vector3 T = DampingFactor*Td + CoriolisFactor*Tc + AddedMassFactor*Tma;

        Debug.Log((Fd, Fc, Fma, Td, Tc, Tma));
        // Convert from right-handed, z-up to left-handed, y-up (Unity's coordinate system)
        F = new Vector3(-F.y, F.z, F.x); // Switch y and z, negate new y
        T = new Vector3(T.y, -T.z, -T.x); // Switch y and z, negate new z

        rb.AddRelativeForce(F);
        rb.AddTorque(T);
        Array.Copy(state, statePrev, nStates);
    }

    private float[] getState(){
        float[] eta = state;
        Vector3 worldVelocity = rb.velocity;
        Vector3<FLU> localVelocity = transform.InverseTransformDirection(worldVelocity).To<FLU>();
        Vector3<FLU> localAngularVelocity = -rb.angularVelocity.To<FLU>();

        eta[0] = localVelocity.x; //forward velocity
        eta[1] = localVelocity.y;
        eta[2] = localVelocity.z;
        
        eta[3] = localAngularVelocity.x;
        eta[4] = localAngularVelocity.y;
        eta[5] = localAngularVelocity.z;
        return eta;
    }

    private float[] getStateDot(float[] currentState, float[] previousState, float dt)
    {
        float[] retStateDot = new float[6];
        for (var i = 0; i < nStates; i++)
        {
            retStateDot[i] = (currentState[i] - previousState[i]) / dt;
        }
        return retStateDot;
    }
    private float[,] CalculateCoriolisMatrix(float[] eta){
        float[,] C = new float[6, 6];
        C[0, 5] = YdotV*eta[1];
        C[1, 5] = XdotU*eta[0];
        C[5, 0] = YdotV*eta[1];
        C[5, 1] = YdotV*eta[0];
        return C;
    }

    private float[,] CalculateDampingMatrix(float[] eta){
        D[0, 0] = Xu + Xuu*Mathf.Abs(eta[0]);
        D[1, 1] = Yv + Yvv*Mathf.Abs(eta[1]);
        D[2, 2] = Zw + Zww*Mathf.Abs(eta[2]);
        D[3, 3] = Kp + Kpp*Mathf.Abs(eta[3]);
        D[4, 4] = Mq + Mqq*Mathf.Abs(eta[4]);
        D[5, 5] = Nr + Nrr*Mathf.Abs(eta[5]);
        return D;
    }
    private (Vector3, Vector3) CalculateDampingForceTorque(float[] eta)
    {
        Vector3 Fd = new Vector3(); // Damping force
        Vector3 Td = new Vector3(); // Damping torque

        // Calculating linear damping force
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Fd[i] += D[i, j] * eta[j];
            }
        }

        // Calculating angular damping torque
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Td[i] += D[i + 3, j + 3] * eta[j + 3];
            }
        }

        return (-Fd, -Td);
    }


    private (Vector3, Vector3) CalculateCoriolisForceTorque(float[,] C, float[] eta)
    {
        Vector3 Fc = new Vector3();
        Vector3 Tc = new Vector3();

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                if (j < 3)
                    Fc[i] += C[i, j] * eta[j];
                else
                    Tc[i] += C[i, j] * eta[j];
            }
        }

        return (Fc, Tc);
    }
    
    private (Vector3, Vector3) CalculateAddedMassForceTorque(float[,] AddedMassMatrix, float[] etaDot)
    {
        Vector3 F = new Vector3();
        Vector3 T = new Vector3();

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                if (j < 3)
                    F[i] += AddedMassMatrix[i, j] * etaDot[j];
                else
                    T[i] += AddedMassMatrix[i, j] * etaDot[j];
            }
        }

        return (F, T);
    }



}
