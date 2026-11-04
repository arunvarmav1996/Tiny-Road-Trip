// Additive sprite for the summer lens flare (SummerLensFlare): adds light to the screen, so flare elements brighten
// what is behind them instead of painting over it. Tint and alpha come from the SpriteRenderer colour.
Shader "Hidden/TinyRoadTrip/FlareAdditive"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
                fixed a = SampleSpriteTexture(i.texcoord).a * i.color.a;
                return fixed4(i.color.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
