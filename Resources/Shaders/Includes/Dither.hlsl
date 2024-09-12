void Dither_float(float4 In, float2 ScreenPosition, float2 ScreenSize, out float4 Out)
{
    float2 uv = ScreenPosition * ScreenSize;
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 16.0,  9.0 / 16.0,  3.0 / 16.0, 11.0 / 16.0,
        13.0 / 16.0,  5.0 / 16.0, 15.0 / 16.0,  7.0 / 16.0,
        4.0 / 16.0, 12.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
        16.0 / 16.0,  8.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0
    };
    uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
    Out = In * DITHER_THRESHOLDS[index];
}


void Dither_float(float4 In, float2 ScreenPosition, out float4 Out)
{
    float2 uv = ScreenPosition;
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 16.0, 9.0 / 16.0, 3.0 / 16.0, 11.0 / 16.0,
        13.0 / 16.0, 5.0 / 16.0, 15.0 / 16.0, 7.0 / 16.0,
        4.0 / 16.0, 12.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
        16.0 / 16.0, 8.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0
    };
    uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
    Out = In * DITHER_THRESHOLDS[index];
}