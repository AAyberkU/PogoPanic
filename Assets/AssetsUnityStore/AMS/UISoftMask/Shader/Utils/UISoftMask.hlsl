// UISoftMask by Alexandre Soria http://sorialexandre.tech
#pragma once

int _WORLDCANVAS;
float2 _RectUvSize;
float4x4 _WorldCanvasMatrix;
float4x4 _OverlayCanvasMatrix;

float2 _MaskDataSettings; // x: enabled | y: gamma2linear

/// RectUV: float4 wPos
float2 RectUV(float4 wPos)
{
    float2 rectFactor = 1.0 / _RectUvSize;
    float2 worldCanvasUV = mul(_WorldCanvasMatrix, wPos).xy * rectFactor;
    float2 overlayCanvasUV = mul(_OverlayCanvasMatrix, wPos).xy * rectFactor;
    return lerp(overlayCanvasUV, worldCanvasUV, _WORLDCANVAS);
}

float3 MaskData(float3 maskDataUV, float3 wPos)
{
    return float3(RectUV(float4(wPos, 1)), maskDataUV.z);
}

/// UISoftMask: float3 maskData, sampler2D maskSampler, float alpha
float UISoftMask(float3 maskData, sampler2D maskSampler, float alpha)
{
    float2 maskUV = maskData.xy;
    half enabled = maskData.z;
    float2 trimUV = saturate((1 - max(maskUV, 1 - maskUV)) * 1000);
    float mask = tex2D(maskSampler, maskUV).r * trimUV.x * trimUV.y;

    float softMaskFactor = enabled * mask;
    return alpha * (softMaskFactor + (1 - enabled));
}

/// float4 rectUV, sampler2D maskSampler
float4 DebugMask(float2 rectUV, sampler2D maskSampler)
{
    float2 uv = rectUV.xy;
    float2 trimUV = saturate((1 - max(uv, 1 - uv)) * 1000);
    return tex2D(maskSampler, uv) * trimUV.x * trimUV.y;
}
