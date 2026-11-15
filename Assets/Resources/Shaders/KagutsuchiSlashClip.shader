Shader "Slayers/KagutsuchiSlashClip" {
Properties {
    [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
    _ClipCenter("Circle center",Vector)=(0,0,0,0)
    _ClipRadius("Circle radius",Float)=1
}
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float4 _ClipCenter;
float _ClipRadius;
struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
struct output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
output vert(input v) {
    output o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy;
    o.uv=v.uv; o.color=v.color; return o;
}
fixed4 frag(output i):SV_Target {
    float2 delta=i.world-_ClipCenter.xy;
    float boundary=_ClipRadius-length(delta);
    clip(boundary);
    fixed4 c=tex2D(_MainTex,i.uv)*i.color;
    c.a*=smoothstep(0,max(fwidth(boundary),0.0001),boundary);
    return c;
}
ENDCG } } }
