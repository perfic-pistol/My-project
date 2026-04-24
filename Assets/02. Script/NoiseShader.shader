Shader "Custom/NoiseShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Noise Intensity", Range(0,1)) = 0
        _NoiseType ("Noise Type (0=Color 1=Grain)", Range(0,1)) = 0
        _GrainSize ("Grain Size", Range(10, 300)) = 150
        _CenterClear ("Center Clear Size", Range(0,1)) = 0.4
        _EdgeSoftness ("Edge Softness", Range(0.01,1)) = 0.3
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
            float _NoiseType;
            float _GrainSize;
            float _CenterClear;
            float _EdgeSoftness;

            float rand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                if (_Intensity < 0.01) return col;

                float2 center = uv - 0.5;
                center.x *= _ScreenParams.x / _ScreenParams.y;
                float distFromCenter = length(center);

                float mask = smoothstep(_CenterClear, _CenterClear + _EdgeSoftness, distFromCenter);

                float noiseIntensity = _Intensity * mask;

                if (noiseIntensity < 0.01) return col;

                float t = _Time.y * 30;
                float2 seed = uv * _GrainSize;

                if (_NoiseType < 0.5)
                {
                    float r = rand(seed + float2(t, 0));
                    float g = rand(seed + float2(0, t));
                    float b = rand(seed + float2(t, t));
                    fixed4 noiseColor = fixed4(r, g, b, 1);
                    return lerp(col, noiseColor, noiseIntensity * 0.85);
                }
                else
                {
                    float grain = rand(seed + float2(floor(t), 0));
                    float threshold = 1.0 - (noiseIntensity * 0.4);
                    float d = step(threshold, grain);
                    fixed4 result = col;
                    result.rgb -= d * noiseIntensity * 0.6;
                    result.rgb = clamp(result.rgb, 0, 1);
                    return result;
                }
            }
            ENDCG
        }
    }
}