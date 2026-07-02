// =================================================================================================
// HLSL Tools - manual feature test.
//
// Open this file in VS Code or Visual Studio. With a working HLSL Tools build there should be
// NO red (error) squiggles anywhere in this file. It exercises everything that was recently added:
//
//   * #pragma once + #include          (Common.hlsli is included twice on purpose)
//   * modern scalar types              (uint64_t / int64_t / 16-bit types - DXC -enable-16bit-types)
//   * Shader Model 6.0 wave intrinsics (WaveActiveSum, WaveBallot, ...)
//   * Shader Model 6.5 wave intrinsics (WaveMatch, WaveMultiPrefix*)
//   * full DirectX Raytracing (DXR)    (TraceRay/ReportHit/CallShader with user payloads,
//                                       RayQuery<> inline raytracing, RAY_FLAG_*/COMMITTED_*/...,
//                                       and the [shader("...")] entry attribute)
//   * HLSL 2021 select() intrinsic     (#223)
//   * const/row_major/... in casts     (#262)
//   * struct field used before decl    (#226)
//   * variadic macros + __VA_ARGS__    (#224)
// =================================================================================================

#include "Common.hlsli"
#include "Common.hlsli"   // included a second time on purpose: #pragma once must dedupe it.

// Variadic function-like macro (#224): __VA_ARGS__ expands to the extra arguments.
#define MAKE_FLOAT4(...) float4(__VA_ARGS__)

RWTexture2D<unorm float4> g_output : register(u0);

//--------------------------------------------------------------------------------------------------
// Modern scalar types (DXC -enable-16bit-types).
//--------------------------------------------------------------------------------------------------
void ScalarTypes()
{
    uint32_t   u32    = 0;
    uint64_t   big    = 0;
    int64_t    sbig   = 0;
    uint16_t   small  = 0;
    int16_t    ssmall = 0;
    float16_t  h       = 0;

    uint64_t2  big2   = (uint64_t2) 0;
    float16_t4 h4     = (float16_t4) 0;

    float angle = TWO_PI;   // from Common.hlsli (the #define)
}

//--------------------------------------------------------------------------------------------------
// HLSL 2021 select() intrinsic (#223), type qualifiers in casts (#262), variadic macros (#224).
//--------------------------------------------------------------------------------------------------
float4 ModernSyntax(bool cond, bool4 mask, float4 a, float4 b)
{
    // select() - function form of the ternary operator, component-wise.
    float  s = select(cond, a.x, b.x);
    float4 v = select(mask, a, b);

    // Type qualifiers inside a C-style cast.
    float4   c = (const float4) a;
    float4x4 m = (row_major float4x4) 0;

    // Variadic macro invocation.
    float4 made = MAKE_FLOAT4(s, v.y, c.z, m[0].w);
    return made;
}

//--------------------------------------------------------------------------------------------------
// Struct method referencing a field declared *after* it (#226).
//--------------------------------------------------------------------------------------------------
struct Accumulator
{
    float Sum()          { return _total; }   // _total is declared below, but still resolves.
    void  Add(float x)   { _total += x; }
    float _total;
};

//--------------------------------------------------------------------------------------------------
// Wave intrinsics - SM6.0 (already supported) and SM6.5 (newly added).
//--------------------------------------------------------------------------------------------------
float WaveIntrinsics(float value, int ivalue)
{
    uint lane  = WaveGetLaneIndex();
    uint count = WaveGetLaneCount();
    bool first = WaveIsFirstLane();

    float sum  = WaveActiveSum(value);
    float prod = WaveActiveProduct(value);
    uint4 ball = WaveActiveBallot(value > 0.0);
    float read = WaveReadLaneAt(value, 0);

    // Shader Model 6.5 additions:
    uint4 match = WaveMatch(value);
    uint4 mask  = uint4(0xFFFFFFFF, 0, 0, 0);
    float msum  = WaveMultiPrefixSum(value, mask);
    float mprod = WaveMultiPrefixProduct(value, mask);
    int   mand  = WaveMultiPrefixBitAnd(ivalue, mask);
    int   mor   = WaveMultiPrefixBitOr(ivalue, mask);
    int   mxor  = WaveMultiPrefixBitXor(ivalue, mask);
    uint  mbits = WaveMultiPrefixCountBits(value > 0.0, mask);

    // Quad intrinsics:
    float qx = QuadReadAcrossX(value);

    return sum + prod + read + msum + mprod + qx;
}

//--------------------------------------------------------------------------------------------------
// DirectX Raytracing (DXR 1.0): TraceRay / ReportHit / CallShader with user-defined structs,
// the predefined RAY_FLAG_* / HIT_KIND_* constants, and the [shader("...")] entry attribute.
//--------------------------------------------------------------------------------------------------
RaytracingAccelerationStructure Scene;

[shader("raygeneration")]
void RayGen()
{
    RayDesc ray;
    ray.Origin    = float3(0, 0, 0);
    ray.Direction = float3(0, 0, 1);
    ray.TMin      = 0.0;
    ray.TMax      = 1e30;

    RayPayload payload;
    payload.color = float4(0, 0, 0, 0);
    payload.hitT  = -1;

    uint flags = RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
    TraceRay(Scene, flags, 0xFF, 0, 1, 0, ray, payload);

    CallableData cd;
    cd.value  = 1;
    cd.weight = 0.5;
    CallShader(0, cd);

    WaveIntrinsics(payload.hitT, (int)payload.color.r);
}

[shader("intersection")]
void Intersection()
{
    MyAttributes attr;
    attr.barycentrics = float2(0.25, 0.5);
    if (ReportHit(1.0, HIT_KIND_TRIANGLE_FRONT_FACE, attr))
        return;
}

[shader("closesthit")]
void ClosestHit(inout RayPayload payload, MyAttributes attr)
{
    payload.color = float4(attr.barycentrics, 0, 1);
    payload.hitT  = RayTCurrent();

    // A few DXR system-value intrinsics:
    uint   prim = PrimitiveIndex();
    uint   inst = InstanceID();
    float3 wo   = WorldRayOrigin();
    float3 wd   = WorldRayDirection();
}

[shader("miss")]
void Miss(inout RayPayload payload)
{
    payload.color = float4(0.1, 0.2, 0.4, 1);
}

//--------------------------------------------------------------------------------------------------
// Inline raytracing (DXR 1.1): the RayQuery<RAY_FLAGS> object and its methods, plus the
// COMMITTED_* / CANDIDATE_* status constants.
//--------------------------------------------------------------------------------------------------
[shader("compute")]
[numthreads(8, 8, 1)]
void InlineRaytracingCS(uint3 dtid : SV_DispatchThreadID)
{
    RayDesc ray;
    ray.Origin    = float3(dtid.x, dtid.y, 0);
    ray.Direction = float3(0, 0, 1);
    ray.TMin      = 0;
    ray.TMax      = 1000;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_CULL_NON_OPAQUE> q;
    q.TraceRayInline(Scene, RAY_FLAG_NONE, 0xFF, ray);

    while (q.Proceed())
    {
        if (q.CandidateType() == CANDIDATE_NON_OPAQUE_TRIANGLE)
            q.CommitNonOpaqueTriangleHit();
        else if (q.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
            q.CommitProceduralPrimitiveHit(1.0);
    }

    if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        float  t    = q.CommittedRayT();
        uint   prim = q.CommittedPrimitiveIndex();
        float2 bary = q.CommittedTriangleBarycentrics();
        float3 wo   = q.WorldRayOrigin();
    }
}
