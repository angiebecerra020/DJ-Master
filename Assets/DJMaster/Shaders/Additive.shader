// Additive glow for lasers, light beams, stars and fireworks. Stereo-instancing safe for Quest.
// _Shape: 0 = flat, 1 = round sprite (particles, stars), 2 = beam (fades along V), 3 = laser (soft across U)
Shader "DJMaster/Additive"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Shape ("Shape", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Shape;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float k = 1;
                float2 c = i.uv * 2 - 1;
                if (_Shape > 0.5 && _Shape < 1.5) k = saturate(1 - dot(c, c));
                else if (_Shape > 1.5 && _Shape < 2.5) k = 1 - i.uv.y;
                else if (_Shape > 2.5) k = saturate(1 - abs(c.x)) * saturate(1.2 - i.uv.y);
                fixed4 col = _Color * i.color;
                return fixed4(col.rgb * col.a * k, 1);
            }
            ENDCG
        }
    }
}
