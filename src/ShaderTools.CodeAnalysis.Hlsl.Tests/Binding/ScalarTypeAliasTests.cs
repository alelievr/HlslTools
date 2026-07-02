using System.Linq;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Diagnostics;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using Xunit;

namespace ShaderTools.CodeAnalysis.Hlsl.Tests.Binding
{
    public class ScalarTypeAliasTests
    {
        private static int ErrorCount(string code)
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(code));
            var compilation = new CodeAnalysis.Hlsl.Compilation.Compilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel();
            return syntaxTree.GetDiagnostics().Concat(semanticModel.GetDiagnostics())
                .Count(x => x.Severity == DiagnosticSeverity.Error);
        }

        [Theory]
        [InlineData("uint32_t")]
        [InlineData("int32_t")]
        [InlineData("float32_t")]
        [InlineData("uint32_t4")]
        [InlineData("int32_t2")]
        [InlineData("float32_t3")]
        [InlineData("float32_t4x4")]
        [InlineData("uint32_t2x2")]
        public void ThirtyTwoBitAliasTypesResolve(string type)
        {
            Assert.Equal(0, ErrorCount($"void f() {{ {type} x = ({type}) 0; }}"));
        }

        [Theory]
        [InlineData("uint32_t", "uint")]
        [InlineData("int32_t", "int")]
        [InlineData("float32_t", "float")]
        [InlineData("uint32_t4", "uint4")]
        [InlineData("float32_t3", "float3")]
        public void ThirtyTwoBitAliasesAreInterchangeableWithBaseType(string aliasType, string baseType)
        {
            // Assigning in both directions with no conversion error proves the alias resolves to the
            // very same type as its base (uint32_t == uint, etc.).
            Assert.Equal(0, ErrorCount($"void f() {{ {aliasType} a = ({aliasType}) 0; {baseType} b = a; {aliasType} c = b; }}"));
        }
    }
}
