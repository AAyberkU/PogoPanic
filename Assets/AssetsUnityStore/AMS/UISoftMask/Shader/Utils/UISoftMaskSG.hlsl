//UISoftMask (Shader Graph) by Alexandre Soria http://sorialexandre.tech
#include_with_pragmas "UISoftMask.hlsl"

///MaskData (ShaderGraph): float3 maskDataUV, float3 wPos, out float3 maskData
void MaskData_float(float3 maskDataUV, float3 wPos, out float3 maskData)
{
    maskData = MaskData(maskDataUV, wPos);
}

///UISoftMask (ShaderGraph): float3 maskData, UnityTexture2D maskSampler, float alpha, out float softMask
void UISoftMask_float(float3 maskData, UnityTexture2D maskSampler, float alpha, out float softMask)
{
    float2 uv = maskData.xy;
    float2 trimUV = saturate((1 - max(uv, 1 - uv)) * 1000);
    half mask = SAMPLE_TEXTURE2D(maskSampler, maskSampler.samplerstate, uv).r * trimUV.x * trimUV.y;

    half softMaskFactor = maskData.z * mask;
    softMask = alpha * (softMaskFactor + (1 - maskData.z));
}
