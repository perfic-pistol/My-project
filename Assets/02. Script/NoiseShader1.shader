Shader "Custom/NoiseShader1"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Noise Intensity", Range(0,1)) = 0
    }
    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;

            float rand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.98, 78.23))) * 43758.55);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                if (_Intensity < 0.01) return col;

                float2 noiseUV = uv * 150 + _Time.y * 3;
                float noise = rand(noiseUV);

                float scanline = rand(float2(floor(uv.y * 100), floor(_Time.y * 20)));
                float scan = step(0.92, scanline) * _Intensity;

                float shift = _Intensity * 0.012;
                float r = tex2D(_MainTex, uv + float2(shift, 0)).r;
                float b = tex2D(_MainTex, uv - float2(shift, 0)).b;

                fixed4 result = col;
                result.r = lerp(col.r, r, _Intensity * 0.7);
                result.b = lerp(col.b, b, _Intensity * 0.7);
                result.rgb += (noise - 0.5) * _Intensity * 0.3;
                result.rgb += scan * 0.4;

                return result;
            }
            ENDCG
        }
    }
}