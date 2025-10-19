#pragma once

#include "UnityCG.cginc"
#include "UISoftMask.hlsl"

#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
#pragma shader_feature _ _DEBUG_MASK

half3 GammaToLinearRGB(half3 gamma)
{
    return half3(GammaToLinearSpaceExact(gamma.r),
                 GammaToLinearSpaceExact(gamma.g),
                 GammaToLinearSpaceExact(gamma.b));
}

uniform sampler2D _MainTex;
uniform float4 _MainTex_ST;
uniform sampler2D _SoftMask;

struct VertexInput
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float3 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float3 uv0 : TEXCOORD0;
    float4 mask : TEXCOORD2;
    float3 maskData : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

fixed4 _Color;
fixed4 _TextureSampleAdd;
float4 _ClipRect;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;

VertexOutput vert(VertexInput v)
{
    VertexOutput o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.positionCS = UnityObjectToClipPos(v.positionOS);

    float2 pixelSize = v.positionOS.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    o.mask = float4(v.positionOS.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

    o.uv0 = v.uv0;

    o.color = v.color * _Color;

    #if !defined(UNITY_COLORSPACE_GAMMA)
    o.color.xyz = lerp(o.color.xyz, GammaToLinearRGB(o.color.xyz), _MaskDataSettings.y);
    #endif

    o.maskData = MaskData(v.uv0,mul(UNITY_MATRIX_M, v.positionOS));

    return o;
}

half4 frag(VertexOutput IN) : SV_Target
{
    float4 color = tex2D(_MainTex, IN.uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw);

    #ifdef _DEBUG_MASK
        return DebugMask(IN.maskData, _SoftMask).r * color.a;
    #else
        color.a = UISoftMask(IN.maskData, _SoftMask, color.a);
    #endif

    color = (color + _TextureSampleAdd) * IN.color;

    #ifdef UNITY_UI_CLIP_RECT
		half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
		color.a *= m.x * m.y;
    #endif

    #ifdef UNITY_UI_ALPHACLIP
		clip(color.a - 0.001);
    #endif

    return color;
}
