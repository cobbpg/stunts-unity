Shader "Stunts/ZBiasOn" {
	SubShader {
		Pass {
			Tags { "RenderType" = "Opaque" }
			LOD 200
			
			Offset -1,-1
			Blend Off
			Cull Off
			ZTest Lequal
			
			CGPROGRAM

			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma exclude_renderers d3d11 d3d11_9x
			#define DISPLAY_MASK

			#include "StuntsSurface.cginc"

			ENDCG
		} 
	}
	FallBack "Diffuse"
}
