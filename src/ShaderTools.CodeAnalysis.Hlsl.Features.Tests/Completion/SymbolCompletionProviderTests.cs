using System.Collections.Generic;
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
    public class ScopedCompletionTests
    {
        [Fact]
        public async Task GlobalDeclaration_AboveSourceLocationInFunction_InCompletionList()
        {
            var markup = @"Texture2D InputTexture;
float4 PS(float4 pos : SV_Position) : SV_Target
{
    In$$
    float4 color;
    return color;
}";

            await VerifyItemExistsAsync(markup, "InputTexture");
        }

        [Fact]
        public async Task LocalDeclaration_AboveSourceLocation_SameFunction_InCompletionList()
        {
            var markup = @"float4 PS(float4 pos : SV_Position) : SV_Target
{
    float4 color;
    return co$$;
}";

            await VerifyItemExistsAsync(markup, "color");
        }

        [Fact]
        public async Task GlobalDeclaration_BelowSourcePosition_NotInCompletionList()
        {
            var markup = @"I$$
Texture2D InputTexture;
float4 PS(float4 pos : SV_Position) : SV_Target
{
    float4 color;
    return color;
}";

            await VerifyItemIsAbsentAsync(markup, "InputTexture");
        }


        [Fact]
        public async Task LocalDeclaration_BelowSourceLocation_SameFunction_NotInCompletionList()
        {
            var markup = @"float4 PS(float4 pos : SV_Position) : SV_Target
{
    co$$
    float4 color;
    return color;
}";

            await VerifyItemIsAbsentAsync(markup, "color");
        }

        [Fact]
        public async Task LocalDeclaration_AboveSourceLocation_DifferentScope_NotInCompletionList()
        {
            var markup = @"float4 PS(float4 pos : SV_Position) : SV_Target
{
    float4 color;
    return color;
}

float Dummy(float input)
{
    co$$
}";

            await VerifyItemIsAbsentAsync(markup, "color");
        }

        [Fact]
        public async Task GlobalFunction_AboveSourceLocationInFunction_InCompletionList()
        {
            var markup = @"float MyHelper(float x) { return x; }
float4 PS(float4 pos : SV_Position) : SV_Target
{
    My$$
    return 0;
}";
            await VerifyItemExistsAsync(markup, "MyHelper");
        }

        [Fact]
        public async Task GlobalScope_VariableNotSuggested_PredefinedTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
Texture2$$
float4 PS(float4 pos : SV_Position) : SV_Target
{
    return 0;
}";

            await VerifyItemIsAbsentAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task GlobalScope_AtEndOfFile_VariableNotSuggested_PredefinedTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
Texture2$$";

            await VerifyItemIsAbsentAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task GlobalScope_FunctionNotSuggested()
        {
            var markup = @"float MyHelper(float x) { return x; }
MyHel$$";

            await VerifyItemIsAbsentAsync(markup, "MyHelper");
        }

        [Fact]
        public async Task StructBody_VariableNotSuggested_PredefinedTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
struct MyStruct
{
    Texture2$$
};";

            await VerifyItemIsAbsentAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task CbufferBody_VariableNotSuggested_PredefinedTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
cbuffer Globals
{
    Texture2$$
};";

            await VerifyItemIsAbsentAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task FunctionBody_BothVariableAndPredefinedTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
float4 PS(float4 pos : SV_Position) : SV_Target
{
    Texture2$$
    return 0;
}";

            await VerifyItemExistsAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task ParameterType_PredefinedTypeSuggested()
        {
            var markup = @"float4 PS(Textu$$)
{
    return 0;
}";

            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task GlobalScope_UserDefinedTypeSuggested()
        {
            var markup = @"struct MyStruct { float x; };
MyStr$$";

            await VerifyItemExistsAsync(markup, "MyStruct");
        }

        [Fact]
        public async Task GlobalInitializer_VariableStillSuggested()
        {
            var markup = @"static float _MyGlobal = 2;
static float _Other = _MyGlo$$;";

            await VerifyItemExistsAsync(markup, "_MyGlobal");
        }

        [Fact]
        public async Task FunctionBody_NextLineIsCall_FunctionAndTypeSuggested()
        {
            // The greedy parse glues "Wav" and the call on the next line into a "Wav WaveIntrinsics"
            // variable declaration, which used to restrict completion to type names only.
            var markup = @"Texture2D<uint> _Texture2DBindless;
void WaveIntrinsics(float a, int b) { }
float4 PS(float4 pos : SV_Position) : SV_Target
{
    Wav$$
    WaveIntrinsics(pos.x, (int) pos.y);
    return 0;
}";

            await VerifyItemExistsAsync(markup, "WaveIntrinsics");
            await VerifyItemExistsAsync(markup, "WaveActiveSum");
            await VerifyItemExistsAsync(markup, "pos");
            await VerifyItemExistsAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task FunctionBody_NextLineIsAssignment_FunctionAndTypeSuggested()
        {
            // "Wav color" parses as a complete variable declaration with an initializer, so the
            // position is ambiguous - both a type name and an expression are valid here.
            var markup = @"Texture2D<uint> _Texture2DBindless;
void WaveIntrinsics(float a, int b) { }
float4 PS(float4 pos : SV_Position) : SV_Target
{
    float4 color;
    Wav$$
    color = float4(0, 0, 0, 1);
    return color;
}";

            await VerifyItemExistsAsync(markup, "WaveIntrinsics");
            await VerifyItemExistsAsync(markup, "color");
            await VerifyItemExistsAsync(markup, "_Texture2DBindless");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task FunctionBody_NextLineIsMemberAccess_FunctionAndTypeSuggested()
        {
            var markup = @"Texture2D<uint> _Texture2DBindless;
void WaveIntrinsics(float a, int b) { }
float4 PS(float4 pos : SV_Position) : SV_Target
{
    float4 color;
    Wav$$
    color.x = 1;
    return color;
}";

            await VerifyItemExistsAsync(markup, "WaveIntrinsics");
            await VerifyItemExistsAsync(markup, "color");
            await VerifyItemExistsAsync(markup, "Texture2D");
        }

        [Fact]
        public async Task VariableDeclaration_TypeAndDeclaratorOnSameLine_VariableNotSuggested()
        {
            // A declarator on the same line as the type is a real declaration, so only type names
            // make sense in the type position.
            var markup = @"Texture2D<uint> _Texture2DBindless;
struct MyStruct { float x; };
float4 PS(float4 pos : SV_Position) : SV_Target
{
    MyStr$$ myLocal;
    return 0;
}";

            await VerifyItemExistsAsync(markup, "MyStruct");
            await VerifyItemIsAbsentAsync(markup, "_Texture2DBindless");
            await VerifyItemIsAbsentAsync(markup, "pos");
        }

        [Fact]
        public async Task RayPayloadStructMembers_InCompletionList()
        {
            // The [raypayload] attribute and the read()/write() access qualifiers used to stop
            // the struct from parsing, so the payload type never bound and member access on it
            // offered nothing.
            var markup = @"struct [raypayload] RayPayload
{
    float3 color : read(caller, closesthit) : write(caller, closesthit, miss);
    uint32_t depth: read(caller, closesthit) : write(caller, closesthit);
};

void closest_hit(inout RayPayload payload)
{
    payload.$$
}";

            await VerifyItemExistsAsync(markup, "color");
            await VerifyItemExistsAsync(markup, "depth");
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

            var completionProvider = new SymbolCompletionProvider();

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
