using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Completion;
using ShaderTools.CodeAnalysis.Hlsl.Completion.Providers;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using ShaderTools.CodeAnalysis.Options;
using ShaderTools.CodeAnalysis.Shared.Extensions;
using ShaderTools.CodeAnalysis.Syntax;
using ShaderTools.CodeAnalysis.Text;

namespace ShaderTools.CodeAnalysis.Hlsl.Completion.CompletionProviders
{
    /// <summary>
    /// Completes preprocessor macros: everything #define'd (in the file and its includes)
    /// before the caret, plus macros predefined via shadertoolsconfig.json. Macros are pure
    /// text substitution, so they're offered in any code context - including inside
    /// #if / #elif conditions and #undef, where they're the only thing that can appear.
    /// </summary>
    internal sealed class MacroCompletionProvider : CompletionProvider
    {
        internal override bool IsInsertionTrigger(SourceText text, int insertedCharacterPosition, OptionSet options)
        {
            return CompletionUtilities.IsTriggerCharacter(text, insertedCharacterPosition, options);
        }

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            var syntaxTree = (SyntaxTree) await context.Document.GetSyntaxTreeAsync(context.CancellationToken).ConfigureAwait(false);

            var position = syntaxTree.MapRootFilePosition(context.Position);
            var root = (SyntaxNode) syntaxTree.Root;

            if (root.InComment(position) || root.InLiteral(position))
                return;

            if (syntaxTree.PossiblyInUserGivenName(position))
                return;

            // After a '.' only members of the expression on the left are useful. A macro can
            // technically expand to a member name there ("#define RGB rgb" then "color.RGB"),
            // but that's vanishingly rare next to the cost: every member list in a real project
            // would be buried under hundreds of macros.
            if (syntaxTree.DefinitelyInMemberAccessName(position))
                return;

            var collector = new MacroDirectiveCollector();
            collector.Visit(root);

            // Don't suggest existing macros while a NEW macro name is being typed
            // ("#define Nam|"). Directives are trivia, so this can't be detected via
            // FindTokenOnLeft - check the caret against the collected names instead.
            // (The macro body is fine - macros can reference other macros there.)
            foreach (var directive in collector.Directives)
            {
                if (directive is DefineDirectiveTriviaSyntax define &&
                    define.MacroName != null &&
                    define.MacroName.FileSpan.File.IsRootFile &&
                    define.MacroName.SourceRange.ContainsOrTouches(position))
                {
                    return;
                }
            }

            // Replay the directives in source (expansion) order, so #undef removes earlier
            // #defines, and anything after the caret doesn't count. SourceRange positions of
            // include-file tokens are interleaved into the root file's, so a single comparison
            // handles both.
            var macros = new Dictionary<string, DefineDirectiveTriviaSyntax>();

            foreach (var directive in collector.Directives)
            {
                switch (directive)
                {
                    case DefineDirectiveTriviaSyntax defineDirective:
                        if (defineDirective.IsActive &&
                            defineDirective.MacroName != null &&
                            defineDirective.SourceRange.End < position)
                        {
                            macros[defineDirective.MacroName.Text] = defineDirective;
                        }
                        break;

                    case UndefDirectiveTriviaSyntax undefDirective:
                        if (undefDirective.IsActive &&
                            undefDirective.Name != null &&
                            undefDirective.SourceRange.End < position)
                        {
                            macros.Remove(undefDirective.Name.Text);
                        }
                        break;
                }
            }

            foreach (var macro in macros.Values)
            {
                context.AddItem(CommonCompletionItem.Create(
                    macro.MacroName.Text,
                    Glyph.Macro,
                    GetDescription(macro).ToSymbolMarkupTokens()));
            }

            // Macros predefined via shadertoolsconfig.json don't appear as #define directives.
            if (syntaxTree.Options is HlslParseOptions parseOptions)
            {
                foreach (var kvp in parseOptions.PreprocessorDefines)
                {
                    if (kvp.Key == "__INTELLISENSE__" || macros.ContainsKey(kvp.Key))
                        continue;

                    context.AddItem(CommonCompletionItem.Create(
                        kvp.Key,
                        Glyph.Macro,
                        $"#define {kvp.Key} {kvp.Value} (defined in shadertoolsconfig.json)".ToSymbolMarkupTokens()));
                }
            }
        }

        private static string GetDescription(DefineDirectiveTriviaSyntax macro)
        {
            var parameterList = string.Empty;
            if (macro is FunctionLikeDefineDirectiveTriviaSyntax functionLike && functionLike.Parameters != null)
            {
                parameterList = "(" + string.Join(", ", functionLike.Parameters.Parameters.Select(x => x.Text)) + ")";
            }

            var body = string.Join(" ", macro.MacroBody.Select(x => x.Text));

            var description = $"#define {macro.MacroName.Text}{parameterList} {body}".TrimEnd();

            const int maxLength = 120;
            if (description.Length > maxLength)
                description = description.Substring(0, maxLength) + "...";

            return description;
        }

        private sealed class MacroDirectiveCollector : SyntaxWalker
        {
            public List<DirectiveTriviaSyntax> Directives { get; } = new List<DirectiveTriviaSyntax>();

            public override void VisitSyntaxToken(SyntaxToken node)
            {
                foreach (var trivia in node.LeadingTrivia)
                    Visit(trivia);

                foreach (var trivia in node.TrailingTrivia)
                    Visit(trivia);

                base.VisitSyntaxToken(node);
            }

            public override void VisitObjectLikeDefineDirectiveTrivia(ObjectLikeDefineDirectiveTriviaSyntax node)
            {
                Directives.Add(node);
                base.VisitObjectLikeDefineDirectiveTrivia(node);
            }

            public override void VisitFunctionLikeDefineDirectiveTrivia(FunctionLikeDefineDirectiveTriviaSyntax node)
            {
                Directives.Add(node);
                base.VisitFunctionLikeDefineDirectiveTrivia(node);
            }

            public override void VisitUndefDirectiveTrivia(UndefDirectiveTriviaSyntax node)
            {
                Directives.Add(node);
                base.VisitUndefDirectiveTrivia(node);
            }
        }
    }
}
