// Draws the zoomed region of the captured monitor with one full-screen triangle.
// Compiled ahead of time into MirrorShaders.cs by tools/build-shaders.ps1.

cbuffer View : register(b0)
{
    float2 uvOffset;
    float2 uvScale;
    float2 texSize;   // captured image size in pixels
    float2 padding;
};
Texture2D tex : register(t0);
SamplerState smp : register(s0);

struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };

VSOut VS(uint id : SV_VertexID)
{
    VSOut o;
    float2 t = float2((id << 1) & 2, id & 2);
    o.pos = float4(t * float2(2, -2) + float2(-1, 1), 0, 1);
    o.uv = uvOffset + t * uvScale;
    return o;
}

// Sharp scaling: each source pixel is drawn as a solid block, and only the last screen pixel
// at its border blends with the neighbor. Zoomed edges stay crisp without the jagged look of
// plain nearest-neighbor. Where a source pixel covers one screen pixel or less, this is
// ordinary bilinear filtering.
float4 PS(VSOut i) : SV_Target
{
    float2 t = i.uv * texSize;                 // position in source pixels
    float2 scale = max(1.0 / fwidth(t), 1.0);  // screen pixels per source pixel
    float2 c = frac(t) - 0.5;
    float2 r = 0.5 - 0.5 / scale;
    float2 f = (c - clamp(c, -r, r)) * scale + 0.5;
    float2 uv = (floor(t) + f) / texSize;
    return float4(tex.Sample(smp, uv).rgb, 1);
}
