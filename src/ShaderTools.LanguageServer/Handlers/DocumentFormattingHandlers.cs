using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis;
using ShaderTools.CodeAnalysis.Formatting;
using ShaderTools.CodeAnalysis.Shared.Extensions;
using LspFormattingOptions = OmniSharp.Extensions.LanguageServer.Protocol.Models.FormattingOptions;
using CodeAnalysisFormattingOptions = ShaderTools.CodeAnalysis.Formatting.FormattingOptions;
using OptionSet = ShaderTools.CodeAnalysis.Options.OptionSet;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class DocumentFormattingHandler : IDocumentFormattingHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly DocumentFormattingRegistrationOptions _registrationOptions;

        public DocumentFormattingHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new DocumentFormattingRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public Task<TextEditContainer> Handle(DocumentFormattingParams request, CancellationToken token)
        {
            var document = _workspace.GetDocument(request.TextDocument.Uri);

            return FormattingHelpers.GetFormattingEdits(
                document,
                new TextSpan(0, document.SourceText.Length),
                request.Options,
                token);
        }

        DocumentFormattingRegistrationOptions IRegistration<DocumentFormattingRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<DocumentFormattingCapability>.SetCapability(DocumentFormattingCapability capability) { }
    }

    internal sealed class DocumentRangeFormattingHandler : IDocumentRangeFormattingHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly DocumentRangeFormattingRegistrationOptions _registrationOptions;

        public DocumentRangeFormattingHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new DocumentRangeFormattingRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken token)
        {
            var document = _workspace.GetDocument(request.TextDocument.Uri);

            var sourceText = document.SourceText;
            var start = sourceText.Lines.GetPosition(new LinePosition(request.Range.Start.Line, request.Range.Start.Character));
            var end = sourceText.Lines.GetPosition(new LinePosition(request.Range.End.Line, request.Range.End.Character));

            return FormattingHelpers.GetFormattingEdits(
                document,
                TextSpan.FromBounds(start, end),
                request.Options,
                token);
        }

        DocumentRangeFormattingRegistrationOptions IRegistration<DocumentRangeFormattingRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<DocumentRangeFormattingCapability>.SetCapability(DocumentRangeFormattingCapability capability) { }
    }

    internal static class FormattingHelpers
    {
        public static async Task<TextEditContainer> GetFormattingEdits(
            Document document,
            TextSpan span,
            LspFormattingOptions requestOptions,
            CancellationToken token)
        {
            var formattingService = document.GetLanguageService<ISyntaxFormattingService>();
            if (formattingService == null)
            {
                return new TextEditContainer();
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(token);

            // Honor the tab settings the editor sent with the request.
            OptionSet options = await document.GetOptionsAsync(token);
            if (requestOptions != null)
            {
                options = options
                    .WithChangedOption(CodeAnalysisFormattingOptions.UseTabs, document.Language, !requestOptions.InsertSpaces)
                    .WithChangedOption(CodeAnalysisFormattingOptions.TabSize, document.Language, (int) requestOptions.TabSize)
                    .WithChangedOption(CodeAnalysisFormattingOptions.IndentationSize, document.Language, (int) requestOptions.TabSize);
            }

            var formattingResult = await formattingService.FormatAsync(
                syntaxTree,
                syntaxTree.Root,
                new[] { span },
                options,
                token);

            var edits = formattingResult.GetTextChanges(token)
                .Select(x => new TextEdit
                {
                    Range = Helpers.ToRange(document.SourceText, x.Span),
                    NewText = x.NewText
                })
                .ToArray();

            return edits;
        }
    }
}
