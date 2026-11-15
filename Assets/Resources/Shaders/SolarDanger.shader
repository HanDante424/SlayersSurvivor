Shader "Slayers/SolarDanger" {
Properties { [PerRendererData] _MainTex("Sprite Texture",2D)="white"{} }
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float4 _Origin,_Pillar0,_Pillar1,_Pillar2,_Pillar3;
struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
struct output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
output vert(input v){ output o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy; o.uv=v.uv; o.color=v.color; return o; }
bool covered(float2 samplePosition,float4 pillar){ float2 ray=samplePosition-_Origin.xy; float len=length(ray); if(len<=0.0001)return false; float2 off=pillar.xy-_Origin.xy; float distanceToPillar=length(off); float along=sqrt(max(0,(dot(off,off)+dot(off,ray/len)*distanceToPillar)*0.5)); float crossSq=dot(off,off)-along*along; if(crossSq>pillar.z*pillar.z)return false; float nearHit=along-sqrt(max(0,pillar.z*pillar.z-crossSq)); return nearHit>=0&&nearHit<=len; }
fixed4 frag(output i):SV_Target { if(covered(i.world,_Pillar0)||covered(i.world,_Pillar1)||covered(i.world,_Pillar2)||covered(i.world,_Pillar3))discard; fixed4 c=tex2D(_MainTex,i.uv)*i.color; c.a*=0.75+0.25*sin(_Time.y*14); return c; }
ENDCG } } }
