using System.Linq;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Diagnostics;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using Xunit;

namespace ShaderTools.CodeAnalysis.Hlsl.Tests.Binding
{
    /// <summary>
    /// Regression tests for specific reported issues, asserting that valid HLSL that previously
    /// produced false-positive errors now binds cleanly.
    /// </summary>
    public class IssueRegressionTests
    {
        private static int ErrorCount(string code)
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(code));

            // The parse must round-trip exactly.
            Assert.Equal(code, syntaxTree.Root.ToFullString());

            var compilation = new CodeAnalysis.Hlsl.Compilation.Compilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel();
            return syntaxTree.GetDiagnostics().Concat(semanticModel.GetDiagnostics())
                .Count(x => x.Severity == DiagnosticSeverity.Error);
        }

        // #262 - 'const' (and other type qualifiers) inside a C-style cast used to be reported as
        // an invalid expression term.
        [Theory]
        [InlineData("(const float4) 0")]
        [InlineData("(const float) 0")]
        [InlineData("(row_major float4x4) 0")]
        [InlineData("(column_major float4x4) 0")]
        [InlineData("(const int2) 0")]
        public void ConstAndModifiersInCastResolve(string castExpression)
        {
            Assert.Equal(0, ErrorCount($"void f() {{ float4 x = {castExpression}; }}"));
        }

        [Fact]
        public void PlainCastStillResolves()
        {
            Assert.Equal(0, ErrorCount("void f() { float4 x = (float4) 0; }"));
        }

        // #226 - a struct/class method body must be able to reference a field regardless of whether
        // the field is declared before or after the method.
        [Fact]
        public void StructFieldDeclaredAfterMethodIsVisible()
        {
            var code =
@"struct S
{
    void M() { x = 1; }
    int x;
};";
            Assert.Equal(0, ErrorCount(code));
        }

        [Fact]
        public void StructFieldDeclaredBeforeMethodIsVisible()
        {
            var code =
@"struct S
{
    int x;
    void M() { x = 1; }
};";
            Assert.Equal(0, ErrorCount(code));
        }

        [Fact]
        public void StructMethodCanUseFieldDeclaredAfterInReturnExpression()
        {
            var code =
@"struct S
{
    float GetValue() { return value * 2; }
    float value;
};";
            Assert.Equal(0, ErrorCount(code));
        }
    }
}
