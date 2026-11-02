// Draws a UI picture along a path rather than all at once (PathReveal): _PathMap holds, for every
// point of the picture, how far along the path it lies (0 at its start, 1 at its end;
// Tools/TransitionArt/make_streak_path.py), and only the part between _From and _To shows, with a
// soft edge _Softness wide. The leading edge glows brighter (_HeadGlow), like the tip of a bolt.
Shader "BountyHunter/UI Path Reveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _PathMap ("Path Map", 2D) = "black" {}
        _From ("From", Range(0, 1)) = 0
        _To ("To", Range(0, 1)) = 1
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.015
        _HeadGlow ("Head Glow", Range(0, 2)) = 0.6

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PathMap;
            float _From;
            float _To;
            float _Softness;
            float _HeadGlow;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float along = tex2D(_PathMap, i.uv).r;
                float shown = smoothstep(_From - _Softness, _From, along) * (1.0 - smoothstep(_To, _To + _Softness, along));
                // The tip, while the path is still being drawn.
                float head = saturate(1.0 - abs(along - _To) / (_Softness * 4.0)) * step(_To, 0.999);
                c.rgb += c.rgb * head * _HeadGlow;
                c.a *= shown;
                return c;
            }
            ENDCG
        }
    }
}
