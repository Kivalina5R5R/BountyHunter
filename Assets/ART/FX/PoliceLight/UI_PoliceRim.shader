// Rim light for UI pictures: lights only the edge of the picture that faces a light, in the
// vertex colour, added on top of what is behind. Used by PoliceLightRim to splash the police car's
// siren onto the backs of the wall characters. _LightDir is the direction toward the light in
// the picture's own space (x right, y up). With KEY_GREEN on, the picture is a green-screen video
// and its shape comes from the green key (same thresholds as UI Chroma Key) instead of alpha.
Shader "BountyHunter/UI Police Rim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _LightDir ("Light Direction", Vector) = (1, 0, 0, 0)
        _RimWidth ("Rim Width (texels)", Float) = 18
        _Strength ("Strength", Float) = 1.6
        [Toggle(KEY_GREEN)] _KeyGreen ("Green Screen Video", Float) = 0
        _KeyLow ("Key Low", Range(0, 1)) = 0.1
        _KeyHigh ("Key High", Range(0, 1)) = 0.353

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
            "CanUseSpriteAtlas" = "True"
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
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ KEY_GREEN
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
            float4 _MainTex_TexelSize;
            float4 _LightDir;
            float _RimWidth;
            float _Strength;
            float _KeyLow;
            float _KeyHigh;

            // How much of the character is at uv: its alpha, or for a video, how far from green it is.
            float Shape(float2 uv)
            {
                fixed4 c = tex2D(_MainTex, uv);
            #ifdef KEY_GREEN
                float excess = c.g - max(c.r, c.b);
                return 1.0 - saturate((excess - _KeyLow) / max(0.0001, _KeyHigh - _KeyLow));
            #else
                return c.a;
            #endif
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float inside = Shape(i.uv);
                float2 toLight = normalize(_LightDir.xy + float2(0.0001, 0)) * _MainTex_TexelSize.xy * _RimWidth;

                // A pixel is on the lit edge when the picture is empty a little way toward the light;
                // three steps make the light fade softly inward instead of a hard line.
                float open = 3.0 - Shape(i.uv + toLight * 0.33)
                                 - Shape(i.uv + toLight * 0.66)
                                 - Shape(i.uv + toLight);
                float rim = inside * (open / 3.0) * _Strength * i.color.a;
                return fixed4(i.color.rgb * rim, rim);
            }
            ENDCG
        }
    }
}
