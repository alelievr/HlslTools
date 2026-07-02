using System.Collections.Generic;
using System.Collections.Immutable;
using ShaderTools.CodeAnalysis.Symbols;

namespace ShaderTools.CodeAnalysis.Hlsl.Symbols
{
    /// <summary>
    /// Predefined global constants that are available in any shader, such as the raytracing
    /// <c>RAY_FLAG_*</c>, <c>COMMITTED_*</c>, <c>CANDIDATE_*</c> and <c>HIT_KIND_*</c> values.
    /// They are modeled as <c>uint</c>-typed variables so they resolve and type-check wherever a
    /// <c>uint</c> is expected.
    /// </summary>
    internal static class IntrinsicConstants
    {
        public static readonly ImmutableArray<VariableSymbol> AllConstants;

        static IntrinsicConstants()
        {
            var allConstants = new List<VariableSymbol>();

            void Add(string name, string documentation)
                => allConstants.Add(new VariableSymbol(SymbolKind.Variable, name, documentation, null, IntrinsicTypes.Uint));

            // Ray flags (RAY_FLAG_*) - passed to TraceRay and RayQuery to control traversal behavior.
            Add("RAY_FLAG_NONE", "No options selected.");
            Add("RAY_FLAG_FORCE_OPAQUE", "All ray-primitive intersections are treated as opaque, regardless of geometry or instance flags. Any hit shaders are not executed.");
            Add("RAY_FLAG_FORCE_NON_OPAQUE", "All ray-primitive intersections are treated as non-opaque, regardless of geometry or instance flags. Any hit shaders are executed.");
            Add("RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH", "The search for hits stops and the closest hit shader is invoked as soon as any hit is accepted.");
            Add("RAY_FLAG_SKIP_CLOSEST_HIT_SHADER", "No closest hit shader is executed for the ray, even when a hit is committed.");
            Add("RAY_FLAG_CULL_BACK_FACING_TRIANGLES", "Back-facing triangles are ignored.");
            Add("RAY_FLAG_CULL_FRONT_FACING_TRIANGLES", "Front-facing triangles are ignored.");
            Add("RAY_FLAG_CULL_OPAQUE", "All opaque primitives are ignored.");
            Add("RAY_FLAG_CULL_NON_OPAQUE", "All non-opaque primitives are ignored.");
            Add("RAY_FLAG_SKIP_TRIANGLES", "Triangle geometry is ignored. (Requires shader model 6.5.)");
            Add("RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES", "Procedural primitives are ignored. (Requires shader model 6.5.)");

            // COMMITTED_STATUS - returned by RayQuery.CommittedStatus().
            Add("COMMITTED_NOTHING", "No hit has been committed yet.");
            Add("COMMITTED_TRIANGLE_HIT", "The committed hit is a triangle hit.");
            Add("COMMITTED_PROCEDURAL_PRIMITIVE_HIT", "The committed hit is a procedural primitive hit.");

            // CANDIDATE_TYPE - returned by RayQuery.CandidateType().
            Add("CANDIDATE_NON_OPAQUE_TRIANGLE", "The current candidate is a non-opaque triangle.");
            Add("CANDIDATE_PROCEDURAL_PRIMITIVE", "The current candidate is a procedural primitive.");

            // HIT_KIND_* - values for the HitKind intrinsic / ReportHit hit kind.
            Add("HIT_KIND_TRIANGLE_FRONT_FACE", "The hit was on the front face of a triangle.");
            Add("HIT_KIND_TRIANGLE_BACK_FACE", "The hit was on the back face of a triangle.");

            AllConstants = allConstants.ToImmutableArray();
        }
    }
}
