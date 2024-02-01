using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleDragDynamics : MonoBehaviour
{
    private Rigidbody rb;
    public float Xu, Xuu;
    public float Yv, Yvv; //, Yvvv, Yr;
    public float Zw, Zww;
    public float Kp, Kpp;
    public float Mq, Mqq;
    public float Nr, Nrr, Nrrr, Nv;
    private float u, v, w, p, q, r;

    private Vector3 vel;
    private Vector3 angVel;
    private float dt;
    private float m;

    
    private Vector3 localVel;
    private float Yr;
    private float Xuuu;
    private float Yvvv;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        dt = Time.fixedDeltaTime;
        m = rb.mass;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        vel = rb.velocity;
        angVel = rb.angularVelocity;
        localVel = transform.InverseTransformDirection(vel);

        u = localVel.z;
        v = -localVel.x;
        w = localVel.y;
        p = -angVel.z;
        q = angVel.x;
        r = -angVel.y;

        ApplyDampingForce();
    }

    void ApplyDampingForce()
    {
        float FwMax = Mathf.Abs(m * w);

        float Fu = -(Xu * u + Xuu * Math.Abs(u) * u + Xuuu * u * u * u);
        float Fv = -(Yv * v + Yr * r + Yvv * Math.Abs(v) * v + Yvvv * v * v * v);
        float Fw = -(Zw * w + Zww * Math.Abs(w) * w);
        Fw = Mathf.Clamp(Fw, -FwMax, FwMax);

        float Tp = -(Kp * p + Kpp * Math.Abs(p) * p);
        float Tq = -(Mq * q + Mqq * Math.Abs(q) * q);
        float Tr = -(Nv * r + Nr * r + Nrr * Math.Abs(r) * r + Nrrr * r * r * r);
        Vector3 F = transform.TransformDirection(new Vector3(-Fv, Fw, Fu));
        rb.AddForce(F);
        Debug.DrawRay(transform.position, F);
        rb.AddTorque(new Vector3(Tq, -Tr, -Tp));
    }
}
