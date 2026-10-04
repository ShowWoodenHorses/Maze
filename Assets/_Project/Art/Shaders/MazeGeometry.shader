// Static level geometry (floor, walls) of the Maze. Standard lighting plus per-cell visibility (ТЗ §54, §105):
// every vertex carries its grid cell in UV3; when MAZE_VISIBILITY is on, vertices of cells hidden in the global
// _MazeVisibility mask collapse to a point, so a combined chunk mesh can hide single cells. Shadows follow
// (addshadow runs the same vertex function). Without the keyword (editor preview) everything is visible.
Shader "Maze/Geometry"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // target 3.5: vertex texture fetch (GLES 3 / WebGL 2).
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        #pragma multi_compile _ MAZE_VISIBILITY

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;

        #if MAZE_VISIBILITY
        sampler2D _MazeVisibility;
        float4 _MazeVisibilitySize; // x = 1 / width, y = 1 / height
        #endif

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
        };

        void vert(inout appdata_full v)
        {
            #if MAZE_VISIBILITY
            float2 cell = floor(v.texcoord3.xy + 0.5);
            float visible = tex2Dlod(_MazeVisibility, float4((cell + 0.5) * _MazeVisibilitySize.xy, 0, 0)).r;
            v.vertex.xyz *= step(0.5, visible);
            #endif
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = albedo.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
