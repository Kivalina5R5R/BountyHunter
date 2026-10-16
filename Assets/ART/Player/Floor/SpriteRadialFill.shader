// A sprite shown only up to a fraction of a full turn, clockwise from the top, like a loading ring
// filling up. Fill 0 shows nothing, 1 the whole sprite. The sprite should be centred in its picture.
Shader "BountyHunter/Sprite Radial Fill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Fill ("Fill", Range(0, 1)) = 1
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
            float _Fill;

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
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                // 0 at the top, growing clockwise to 1.
                float2 d = i.uv - 0.5;
                float turn = frac(atan2(d.x, d.y) / (2.0 * UNITY_PI) + 1.0);
                c.a *= step(turn, _Fill) * step(0.0001, _Fill);
                return c;
            }
            ENDCG
        }
    }
}
