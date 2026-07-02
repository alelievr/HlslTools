#pragma once

// Shared definitions. This header is intentionally #include'd twice from NewFeaturesTest.hlsl;
// "#pragma once" must ensure it is only processed once (i.e. no duplicate-definition errors).

#define TWO_PI 6.28318530718

struct RayPayload
{
    float4 color;
    float  hitT;
};

struct MyAttributes
{
    float2 barycentrics;
};

struct CallableData
{
    int   value;
    float weight;
};
