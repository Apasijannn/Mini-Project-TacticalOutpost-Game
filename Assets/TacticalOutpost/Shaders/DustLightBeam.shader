Shader "TacticalOutpost/DustLightBeam"
{
    Properties { _BeamTint ("Beam tint", Color)=(1,0.025,0.01,1) _Density ("Dust density", Range(0,1))=0.075 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            Cull Front
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BeamTint;
            float _Density;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings Vert(Attributes i) { Varyings o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float3 origin=GetCameraPositionWS();
                float3 direction=normalize(i.world-origin);
                if(unity_OrthoParams.w>0.5) { direction=-GetViewForwardDir(); origin=i.world-direction*dot(i.world-origin,direction); }
                float3 ro=TransformWorldToObject(origin);
                float3 rd=mul((float3x3)unity_WorldToObject,direction);
                float3 safeRD=rd+float3(abs(rd.x)<1e-6?1e-6:0,abs(rd.y)<1e-6?1e-6:0,abs(rd.z)<1e-6?1e-6:0);
                float3 a=(float3(-1,-1,0)-ro)/safeRD, b=(float3(1,1,1)-ro)/safeRD;
                float3 lo=min(a,b), hi=max(a,b);
                float begin=max(0,max(lo.x,max(lo.y,lo.z)));
                float end=min(hi.x,min(hi.y,hi.z));
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 surface=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                end=min(end,dot(surface-origin,direction));
                if(end<=begin) return 0;
                float stepLength=(end-begin)/16;
                float jitter=frac(sin(dot(i.positionCS.xy,float2(12.9898,78.233)))*43758.5453);
                float density=0;
                // shortcut: camera depth clips surfaces, use shadow-map scattering if light-path occlusion becomes necessary.
                [unroll] for(int n=0;n<16;n++) {
                    float3 p=ro+rd*(begin+(n+jitter)*stepLength);
                    float radius=length(p.xy)/max(p.z,.001);
                    float edge=1-smoothstep(.4,1,radius);
                    float fade=smoothstep(0,.045,p.z)*(1-smoothstep(.65,1,p.z));
                    float3 world=origin+direction*(begin+(n+jitter)*stepLength);
                    float dust=.88+.12*sin(dot(world,float3(.73,.41,.57))-_Time.y*.25);
                    density+=edge*fade*dust/(1+8*p.z*p.z);
                }
                float scatter=(1-exp(-density*stepLength*_Density))*1.8;
                return half4(_BeamTint.rgb*scatter,0);
            }
            ENDHLSL
        }
    }
}
