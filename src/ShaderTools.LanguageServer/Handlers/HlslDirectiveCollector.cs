using System.Collections.Generic;
using System.Linq;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;

namespace ShaderTools.LanguageServer.Handlers
{
    /// <summary>
    /// Collects preprocessor-related trivia from an HLSL syntax tree: #include directives,
    /// conditional (#if / #ifdef / #ifndef / #elif) directives, and text disabled by
    /// inactive preprocessor branches.
    /// </summary>
    internal sealed class HlslDirectiveCollector : SyntaxWalker
    {
        public List<IncludeDirectiveTriviaSyntax> Includes { get; } = new List<IncludeDirectiveTriviaSyntax>();
        public List<BranchingDirectiveTriviaSyntax> Conditionals { get; } = new List<BranchingDirectiveTriviaSyntax>();
        public List<SyntaxTrivia> DisabledText { get; } = new List<SyntaxTrivia>();

        public static HlslDirectiveCollector Collect(SyntaxNode root)
        {
            var collector = new HlslDirectiveCollector();
            collector.Visit(root);
            return collector;
        }

        /// <summary>
        /// The macro names a conditional directive depends on: the single name for
        /// #ifdef / #ifndef, or the identifiers referenced by a #if / #elif condition.
        /// </summary>
        public static IEnumerable<string> GetReferencedMacroNames(BranchingDirectiveTriviaSyntax directive)
        {
            switch (directive)
            {
                case IfDefDirectiveTriviaSyntax ifDef when ifDef.Name != null:
                    return new[] { ifDef.Name.Text };

                case IfNDefDirectiveTriviaSyntax ifNDef when ifNDef.Name != null:
                    return new[] { ifNDef.Name.Text };

                case ConditionalDirectiveTriviaSyntax conditional when conditional.Condition != null:
                    var identifierCollector = new IdentifierCollector();
                    identifierCollector.Visit(conditional.Condition);

                    return identifierCollector.Identifiers
                        .Where(x => x != "defined")
                        .Distinct()
                        .Take(3);

                default:
                    return Enumerable.Empty<string>();
            }
        }

        private sealed class IdentifierCollector : SyntaxWalker
        {
            public List<string> Identifiers { get; } = new List<string>();

            public override void VisitSyntaxToken(SyntaxToken node)
            {
                if (node.Kind == SyntaxKind.IdentifierToken && !string.IsNullOrEmpty(node.Text))
                    Identifiers.Add(node.Text);

                base.VisitSyntaxToken(node);
            }
        }

        public override void VisitSyntaxToken(SyntaxToken node)
        {
            foreach (var trivia in node.LeadingTrivia)
                Visit(trivia);

            foreach (var trivia in node.TrailingTrivia)
                Visit(trivia);

            base.VisitSyntaxToken(node);
        }

        public override void VisitSyntaxTrivia(SyntaxTrivia node)
        {
            if (node.Kind == SyntaxKind.DisabledTextTrivia)
                DisabledText.Add(node);

            base.VisitSyntaxTrivia(node);
        }

        public override void VisitIncludeDirectiveTrivia(IncludeDirectiveTriviaSyntax node)
        {
            Includes.Add(node);
            base.VisitIncludeDirectiveTrivia(node);
        }

        public override void VisitIfDirectiveTrivia(IfDirectiveTriviaSyntax node)
        {
            Conditionals.Add(node);
            base.VisitIfDirectiveTrivia(node);
        }

        public override void VisitIfDefDirectiveTrivia(IfDefDirectiveTriviaSyntax node)
        {
            Conditionals.Add(node);
            base.VisitIfDefDirectiveTrivia(node);
        }

        public override void VisitIfNDefDirectiveTrivia(IfNDefDirectiveTriviaSyntax node)
        {
            Conditionals.Add(node);
            base.VisitIfNDefDirectiveTrivia(node);
        }

        public override void VisitElifDirectiveTrivia(ElifDirectiveTriviaSyntax node)
        {
            Conditionals.Add(node);
            base.VisitElifDirectiveTrivia(node);
        }
    }
}
