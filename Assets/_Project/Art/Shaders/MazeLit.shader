// Shared lit shader of everything "alive" in the level: characters, weapons, doors, keys, pickups. Same property
// names as Standard (a material switched from Standard keeps its values), Standard lighting from ambient and the
// shadowless directional light, plus Maze lighting (MazeLighting.cginc: lantern, light of the level's sources).
// No realtime shadows, no per-pixel additional lights. Opaque only.
Shader "Maze/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.3
        [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert noforwardadd nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0
        #include "MazeLighting.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 mazeNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.mazeNormal = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = albedo.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb +
                         albedo.rgb * MazeExtraLight(IN.worldPos, normalize(IN.mazeNormal));
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
