Shader "Hidden/MornUI/Text"
{
    Properties
    {
        _MainTex ("Base", 2D) = "black" {}
        _TextTex ("Text", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _TextTex;
            float4 _TextColor; // RGBA (linear)
            float4 _TextRect; // x, y, w, h in pixels (destination)
            float4 _TextSrcRect; // x, y, w, h in source texture (normalized 0-1 after division)
            float2 _TexSize;
            float _TextGamma;
            float _LumThreshold;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pixelPos = i.uv * _TexSize;
                pixelPos.y = _TexSize.y - pixelPos.y;
                fixed4 existing = tex2D(_MainTex, i.uv);

                // Check if pixel is in text destination rect
                if (pixelPos.x < _TextRect.x || pixelPos.x >= _TextRect.x + _TextRect.z ||
                    pixelPos.y < _TextRect.y || pixelPos.y >= _TextRect.y + _TextRect.w)
                {
                    return existing;
                }

                // Map to source texture UV (flip Y for texture sampling)
                float2 localPos = (pixelPos - _TextRect.xy) / _TextRect.zw;
                localPos.y = 1.0 - localPos.y;
                float2 srcUV = _TextSrcRect.xy + localPos * _TextSrcRect.zw;
                fixed4 texel = tex2D(_TextTex, srcUV);

                float lum = max(texel.r, max(texel.g, texel.b));
                if (lum < _LumThreshold) return existing;

                float normalizedLum = lum;
                float adjustedLum = pow(normalizedLum, _TextGamma);
                float alpha = adjustedLum * _TextColor.a;

                fixed4 src = fixed4(_TextColor.rgb, alpha);
                float a = src.a;
                return fixed4(
                    existing.rgb * (1.0 - a) + src.rgb * a,
                    existing.a * (1.0 - a) + a
                );
            }
            ENDCG
        }
    }
}
