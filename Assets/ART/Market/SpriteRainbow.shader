// A sprite whose coloured parts run through the rainbow: every pixel with colour in it (not the
// white, grey or black ones) takes a hue that drifts with time and changes across the picture, so
// bands of colour sweep over it while its shading stays. _Speed is turns of the rainbow a second,
// _Spread how many rainbows fit across the picture (diagonally), _Saturation boosts the colour and
// _From is how saturated a pixel must be to turn (the wood and gold stay below it).
Shader "BountyHunter/Sprite Rainbow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Speed ("Speed", Float) = 0.25
        _Spread ("Spread", Float) = 0.6
        _Saturation ("Saturation", Range(0, 2)) = 1.1
        _From ("Colour From", Range(0, 1)) = 0.62
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
            float _Speed;
            float _Spread;
            float _Saturation;
            float _From;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float3 RgbToHsv(float3 c)
            {
                float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-5)), d / (q.x + 1e-5), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                return c.z * lerp(float3(1.0, 1.0, 1.0), saturate(p - 1.0), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float3 hsv = RgbToHsv(c.rgb);
                float hue = frac(_Time.y * _Speed - (i.uv.x + i.uv.y) * _Spread);
                float3 rainbow = HsvToRgb(float3(hue, saturate(hsv.y * _Saturation), hsv.z));
                // Only the strongly coloured panels turn; the wood (a duller brown), white, grey and the
                // dark outlines stay as drawn.
                float coloured = smoothstep(_From, _From + 0.12, hsv.y) * smoothstep(0.08, 0.2, hsv.z);
                c.rgb = lerp(c.rgb, rainbow, coloured);
                return c * i.color;
            }
            ENDCG
        }
    }
}
