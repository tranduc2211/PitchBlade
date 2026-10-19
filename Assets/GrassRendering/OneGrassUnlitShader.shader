Shader "Unlit/OneGrassUnlitShader"
{
	Properties
	{
		// _MainTex("Texture", 2D) = "white" {}
	  //   _Color("FixColor", Color) = (1, 1,1, 1)
		 _RootPos("RootPos", Vector) = (0, 0, 0, 0)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Cull Off

		Stencil
			{
				Ref 0
				Comp Equal
				Pass IncrWrap
				ZFail Keep
			}
		Pass
		{
			CGPROGRAM
			// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members brightness)
			//#pragma exclude_renderers d3d11
			// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members brightness)
						//#pragma exclude_renderers d3d11
						#pragma vertex vert
						#pragma fragment frag
						// make fog work
					  //  #pragma multi_compile_fog

						#include "UnityCG.cginc"

						uniform StructuredBuffer<float3> verticesBuffer;
	//   uniform StructuredBuffer<float> brightnessBuffer;
	   uniform StructuredBuffer<int> cullingBuffer;
	   uniform StructuredBuffer<float4> colorBuffer;

	   struct v2f
	   {
		   float2 uv : TEXCOORD0;
		   // float brightness: FLOAT;//whyyy
			float4 vertex : SV_POSITION;
			float4 color: COLOR;
		};
	   float3 _RootPos;

		v2f vert(uint vid: SV_VertexID)
		{
			v2f o;

		 	

			  int cullFactor = cullingBuffer[vid];

			  if (cullFactor == 0 || colorBuffer[vid].a<0.01)
			  {
				  o.vertex = fixed4(0, 0, 0, 1);
				  return o;
				} 
			  float3 pos = verticesBuffer[vid]+_RootPos;
			 
			//	o.vertex = (1-cullFactor)*fixed4(0, 0, 0, 1)+cullFactor*  UnityObjectToClipPos(pos);
			  o.vertex =   UnityObjectToClipPos(pos  );
			//   o.brightness = brightnessBuffer[vid];
			  o.color = colorBuffer[vid];
			  return o;
		  }

		  fixed4 frag(v2f i) : SV_Target
		  {
			  // sample the texture

			//  fixed4 col = fixed4(0 , 1,0,1)  *i.brightness *i.color ;
			//  fixed4 col =  half4(i.color,1) * _MainTex_ST;
			 // fixed4 col = half4(i.color,1) * _Color ;


			 fixed4 col = i.color;
		  //    fixed4 col = fixed4(0, 1, 0, 1);
			// fixed4 col = fixed4(0, 0, 0, 0);
		   //    col = col  * i.brightness;
			 return col;
		 }
		 ENDCG
	 }
	}
}
