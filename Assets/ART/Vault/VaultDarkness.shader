// The vault's blackout: a dark sheet over the floor, lifted round up to 16 lights (_Lights: x and
// world z of each, its radius, and how much it lifts the dark there, 0 to 1). Each light is full
// strength out to _Core of its radius and fades softly to nothing at its edge, so no light has a hard
// rim. The sheet's colour (the sprite's tint) sets how dark it is.
Shader "BountyHunter/Vault Darkness"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Core ("Light Core", Range(0, 1)) = 0.35
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
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float3 world : TEXCOORD0;
            };

            float4 _Lights[16];
            float _LightCount;
            float _Core;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // How much of the dark is left after every light has lifted its share.
                float dark = 1.0;
                for (int n = 0; n < 16; n++)
                {
                    if (n >= (int)_LightCount) break;

                    float4 light = _Lights[n];
                    float distance = length(i.world.xz - light.xy);
                    float reach = 1.0 - smoothstep(light.z * _Core, light.z, distance);
                    dark *= 1.0 - reach * light.w;
                }

                return fixed4(i.color.rgb, i.color.a * dark);
            }
            ENDCG
        }
    }
}
