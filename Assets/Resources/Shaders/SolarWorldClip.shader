Shader "Slayers/SolarWorldClip" {
Properties {
    [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
    _ClipY("Ground height",Float)=0
    _ClipCenterX("Ground center X",Float)=0
    _ClipRadius("Ground arc half width",Float)=1
    _ClipDepth("Ground arc depth",Float)=0
}
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float _ClipY, _ClipCenterX, _ClipRadius, _ClipDepth;
struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
struct output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
output vert(input v){ output o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy; o.uv=v.uv; o.color=v.color; return o; }
fixed4 frag(output i):SV_Target {
    fixed4 c=tex2D(_MainTex,i.uv)*i.color;
    float x=(i.world.x-_ClipCenterX)/max(_ClipRadius,0.0001);
    // 지면의 앞쪽 얕은 반타원 아래만 숨김. 화염 고리는 이 머티리얼을 쓰지 않는다.
    float cutY=_ClipY-_ClipDepth*sqrt(saturate(1-x*x));
    float distanceAbove=i.world.y-cutY;
    float edge=max(fwidth(distanceAbove),0.0001);
    c.a*=smoothstep(-edge,edge,distanceAbove);
    return c;
}
ENDCG } } }
