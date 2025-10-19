// Made with Amplify Shader Editor v1.9.3.2
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Slash"
{
	Properties
	{
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[NoScaleOffset]_MainTexture("MainTexture", 2D) = "white" {}
		_MainTiling("MainTiling", Vector) = (0,0,0,0)
		_MainOffset("MainOffset", Vector) = (-0.07,0,0,0)
		_MainScroll("MainScroll", Vector) = (0,0,0,0)
		[Toggle]_LoopMain("LoopMain", Float) = 0
		[NoScaleOffset]_Mask("Mask", 2D) = "white" {}
		_MaskScale("MaskScale", Vector) = (1,1,0,0)
		_MaskOffset("MaskOffset", Range( -1 , 1)) = 0
		_MaskScroll("MaskScroll", Vector) = (0,0,0,0)
		_EdgeSharpness("EdgeSharpness", Range( 0 , 1)) = 0
		_StaticMask("StaticMask", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		[HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

		Cull Off
		HLSLINCLUDE
		#pragma target 2.0
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
		ENDHLSL

		
		Pass
		{
			Name "Sprite Unlit"
			Tags { "LightMode"="Universal2D" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_SRP_VERSION 140010


			#pragma vertex vert
			#pragma fragment frag

			#define _SURFACE_TYPE_TRANSPARENT 1
			#define SHADERPASS SHADERPASS_SPRITEUNLIT

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR


			sampler2D _MainTexture;
			sampler2D _Mask;
			sampler2D _StaticMask;
			CBUFFER_START( UnityPerMaterial )
			float4 _StaticMask_ST;
			float2 _MainTiling;
			float2 _MainOffset;
			float2 _MainScroll;
			float2 _MaskScale;
			float2 _MaskScroll;
			float _LoopMain;
			float _EdgeSharpness;
			float _MaskOffset;
			CBUFFER_END


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D( _AlphaTex ); SAMPLER( sampler_AlphaTex );
				float _EnableAlphaTexture;
			#endif

			float4 _RendererColor;

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord3 = v.ase_texcoord1;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );

				o.texCoord0 = v.uv0;
				o.color = v.color;
				o.positionCS = vertexInput.positionCS;
				o.positionWS = vertexInput.positionWS;

				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float2 texCoord2 = IN.texCoord0.xy * _MainTiling + _MainOffset;
				float3 texCoord78 = IN.ase_texcoord3.xyz;
				texCoord78.xy = IN.ase_texcoord3.xyz.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult77 = (float2(pow( texCoord2.x , ( 1.0 + texCoord78.z ) ) , texCoord2.y));
				float4 texCoord41 = IN.texCoord0;
				texCoord41.xy = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_9_0 = ( 1.0 - texCoord41.z );
				float2 appendResult55 = (float2(( _MainScroll.x * temp_output_9_0 ) , ( temp_output_9_0 * _MainScroll.y )));
				float2 temp_output_6_0 = ( appendResult77 + appendResult55 );
				float2 clampResult8 = clamp( temp_output_6_0 , float2( -99,0 ) , float2( 1,1 ) );
				float4 tex2DNode1 = tex2D( _MainTexture, (( _LoopMain )?( temp_output_6_0 ):( clampResult8 )) );
				float4 texCoord68 = IN.ase_texcoord3;
				texCoord68.xy = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float4 texCoord42 = IN.texCoord0;
				texCoord42.xy = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 texCoord44 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult43 = (float2(0.0 , _MaskOffset));
				float2 texCoord31 = IN.texCoord0.xy * _MaskScale + ( ( _MaskScroll * ( 1.0 - texCoord44.x ) ) + appendResult43 );
				float4 appendResult29 = (float4(texCoord31.x , texCoord31.y , 0.0 , 0.0));
				float4 tex2DNode10 = tex2D( _Mask, appendResult29.xy );
				float smoothstepResult12 = smoothstep( texCoord42.w , ( texCoord42.w + _EdgeSharpness ) , tex2DNode10.r);
				float2 uv_StaticMask = IN.texCoord0.xy * _StaticMask_ST.xy + _StaticMask_ST.zw;
				float4 appendResult91 = (float4(( tex2DNode1 * IN.color * ( texCoord68.y + 1.0 ) ).rgb , ( tex2DNode1.a * smoothstepResult12 * IN.color.a * tex2D( _StaticMask, uv_StaticMask ).r )));
				
				float4 Color = appendResult91;

				#if ETC1_EXTERNAL_ALPHA
					float4 alpha = SAMPLE_TEXTURE2D( _AlphaTex, sampler_AlphaTex, IN.texCoord0.xy );
					Color.a = lerp( Color.a, alpha.r, _EnableAlphaTexture );
				#endif

				#if defined(DEBUG_DISPLAY)
				SurfaceData2D surfaceData;
				InitializeSurfaceData(Color.rgb, Color.a, surfaceData);
				InputData2D inputData;
				InitializeInputData(IN.positionWS.xy, half2(IN.texCoord0.xy), inputData);
				half4 debugColor = 0;

				SETUP_DEBUG_DATA_2D(inputData, IN.positionWS);

				if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
				{
					return debugColor;
				}
				#endif

				Color *= IN.color * _RendererColor;
				return Color;
			}

			ENDHLSL
		}
		
		Pass
		{
			
			Name "Sprite Unlit Forward"
            Tags { "LightMode"="UniversalForward" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_SRP_VERSION 140010


			#pragma vertex vert
			#pragma fragment frag

			#define _SURFACE_TYPE_TRANSPARENT 1
			#define SHADERPASS SHADERPASS_SPRITEFORWARD

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR


			sampler2D _MainTexture;
			sampler2D _Mask;
			sampler2D _StaticMask;
			CBUFFER_START( UnityPerMaterial )
			float4 _StaticMask_ST;
			float2 _MainTiling;
			float2 _MainOffset;
			float2 _MainScroll;
			float2 _MaskScale;
			float2 _MaskScroll;
			float _LoopMain;
			float _EdgeSharpness;
			float _MaskOffset;
			CBUFFER_END


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D( _AlphaTex ); SAMPLER( sampler_AlphaTex );
				float _EnableAlphaTexture;
			#endif

			float4 _RendererColor;

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord3 = v.ase_texcoord1;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );

				o.texCoord0 = v.uv0;
				o.color = v.color;
				o.positionCS = vertexInput.positionCS;
				o.positionWS = vertexInput.positionWS;

				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float2 texCoord2 = IN.texCoord0.xy * _MainTiling + _MainOffset;
				float3 texCoord78 = IN.ase_texcoord3.xyz;
				texCoord78.xy = IN.ase_texcoord3.xyz.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult77 = (float2(pow( texCoord2.x , ( 1.0 + texCoord78.z ) ) , texCoord2.y));
				float4 texCoord41 = IN.texCoord0;
				texCoord41.xy = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_9_0 = ( 1.0 - texCoord41.z );
				float2 appendResult55 = (float2(( _MainScroll.x * temp_output_9_0 ) , ( temp_output_9_0 * _MainScroll.y )));
				float2 temp_output_6_0 = ( appendResult77 + appendResult55 );
				float2 clampResult8 = clamp( temp_output_6_0 , float2( -99,0 ) , float2( 1,1 ) );
				float4 tex2DNode1 = tex2D( _MainTexture, (( _LoopMain )?( temp_output_6_0 ):( clampResult8 )) );
				float4 texCoord68 = IN.ase_texcoord3;
				texCoord68.xy = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float4 texCoord42 = IN.texCoord0;
				texCoord42.xy = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 texCoord44 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult43 = (float2(0.0 , _MaskOffset));
				float2 texCoord31 = IN.texCoord0.xy * _MaskScale + ( ( _MaskScroll * ( 1.0 - texCoord44.x ) ) + appendResult43 );
				float4 appendResult29 = (float4(texCoord31.x , texCoord31.y , 0.0 , 0.0));
				float4 tex2DNode10 = tex2D( _Mask, appendResult29.xy );
				float smoothstepResult12 = smoothstep( texCoord42.w , ( texCoord42.w + _EdgeSharpness ) , tex2DNode10.r);
				float2 uv_StaticMask = IN.texCoord0.xy * _StaticMask_ST.xy + _StaticMask_ST.zw;
				float4 appendResult91 = (float4(( tex2DNode1 * IN.color * ( texCoord68.y + 1.0 ) ).rgb , ( tex2DNode1.a * smoothstepResult12 * IN.color.a * tex2D( _StaticMask, uv_StaticMask ).r )));
				
				float4 Color = appendResult91;

				#if ETC1_EXTERNAL_ALPHA
					float4 alpha = SAMPLE_TEXTURE2D( _AlphaTex, sampler_AlphaTex, IN.texCoord0.xy );
					Color.a = lerp( Color.a, alpha.r, _EnableAlphaTexture );
				#endif


				#if defined(DEBUG_DISPLAY)
				SurfaceData2D surfaceData;
				InitializeSurfaceData(Color.rgb, Color.a, surfaceData);
				InputData2D inputData;
				InitializeInputData(IN.positionWS.xy, half2(IN.texCoord0.xy), inputData);
				half4 debugColor = 0;

				SETUP_DEBUG_DATA_2D(inputData, IN.positionWS);

				if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
				{
					return debugColor;
				}
				#endif

				Color *= IN.color * _RendererColor;
				return Color;
			}

			ENDHLSL
		}
		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }

            Cull Off

            HLSLPROGRAM

			#define ASE_SRP_VERSION 140010


			#pragma vertex vert
			#pragma fragment frag

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX
            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENESELECTIONPASS 1


            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_COLOR


			sampler2D _MainTexture;
			sampler2D _Mask;
			sampler2D _StaticMask;
			CBUFFER_START( UnityPerMaterial )
			float4 _StaticMask_ST;
			float2 _MainTiling;
			float2 _MainOffset;
			float2 _MainScroll;
			float2 _MaskScale;
			float2 _MaskScroll;
			float _LoopMain;
			float _EdgeSharpness;
			float _MaskOffset;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};


            int _ObjectId;
            int _PassValue;

			
			VertexOutput vert(VertexInput v )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord = v.ase_texcoord;
				o.ase_texcoord1 = v.ase_texcoord1;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
				float3 positionWS = TransformObjectToWorld(v.positionOS);
				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				float2 texCoord2 = IN.ase_texcoord.xy * _MainTiling + _MainOffset;
				float3 texCoord78 = IN.ase_texcoord1.xyz;
				texCoord78.xy = IN.ase_texcoord1.xyz.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult77 = (float2(pow( texCoord2.x , ( 1.0 + texCoord78.z ) ) , texCoord2.y));
				float4 texCoord41 = IN.ase_texcoord;
				texCoord41.xy = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_9_0 = ( 1.0 - texCoord41.z );
				float2 appendResult55 = (float2(( _MainScroll.x * temp_output_9_0 ) , ( temp_output_9_0 * _MainScroll.y )));
				float2 temp_output_6_0 = ( appendResult77 + appendResult55 );
				float2 clampResult8 = clamp( temp_output_6_0 , float2( -99,0 ) , float2( 1,1 ) );
				float4 tex2DNode1 = tex2D( _MainTexture, (( _LoopMain )?( temp_output_6_0 ):( clampResult8 )) );
				float4 texCoord68 = IN.ase_texcoord1;
				texCoord68.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float4 texCoord42 = IN.ase_texcoord;
				texCoord42.xy = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 texCoord44 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult43 = (float2(0.0 , _MaskOffset));
				float2 texCoord31 = IN.ase_texcoord.xy * _MaskScale + ( ( _MaskScroll * ( 1.0 - texCoord44.x ) ) + appendResult43 );
				float4 appendResult29 = (float4(texCoord31.x , texCoord31.y , 0.0 , 0.0));
				float4 tex2DNode10 = tex2D( _Mask, appendResult29.xy );
				float smoothstepResult12 = smoothstep( texCoord42.w , ( texCoord42.w + _EdgeSharpness ) , tex2DNode10.r);
				float2 uv_StaticMask = IN.ase_texcoord.xy * _StaticMask_ST.xy + _StaticMask_ST.zw;
				float4 appendResult91 = (float4(( tex2DNode1 * IN.ase_color * ( texCoord68.y + 1.0 ) ).rgb , ( tex2DNode1.a * smoothstepResult12 * IN.ase_color.a * tex2D( _StaticMask, uv_StaticMask ).r )));
				
				float4 Color = appendResult91;

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}

            ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

            Cull Off

            HLSLPROGRAM

			#define ASE_SRP_VERSION 140010


			#pragma vertex vert
			#pragma fragment frag

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX
            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENEPICKINGPASS 1


            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        	#define ASE_NEEDS_FRAG_COLOR


			sampler2D _MainTexture;
			sampler2D _Mask;
			sampler2D _StaticMask;
			CBUFFER_START( UnityPerMaterial )
			float4 _StaticMask_ST;
			float2 _MainTiling;
			float2 _MainOffset;
			float2 _MainScroll;
			float2 _MaskScale;
			float2 _MaskScroll;
			float _LoopMain;
			float _EdgeSharpness;
			float _MaskOffset;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

            float4 _SelectionID;

			
			VertexOutput vert(VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord = v.ase_texcoord;
				o.ase_texcoord1 = v.ase_texcoord1;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
				float3 positionWS = TransformObjectToWorld(v.positionOS);
				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				float2 texCoord2 = IN.ase_texcoord.xy * _MainTiling + _MainOffset;
				float3 texCoord78 = IN.ase_texcoord1.xyz;
				texCoord78.xy = IN.ase_texcoord1.xyz.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult77 = (float2(pow( texCoord2.x , ( 1.0 + texCoord78.z ) ) , texCoord2.y));
				float4 texCoord41 = IN.ase_texcoord;
				texCoord41.xy = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_9_0 = ( 1.0 - texCoord41.z );
				float2 appendResult55 = (float2(( _MainScroll.x * temp_output_9_0 ) , ( temp_output_9_0 * _MainScroll.y )));
				float2 temp_output_6_0 = ( appendResult77 + appendResult55 );
				float2 clampResult8 = clamp( temp_output_6_0 , float2( -99,0 ) , float2( 1,1 ) );
				float4 tex2DNode1 = tex2D( _MainTexture, (( _LoopMain )?( temp_output_6_0 ):( clampResult8 )) );
				float4 texCoord68 = IN.ase_texcoord1;
				texCoord68.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float4 texCoord42 = IN.ase_texcoord;
				texCoord42.xy = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 texCoord44 = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult43 = (float2(0.0 , _MaskOffset));
				float2 texCoord31 = IN.ase_texcoord.xy * _MaskScale + ( ( _MaskScroll * ( 1.0 - texCoord44.x ) ) + appendResult43 );
				float4 appendResult29 = (float4(texCoord31.x , texCoord31.y , 0.0 , 0.0));
				float4 tex2DNode10 = tex2D( _Mask, appendResult29.xy );
				float smoothstepResult12 = smoothstep( texCoord42.w , ( texCoord42.w + _EdgeSharpness ) , tex2DNode10.r);
				float2 uv_StaticMask = IN.ase_texcoord.xy * _StaticMask_ST.xy + _StaticMask_ST.zw;
				float4 appendResult91 = (float4(( tex2DNode1 * IN.ase_color * ( texCoord68.y + 1.0 ) ).rgb , ( tex2DNode1.a * smoothstepResult12 * IN.ase_color.a * tex2D( _StaticMask, uv_StaticMask ).r )));
				
				float4 Color = appendResult91;
				half4 outColor = _SelectionID;
				return outColor;
			}

            ENDHLSL
        }
		
	}
	// CustomEditor "ASEMaterialInspector"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=19302
Node;AmplifyShaderEditor.TextureCoordinatesNode;41;-1503.131,30.57038;Inherit;True;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;44;-898.2465,526.056;Inherit;False;1;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;62;-1129.084,-540.9309;Inherit;False;Property;_MainTiling;MainTiling;1;0;Create;True;0;0;0;False;0;False;0,0;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;63;-1140.584,-357.0073;Inherit;False;Property;_MainOffset;MainOffset;2;0;Create;True;0;0;0;False;0;False;-0.07,0;0.75,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;80;-726.8883,-946.8317;Inherit;False;Constant;_Float2;Float 2;12;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;78;-786.1381,-858.2084;Inherit;True;1;-1;3;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;9;-1193.788,17.13062;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;58;-1222.901,-140.4532;Inherit;False;Property;_MainScroll;MainScroll;3;0;Create;True;0;0;0;False;0;False;0,0;-1,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.OneMinusNode;46;-613.5474,565.0714;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;51;-774.9366,751.4554;Inherit;False;Property;_MaskOffset;MaskOffset;7;0;Create;True;0;0;0;False;0;False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;64;-593.561,399.1113;Inherit;False;Property;_MaskScroll;MaskScroll;8;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;2;-643.1315,-422.6316;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;79;-504.4402,-815.1527;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;59;-925.664,188.0646;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-956.6622,-106.3203;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;65;-351.886,475.1554;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;43;-323.2667,649.3185;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PowerNode;75;-390.0539,-581.8015;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;55;-723.2347,29.27092;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;26;-297.0805,893.0111;Inherit;False;Property;_MaskScale;MaskScale;6;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;66;-84.75073,599.7093;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;77;-208.9484,-565.2256;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;6;-95.58041,-386.0784;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;31;139.7293,690.3251;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;29;653.7019,530.7637;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;18;376.2626,1475.97;Inherit;False;Property;_EdgeSharpness;EdgeSharpness;9;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;42;393.8576,1108.973;Inherit;True;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;8;158.697,-253.9434;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;-99,0;False;2;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;19;802.9433,1386.165;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;61;377.2645,-382.0606;Inherit;False;Property;_LoopMain;LoopMain;4;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;10;869.5454,486.8579;Inherit;True;Property;_Mask;Mask;5;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;3584f2bf4afb5284d91edb6a29126e62;3584f2bf4afb5284d91edb6a29126e62;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;68;1220.828,-375.4452;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;69;1254.842,-198.2165;Inherit;False;Constant;_Float0;Float 0;10;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;12;1503.023,896.5301;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;954.0311,-69.44924;Inherit;True;Property;_MainTexture;MainTexture;0;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;743e979d144c0924583c2cd743af24e4;bcec5d7749ce70a46b57b7e8fc65506c;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VertexColorNode;49;1209.573,223.4704;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;72;1767.225,489.6884;Inherit;True;Property;_StaticMask;StaticMask;10;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;70;1487.566,-262.6633;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;11;2115.739,271.6643;Inherit;True;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;47;2152.292,25.15945;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;40;1185.719,723.7791;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;37;862.613,845.4276;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;540.226,930.5369;Inherit;False;Constant;_Float1;Float 1;8;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;91;2401.236,92.73373;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;92;2570.288,16.49028;Float;False;True;-1;2;ASEMaterialInspector;0;15;Slash;cf964e524c8e69742b1d21fbe2ebcc4a;True;Sprite Unlit;0;0;Sprite Unlit;4;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;Hidden/InternalErrorShader;0;0;Standard;3;Vertex Position;1;0;Debug Display;0;0;External Alpha;0;0;0;4;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;93;2570.288,16.49028;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;cf964e524c8e69742b1d21fbe2ebcc4a;True;Sprite Unlit Forward;0;1;Sprite Unlit Forward;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;94;2570.288,16.49028;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;cf964e524c8e69742b1d21fbe2ebcc4a;True;SceneSelectionPass;0;2;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;95;2570.288,16.49028;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;cf964e524c8e69742b1d21fbe2ebcc4a;True;ScenePickingPass;0;3;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
WireConnection;9;0;41;3
WireConnection;46;0;44;1
WireConnection;2;0;62;0
WireConnection;2;1;63;0
WireConnection;79;0;80;0
WireConnection;79;1;78;3
WireConnection;59;0;9;0
WireConnection;59;1;58;2
WireConnection;57;0;58;1
WireConnection;57;1;9;0
WireConnection;65;0;64;0
WireConnection;65;1;46;0
WireConnection;43;1;51;0
WireConnection;75;0;2;1
WireConnection;75;1;79;0
WireConnection;55;0;57;0
WireConnection;55;1;59;0
WireConnection;66;0;65;0
WireConnection;66;1;43;0
WireConnection;77;0;75;0
WireConnection;77;1;2;2
WireConnection;6;0;77;0
WireConnection;6;1;55;0
WireConnection;31;0;26;0
WireConnection;31;1;66;0
WireConnection;29;0;31;1
WireConnection;29;1;31;2
WireConnection;8;0;6;0
WireConnection;19;0;42;4
WireConnection;19;1;18;0
WireConnection;61;0;8;0
WireConnection;61;1;6;0
WireConnection;10;1;29;0
WireConnection;12;0;10;1
WireConnection;12;1;42;4
WireConnection;12;2;19;0
WireConnection;1;1;61;0
WireConnection;70;0;68;2
WireConnection;70;1;69;0
WireConnection;11;0;1;4
WireConnection;11;1;12;0
WireConnection;11;2;49;4
WireConnection;11;3;72;1
WireConnection;47;0;1;0
WireConnection;47;1;49;0
WireConnection;47;2;70;0
WireConnection;40;0;10;1
WireConnection;40;1;37;0
WireConnection;40;2;37;0
WireConnection;37;0;39;0
WireConnection;37;1;31;1
WireConnection;91;0;47;0
WireConnection;91;3;11;0
WireConnection;92;1;91;0
ASEEND*/
//CHKSM=27866302BFF309745040ADF5A796601468FE4121