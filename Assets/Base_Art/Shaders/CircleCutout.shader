Shader "UI/CircleCutout"
{
    Properties                         // ▼  NEW: _MainTex to satisfy Image
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color   ("Tint",   Color) = (0,0,0,1)
        _Radius  ("Radius", Range(0,1.5)) = 1.2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;        // not strictly needed, but prevents warnings
            fixed4 _Color;
            float  _Radius;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag (v2f i) : SV_Target
            {
                float dist = distance(i.uv, float2(0.5, 0.5));
                if (dist < _Radius) discard;   // transparent hole
                return _Color;                 // black everywhere else
            }
            ENDCG
        }
    }
}
