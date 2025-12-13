// Duotone shader for Celeste/FNA
// Maps grayscale to two colors: dark (GridColor) and light (BoxColor)
sampler2D TextureSampler : register(s0);

// Dark color (for shadows/black) - GridColor #3a3c4d
float3 DarkColor = float3(0.227, 0.235, 0.302);

// Light color (for highlights/white) - BoxColor #eec39a
float3 LightColor = float3(0.933, 0.765, 0.604);

float4 DuotonePixelShader(float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(TextureSampler, texCoord);
    
    // Skip fully transparent pixels
    if (color.a < 0.01)
        return float4(0, 0, 0, 0);
    
    // Calculate luminance
    float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
    
    // Lerp between dark and light based on luminance
    float3 result = lerp(DarkColor, LightColor, luminance);
    
    // Premultiply alpha for proper blending
    return float4(result * color.a, color.a);
}

technique Grayscale
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 DuotonePixelShader();
    }
}
