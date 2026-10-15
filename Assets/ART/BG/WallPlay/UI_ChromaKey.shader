// Green-screen key for UI pictures (RawImage showing a video). Clearly green pixels turn
// transparent, slightly green ones get a soft edge, and green spill on kept pixels is pulled down
// to the other channels. Same thresholds as Tools/extract_thief_video.py.
Shader "BountyHunter/UI Chroma Key"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video", 2D) = "white" {}
        // How much green must beat red and blue: below Low is kept, above High is removed.
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
            float _KeyLow;
            float _KeyHigh;

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
                fixed4 c = tex2D(_MainTex, i.uv);
                float others = max(c.r, c.b);
                float excess = c.g - others;
                float alpha = 1.0 - saturate((excess - _KeyLow) / max(0.0001, _KeyHigh - _KeyLow));
                c.g = min(c.g, others);
                return fixed4(c.rgb, alpha) * i.color;
            }
            ENDCG
        }
    }
}
