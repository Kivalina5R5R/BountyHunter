// Slides what is printed on a straight tape along it, endlessly (TapeScroll): the picture is shifted
// _Offset pixels along _Along (a unit step along the tape, in picture pixels, y up) and the stretch
// that slid in at the start is taken from one repeat of the pattern (_Period pixels) further along,
// so it loops without a seam. Tools/TransitionArt/measure_tapes.py measures _Along and _Period.
Shader "BountyHunter/UI Tape Scroll"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _Size ("Picture Size (pixels)", Vector) = (8192, 512, 0, 0)
        _Along ("Along the Tape", Vector) = (1, 0, 0, 0)
        _Period ("Pattern Repeat (pixels)", Float) = 1000
        _Offset ("Offset (pixels)", Float) = 0

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
            float4 _Size;
            float4 _Along;
            float _Period;
            float _Offset;

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
                float2 along = _Along.xy;
                float2 spot = i.uv * _Size.xy - along * _Offset;
                // Slid in from before the start: one repeat further along shows the same.
                if (spot.x < 0.0) spot += along * _Period;
                if (spot.x < 0.0) spot += along * _Period;
                float2 uv = spot / _Size.xy;
                fixed4 c = tex2D(_MainTex, uv) * i.color;
                c.a *= step(0.0, uv.y) * step(uv.y, 1.0) * step(uv.x, 1.0);
                return c;
            }
            ENDCG
        }
    }
}
