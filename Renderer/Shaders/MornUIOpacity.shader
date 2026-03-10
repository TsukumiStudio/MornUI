Shader "Hidden/MornUI/Opacity"
{
    Properties
    {
        _MainTex ("Saved (before)", 2D) = "black" {}
        _OverTex ("Rendered (after)", 2D) = "black" {}
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

            sampler2D _MainTex; // saved pixels (before opacity group)
            sampler2D _OverTex; // rendered pixels (after opacity group)
            float _Opacity;
            float4 _OpacityRect; // x, y, w, h in pixels
            float2 _TexSize;

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
                fixed4 saved = tex2D(_MainTex, i.uv);
                fixed4 rendered = tex2D(_OverTex, i.uv);

                // Outside opacity rect: return rendered as-is
                if (pixelPos.x < _OpacityRect.x || pixelPos.x >= _OpacityRect.x + _OpacityRect.z ||
                    pixelPos.y < _OpacityRect.y || pixelPos.y >= _OpacityRect.y + _OpacityRect.w)
                {
                    return rendered;
                }

                // Lerp between saved and rendered
                return lerp(saved, rendered, _Opacity);
            }
            ENDCG
        }
    }
}
