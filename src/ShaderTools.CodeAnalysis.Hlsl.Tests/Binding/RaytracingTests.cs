using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Diagnostics;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using Xunit;

namespace ShaderTools.CodeAnalysis.Hlsl.Tests.Binding
{
    public class RaytracingTests
    {
        private static IReadOnlyList<Diagnostic> GetDiagnostics(string code)
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(code));
            var compilation = new CodeAnalysis.Hlsl.Compilation.Compilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel();
            return syntaxTree.GetDiagnostics().Concat(semanticModel.GetDiagnostics()).ToList();
        }

        private static void AssertNoErrors(string code)
        {
            var errors = GetDiagnostics(code)
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Select(x => x.ToString())
                .ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        [Fact]
        public void TraceRayWithCustomPayloadHasNoErrors()
        {
            AssertNoErrors(@"
struct Payload { float4 color; };

RaytracingAccelerationStructure scene;

[shader(""raygeneration"")]
void RayGen()
{
    RayDesc ray;
    ray.Origin = float3(0, 0, 0);
    ray.Direction = float3(0, 0, 1);
    ray.TMin = 0.0;
    ray.TMax = 1000.0;

    Payload payload;
    payload.color = float4(0, 0, 0, 0);

    TraceRay(scene, RAY_FLAG_NONE, 0xFF, 0, 1, 0, ray, payload);
}");
        }

        [Fact]
        public void ReportHitWithCustomAttributesHasNoErrors()
        {
            AssertNoErrors(@"
struct Attributes { float2 barycentrics; };

[shader(""intersection"")]
void Intersection()
{
    Attributes attr;
    attr.barycentrics = float2(0, 0);
    if (ReportHit(1.0, HIT_KIND_TRIANGLE_FRONT_FACE, attr))
        return;
}");
        }

        [Fact]
        public void CallShaderWithCustomParameterHasNoErrors()
        {
            AssertNoErrors(@"
struct CallableParams { int value; };

[shader(""raygeneration"")]
void RayGen()
{
    CallableParams p;
    p.value = 0;
    CallShader(0, p);
}");
        }

        [Fact]
        public void RayQueryInlineRaytracingHasNoErrors()
        {
            AssertNoErrors(@"
RaytracingAccelerationStructure scene;

[shader(""compute"")]
[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE> q;

    RayDesc ray;
    ray.Origin = float3(0, 0, 0);
    ray.Direction = float3(0, 0, 1);
    ray.TMin = 0;
    ray.TMax = 1000;

    q.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

    while (q.Proceed())
    {
        if (q.CandidateType() == CANDIDATE_NON_OPAQUE_TRIANGLE)
            q.CommitNonOpaqueTriangleHit();
    }

    if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        float t = q.CommittedRayT();
        uint prim = q.CommittedPrimitiveIndex();
        float3 origin = q.WorldRayOrigin();
    }
}");
        }

        [Fact]
        public void RayQueryWithCombinedFlagsParsesAndBinds()
        {
            AssertNoErrors(@"
RaytracingAccelerationStructure scene;

void Trace()
{
    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_CULL_NON_OPAQUE> q;
}");
        }

        [Theory]
        [InlineData("RAY_FLAG_NONE")]
        [InlineData("RAY_FLAG_FORCE_OPAQUE")]
        [InlineData("RAY_FLAG_SKIP_TRIANGLES")]
        [InlineData("COMMITTED_TRIANGLE_HIT")]
        [InlineData("CANDIDATE_PROCEDURAL_PRIMITIVE")]
        [InlineData("HIT_KIND_TRIANGLE_BACK_FACE")]
        public void RaytracingConstantsResolveAsUint(string constant)
        {
            AssertNoErrors($@"
void Use()
{{
    uint x = {constant};
}}");
        }
    }
}
