// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/NoiseDistortRGB"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            static const float PI = 3.14;
            float _K1;
            float _K2;
            float _K3;
            float _T1;
            float _T2;
            float _noise_intensity;
            float4 _OutOfBoundColour = float4(0.0, 0.0, 0.0, 1.0);
        
            float Rand(float2 co) {
                float time = _Time.y;
                return frac(sin(dot(co.xy, float2(12.9898 + time, 78.233 + time))) * 43758.5453);
            }
        
            float2 GaussianRandom(float2 uv) {
                float u1 = Rand(uv);
                float u2 = Rand(uv + 1.0);
        
                float z0 = sqrt(-2.0 * log(u1)) * sin(2.0 * PI * u2);
                float z1 = sqrt(-2.0 * log(u1)) * cos(2.0 * PI * u2);
        
                return float2(z0, z1);
            }
        
            float2 getUndistorted(float2 p, float3 K, float2 P){
                const float2 c = float2(0.5, 0.5);
                const float x_d = p.x; const float y_d = p.y;
                const float x_c = c.y; const float y_c = c.y;
                const float r = sqrt(pow(p.x-c.x, 2) + pow(p.y-c.y, 2));
        
                const float x_u = x_d + (x_d-x_c)*(K[0]*r*r + K[1]*pow(r, 4) + K[2]*pow(r, 6)) + (P[0]*(r*r + 2*pow(x_d-x_c, 2)) 
                                      + 2*P[1]*(x_d-x_c)*(y_d-y_c));
        
                const float y_u = y_d + (y_d-y_c)*(K[0]*r*r + K[1]*pow(r, 4) + K[2]*pow(r, 6)) + (2*P[0]*(x_d-x_c)*(y_d-y_c)
                                      + P[1]*(r*r+2*pow(y_d-y_c, 2)));
        
                return float2(x_u, y_u);
            }
        
        
            bool outOfBounds(float2 uv){
                return (uv.x <= 0 || uv.x >= 1 || uv.y <= 0 || uv.y >= 1);
            }
        
            float4 AddNoise(float4 color, float2 uv){
                float2 sample = GaussianRandom(uv);
                float noise = clamp(sample.x, -1.0, 1.0)*_noise_intensity;
                return color*(1-noise);
            }


            /////////////////////
            /////////////////////
            //// Main shader ////
            /////////////////////
            /////////////////////
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1; // Add a field for the view direction
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

             
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = worldPosition - _WorldSpaceCameraPos;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //apply distortion
                const float2 xy_d = float2(i.uv[0], i.uv[1]);
                const float2 xy_u = getUndistorted(xy_d, float3(_K1, _K2, _K3), float2(_T1, _T2));
                if (outOfBounds(xy_u)) return 0.0;
                
                
                float4 color = tex2D(_MainTex, xy_u);
                color = AddNoise(color, i.uv);
                //return float4(i.viewDir.xyz, 1);
                return color;
            }
            ENDCG
        }
    }
}
