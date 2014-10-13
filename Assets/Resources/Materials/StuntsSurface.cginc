#include "UnityCG.cginc"

struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 color : COLOR;
	float4 texcoord : TEXCOORD;
};

struct v2f {
    float4 pos : SV_POSITION;
    float3 worldpos : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float4 color : COLOR;
#ifdef DISPLAY_MASK    
    float4 screenpos : TEXCOORD2;
    float mask : TEXCOORD3;
#endif
};

v2f vert(appdata v) {
    v2f o;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    o.worldpos = mul(_Object2World, v.vertex).xyz;
    o.normal = normalize(float3(mul(_Object2World, float4(v.normal, 0))));
    o.color = v.color;
#ifdef DISPLAY_MASK    
    o.mask = round(v.texcoord.x * 255) + round(v.texcoord.y * 255) * 256;
    o.screenpos = ComputeScreenPos(o.pos) * float4(_ScreenParams.xy * 0.5, 0, 1);
#endif
    return o;
}

half4 frag(v2f i) : COLOR {
#ifdef DISPLAY_MASK
	// A horrible way to simulate simple bitmasking with floating point operations.
	// It would make more sense to build a texture with all the masks and sample that instead.
	float2 screenpos = i.screenpos.xy / i.screenpos.w;
	float bit = pow(2, floor(fmod(screenpos.x, 8)) + 8 * floor(fmod(screenpos.y, 2)));
	// Equivalent to (mask & bit) == 0
	if (floor(fmod(i.mask, bit * 2)) == floor(fmod(i.mask, bit))) {
		discard;
	}
#endif
    return (abs(dot(i.normal, normalize(_WorldSpaceCameraPos - i.worldpos))) * 0.75 + 0.25) * i.color;
}
