Shader "Unlit/OneGrassUnlitShader"
{
	Properties
	{
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
			// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members brightness)
						#pragma vertex vert
						#pragma fragment frag
						// make fog work

						#include "UnityCG.cginc"

						uniform StructuredBuffer<float3> verticesBuffer;
	   uniform StructuredBuffer<int> cullingBuffer;
	   uniform StructuredBuffer<float4> colorBuffer;

	   struct v2f
	   {
		   float2 uv : TEXCOORD0;
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
			 
			  o.vertex =   UnityObjectToClipPos(pos  );
			  o.color = colorBuffer[vid];
			  return o;
		  }

		  fixed4 frag(v2f i) : SV_Target
		  {
			  // sample the texture



			 fixed4 col = i.color;
			 return col;
		 }
		 ENDCG
	 }
	}
}
