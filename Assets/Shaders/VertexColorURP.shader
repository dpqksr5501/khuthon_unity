Shader "Custom/URPVertexColor"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0, 0.1)) = 0.01
        [Toggle] _ShowOutline("Show Outline", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            float _OutlineWidth;
            float4 _OutlineColor;
            float _ShowOutline;

            Varyings vert(Attributes input)
            {
                Varyings output;
                if (_ShowOutline < 0.5) {
                    output.positionHCS = float4(0,0,0,0);
                    return output;
                }
                float3 pos = input.positionOS.xyz + input.normalOS * _OutlineWidth;
                output.positionHCS = TransformObjectToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float4 color : COLOR; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
