Shader "AMS/UISoftMask"
{
	Properties
	{
		[PerRendererData]
		_MainTex ("MainTex", 2D) = "white" { }
		[PerRendererData]
		_SoftMask ("_SoftMask", 2D) = "white" { }
		[HideInInspector]
		_Color ("Tint", Color) = (1, 1, 1, 1)
		[Space(10)]

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
		[Space(10)]
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

		Pass
		{
			Stencil
			{
				Ref [_Stencil]
				ReadMask [_StencilReadMask]
				WriteMask [_StencilWriteMask]
				Comp [_StencilComp]
				Pass [_StencilOp]
			}

			Cull Off
			Lighting Off
			ZWrite Off
			ZTest [unity_GUIZTestMode]
			Blend SrcAlpha OneMinusSrcAlpha
			ColorMask [_ColorMask]

			Name "UISoftMaskPass"

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#pragma target 3.0

			#include_with_pragmas "Utils/UISoftMaskPass.hlsl"
			
			ENDCG
		}
	}
	Fallback Off
}