// Full-screen colour grade for the weather filter (WeatherScreenFilter): brightness, contrast, saturation,
// then a multiply tint. One texture read and a few multiplies, cheap enough for phones and WebGL.
Shader "Hidden/TinyRoadTrip/WeatherGrade"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Tint;  // rgb: colour the screen is multiplied towards
            half4 _Grade; // x: brightness, y: contrast, z: saturation, w: tint strength

            half4 frag (v2f_img i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                half3 rgb = c.rgb * _Grade.x;
                rgb = (rgb - 0.5h) * _Grade.y + 0.5h;
                half luma = dot(rgb, half3(0.299h, 0.587h, 0.114h));
                rgb = lerp(luma.xxx, rgb, _Grade.z);
                rgb *= lerp(half3(1.0h, 1.0h, 1.0h), _Tint.rgb, _Grade.w);
                return half4(saturate(rgb), c.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
