using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Completion;
using ShaderTools.CodeAnalysis.Hlsl.Completion.CompletionProviders;
using ShaderTools.CodeAnalysis.Text;
using ShaderTools.Testing.Workspaces;
using Xunit;

namespace ShaderTools.CodeAnalysis.Hlsl.Tests.Completion
{
    public class MacroCompletionProviderTests
    {
        [Fact]
        public async Task MacroDefinedAbove_InCompletionList()
        {
            var markup = @"#define MY_MACRO 1
float x = MY_MA$$;";

            await VerifyItemExistsAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task MacroDefinedBelow_NotInCompletionList()
        {
            var markup = @"float x = MY_MA$$;
#define MY_MACRO 1";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task UndeffedMacro_NotInCompletionList()
        {
            var markup = @"#define MY_MACRO 1
#undef MY_MACRO
float x = MY_MA$$;";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task FunctionLikeMacro_InCompletionList()
        {
            var markup = @"#define LERP3(a, b, t) lerp(a, b, t)
float x = LER$$;";

            await VerifyItemExistsAsync(markup, "LERP3");
        }

        [Fact]
        public async Task MacroInIfDirectiveCondition_InCompletionList()
        {
            var markup = @"#define FEATURE_A 1
#if FEATU$$
#endif";

            await VerifyItemExistsAsync(markup, "FEATURE_A");
        }

        [Fact]
        public async Task MacroInIfdefDirective_InCompletionList()
        {
            var markup = @"#define FEATURE_A 1
#ifdef FEATU$$
#endif";

            await VerifyItemExistsAsync(markup, "FEATURE_A");
        }

        [Fact]
        public async Task DefineNamePosition_NoMacroCompletion()
        {
            var markup = @"#define MY_MACRO 1
#define MY_MA$$";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task MacroBodyPosition_MacroCompletionAvailable()
        {
            var markup = @"#define MY_MACRO 1
#define OTHER MY_MA$$";

            await VerifyItemExistsAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task MacroInInactiveBranch_NotInCompletionList()
        {
            var markup = @"#ifdef UNDEFINED_THING
#define HIDDEN_MACRO 1
#endif
float x = HIDD$$;";

            await VerifyItemIsAbsentAsync(markup, "HIDDEN_MACRO");
        }

        [Fact]
        public async Task AfterMemberAccessDot_NotInCompletionList()
        {
            // Only members of the expression on the left can appear after a '.'.
            var markup = @"#define MY_MACRO 1
Texture2D MyTexture;
float4 PS() : SV_Target
{
    MyTexture.$$
    return 0;
}";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task WhileTypingMemberName_NotInCompletionList()
        {
            var markup = @"#define MY_MACRO 1
Texture2D MyTexture;
float4 PS() : SV_Target
{
    MyTexture.MY_MA$$
    return 0;
}";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task AfterSwizzleDot_NotInCompletionList()
        {
            var markup = @"#define MY_MACRO 1
float4 PS(float4 color : SV_Target) : SV_Target
{
    float f = color.$$;
    return 0;
}";

            await VerifyItemIsAbsentAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task ArgumentOfMethodCallOnObject_InCompletionList()
        {
            // Inside the call parens we're past the member name, so macros are back in play.
            var markup = @"#define MY_MACRO 1
Texture2D MyTexture;
SamplerState MySampler;
float4 PS(float2 uv : TEXCOORD) : SV_Target
{
    return MyTexture.Sample(MySampler, uv * MY_MA$$);
}";

            await VerifyItemExistsAsync(markup, "MY_MACRO");
        }

        [Fact]
        public async Task ExpressionBeforeTheDot_InCompletionList()
        {
            // The receiver position is an ordinary expression - a macro can legitimately go here.
            var markup = @"#define MY_MACRO 1
float4 PS(float4 color : SV_Target) : SV_Target
{
    float f = MY_MA$$.x;
    return 0;
}";

            await VerifyItemExistsAsync(markup, "MY_MACRO");
        }

        private async Task VerifyItemExistsAsync(string markup, string expectedItem)
        {
            var completionItems = await GetCompletionItems(markup);

            Assert.Contains(completionItems, x => x.DisplayText == expectedItem);
        }

        private async Task VerifyItemIsAbsentAsync(string markup, string expectedItem)
        {
            var completionItems = await GetCompletionItems(markup);

            Assert.DoesNotContain(completionItems, x => x.DisplayText == expectedItem);
        }

        private async Task<IReadOnlyList<CompletionItem>> GetCompletionItems(string testCode)
        {
            var index = testCode.IndexOf("$$");
            testCode = testCode.Remove(index, 2);

            var workspace = new TestWorkspace();

            var document = workspace.OpenDocument(
                DocumentId.CreateNewId(),
                new SourceFile(SourceText.From(testCode)),
                LanguageNames.Hlsl);

            var completionProvider = new MacroCompletionProvider();

            var completionContext = new CompletionContext(
                completionProvider,
                document,
                index,
                new TextSpan(),
                Microsoft.CodeAnalysis.Completion.CompletionTrigger.Invoke,
                await document.GetOptionsAsync(),
                CancellationToken.None);

            await completionProvider.ProvideCompletionsAsync(completionContext);

            return completionContext.Items;
        }
    }
}
