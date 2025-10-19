Shader "Hidden/AMS/UISoftMaskBlit"
{
    Properties
    {
		[PerRendererData]
        _MainTex ("_MainTex", 2D) = "white" { }
		[PerRendererData]
        _FallOff ("_FallOff", Float) = 1
		[PerRendererData]
        _Opacity ("_Opacity", Float) = 1
		[PerRendererData]
        _AtlasData ("_AtlasData", Vector) = (1, 1, 0.14, 0)
		[PerRendererData]
        _SliceScale ("_SliceScale", Vector) = (1, 1, 0, 0)
		[PerRendererData]
        _SliceBorder ("_SliceBorder", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Blend Off
        AlphaToMask Off
        Cull Back
        ColorMask RGBA
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Unlit"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 2.0

            #include "UnityCG.cginc"

            #pragma shader_feature_local_fragment _SLICED

            struct appdata
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionOS : SV_POSITION;
                float4 uv : TEXCOORD1;
            };

            uniform sampler2D _MainTex;
            uniform float _FallOff;
            uniform float _Opacity;

            uniform sampler2D _ParentMask;
            float4x4 _ParentMaskMatrix;

            uniform float4 _SliceScale; // xy: texSize | z: scale
            uniform float4 _SliceBorder; // x: left | y: top | z: right | w:bottom
            uniform float4 _AtlasData; // xy: scale | wz: position

            // float2 uv, float2 scale, float4 borders;
            float2 Uv9slice(float2 uv, float2 scale, float4 borders)
            {
                float2 t = saturate((scale * uv - borders.xy) / (scale - borders.xy - borders.zw));
                return lerp(uv * scale, 1 - scale * (1 - uv), t);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv0;
                o.positionOS = UnityObjectToClipPos(v.positionOS);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv.xy;

                float2 parentMaskUV = uv;
                half2 center = .5;
                parentMaskUV = mul(_ParentMaskMatrix, float4(parentMaskUV - center, 0, 1)).xy + center;
                half2 trimUV = saturate((1 - max(parentMaskUV, 1 - parentMaskUV)) * 1000);
                float parentMask = tex2D(_ParentMask, parentMaskUV).r * trimUV.x * trimUV.y;

                #ifdef _SLICED
					uv = Uv9slice(uv, _SliceScale, _SliceBorder);
                #endif

                float mask = tex2D(_MainTex, (uv * _AtlasData.xy) + _AtlasData.zw).a;
                mask = smoothstep(0, _FallOff, mask) * _Opacity;

                float finalMask = mask * parentMask;

                return half4(finalMask, mask, 0, finalMask);
            }
            ENDCG
        }
    }
    Fallback Off
}