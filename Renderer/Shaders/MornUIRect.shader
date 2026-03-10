Shader "Hidden/MornUI/Rect"
{
    Properties
    {
        _MainTex ("Base", 2D) = "black" {}
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

            // Rect params (set per draw call)
            float4 _BgColor;
            float4 _Rect; // x, y, w, h in pixels
            float4 _Radii; // TL, TR, BR, BL
            float4 _BorderWidths; // T, R, B, L
            float4 _BorderColorTop;
            float4 _BorderColorRight;
            float4 _BorderColorBottom;
            float4 _BorderColorLeft;
            float2 _TexSize; // texture width, height

            float roundedRectSDF(float2 p, float2 halfSize, float4 radii)
            {
                // Select radius based on quadrant
                float r = (p.x > 0.0)
                    ? ((p.y > 0.0) ? radii.z : radii.y)  // BR or TR
                    : ((p.y > 0.0) ? radii.w : radii.x);  // BL or TL
                float2 q = abs(p) - halfSize + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Pixel position in texture space (top-left origin)
                float2 pixelPos = i.uv * _TexSize;
                pixelPos.y = _TexSize.y - pixelPos.y;

                // Check if pixel is inside rect bounds (with 1px margin for AA)
                float2 rectMin = _Rect.xy - 1.0;
                float2 rectMax = _Rect.xy + _Rect.zw + 1.0;
                if (pixelPos.x < rectMin.x || pixelPos.x > rectMax.x ||
                    pixelPos.y < rectMin.y || pixelPos.y > rectMax.y)
                {
                    return tex2D(_MainTex, i.uv);
                }

                // Position relative to rect center
                float2 center = _Rect.xy + _Rect.zw * 0.5;
                float2 p = pixelPos - center;
                float2 halfSize = _Rect.zw * 0.5;

                // Outer SDF (border-box)
                float outerDist = roundedRectSDF(p, halfSize, _Radii);

                // Inner radii (reduced by border width)
                float4 innerRadii = max(_Radii - float4(
                    max(_BorderWidths.w, _BorderWidths.x), // TL: left, top
                    max(_BorderWidths.y, _BorderWidths.x), // TR: right, top
                    max(_BorderWidths.y, _BorderWidths.z), // BR: right, bottom
                    max(_BorderWidths.w, _BorderWidths.z)  // BL: left, bottom
                ), 0.0);

                // Inner half-size (padding-box)
                float2 innerHalfSize = halfSize - float2(
                    (_BorderWidths.y + _BorderWidths.w) * 0.5,
                    (_BorderWidths.x + _BorderWidths.z) * 0.5
                );
                float2 innerCenter = float2(
                    (_BorderWidths.w - _BorderWidths.y) * 0.5,
                    (_BorderWidths.x - _BorderWidths.z) * 0.5
                );
                float innerDist = roundedRectSDF(p - innerCenter, max(innerHalfSize, 0.0), innerRadii);

                // Anti-aliased coverage
                float outerAlpha = saturate(0.5 - outerDist);
                float innerAlpha = saturate(0.5 - innerDist);

                // Existing pixel
                fixed4 existing = tex2D(_MainTex, i.uv);

                if (outerAlpha <= 0.0)
                    return existing;

                // Determine color
                fixed4 color;
                if (innerAlpha > 0.0)
                {
                    // Inside: background color
                    color = _BgColor;
                    color.a *= innerAlpha;
                }
                else
                {
                    // Border zone: pick color based on closest side
                    float2 rel = pixelPos - _Rect.xy;
                    float distTop = rel.y;
                    float distBottom = _Rect.w - rel.y;
                    float distLeft = rel.x;
                    float distRight = _Rect.z - rel.x;
                    float minDist = min(min(distTop, distBottom), min(distLeft, distRight));

                    if (minDist == distTop) color = _BorderColorTop;
                    else if (minDist == distBottom) color = _BorderColorBottom;
                    else if (minDist == distLeft) color = _BorderColorLeft;
                    else color = _BorderColorRight;

                    float borderAlpha = outerAlpha * (1.0 - innerAlpha);
                    color.a *= borderAlpha;
                }

                // Alpha blend with existing
                float a = color.a;
                return fixed4(
                    existing.rgb * (1.0 - a) + color.rgb * a,
                    existing.a * (1.0 - a) + a
                );
            }
            ENDCG
        }
    }
}
