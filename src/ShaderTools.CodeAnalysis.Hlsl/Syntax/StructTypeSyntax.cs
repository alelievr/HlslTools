using System.Collections.Generic;

namespace ShaderTools.CodeAnalysis.Hlsl.Syntax
{
    public sealed partial class StructTypeSyntax : TypeDefinitionSyntax
    {
        public bool IsClass => Kind == SyntaxKind.ClassType;

        public StructTypeSyntax(SyntaxToken structKeyword, List<AttributeDeclarationSyntaxBase> attributes, SyntaxToken name, BaseListSyntax baseList, SyntaxToken openBraceToken, List<SyntaxNode> members, SyntaxToken closeBraceToken)
            : this(structKeyword.Kind == SyntaxKind.ClassKeyword ? SyntaxKind.ClassType : SyntaxKind.StructType,
                   structKeyword, attributes, name, baseList, openBraceToken, members, closeBraceToken)
        {
        }
    }
}