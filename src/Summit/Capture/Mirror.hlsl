// Draws the zoomed region of the captured monitor with one full-screen triangle.
// Compiled ahead of time into MirrorShaders.cs by tools/build-shaders.ps1.

cbuffer View : register(b0) { float2 uvOffset; float2 uvScale; };
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

float4 PS(VSOut i) : SV_Target { return float4(tex.Sample(smp, i.uv).rgb, 1); }
