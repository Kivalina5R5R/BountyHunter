// Rim light for UI pictures: lights only the edge of the picture that faces a light, in the
// vertex colour, added on top of what is behind. Used by PoliceLightRim to splash the police car's
// siren onto the backs of the wall characters. _LightDir is the direction toward the light in
// the picture's own space (x right, y up).
Shader "BountyHunter/UI Police Rim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _LightDir ("Light Direction", Vector) = (1, 0, 0, 0)
        _RimWidth ("Rim Width (texels)", Float) = 18
        _Strength ("Strength", Float) = 1.6

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
                float inside = tex2D(_MainTex, i.uv).a;
                float2 toLight = normalize(_LightDir.xy + float2(0.0001, 0)) * _MainTex_TexelSize.xy * _RimWidth;

                // A pixel is on the lit edge when the picture is empty a little way toward the light;
                // three steps make the light fade softly inward instead of a hard line.
                float open = 3.0 - tex2D(_MainTex, i.uv + toLight * 0.33).a
                                 - tex2D(_MainTex, i.uv + toLight * 0.66).a
                                 - tex2D(_MainTex, i.uv + toLight).a;
                float rim = inside * (open / 3.0) * _Strength * i.color.a;
                return fixed4(i.color.rgb * rim, rim);
            }
            ENDCG
        }
    }
}
