using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis.SymbolSearch;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class RenameHandler : IRenameHandler
    {
        private static readonly Regex ValidIdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private readonly LanguageServerWorkspace _workspace;
        private readonly RenameRegistrationOptions _registrationOptions;

        public RenameHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new RenameRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken token)
        {
            if (request.NewName == null || !ValidIdentifierRegex.IsMatch(request.NewName))
            {
                return new WorkspaceEdit();
            }

            // RenameParams doesn't derive from TextDocumentPositionParams, so resolve manually.
            var document = _workspace.GetDocument(request.TextDocument.Uri);
            var position = document.SourceText.Lines.GetPosition(new LinePosition(
                request.Position.Line,
                request.Position.Character));

            var semanticModel = await document.GetSemanticModelAsync(token);
            if (semanticModel == null)
            {
                return new WorkspaceEdit();
            }

            var symbolSearchService = document.LanguageServices.GetService<ISymbolSearchService>();
            if (symbolSearchService == null)
            {
                return new WorkspaceEdit();
            }

            var mappedPosition = semanticModel.SyntaxTree.MapRootFilePosition(position);

            var symbolAtPosition = symbolSearchService.FindSymbol(semanticModel, mappedPosition);
            if (symbolAtPosition == null)
            {
                return new WorkspaceEdit();
            }

            var usages = symbolSearchService.FindUsages(semanticModel, symbolAtPosition.Value.Symbol)
                .Where(x => x.Span.File.IsRootFile)
                .ToArray();

            // Don't rename symbols that aren't defined in this file (intrinsics, and symbols
            // defined in #include'd files - we can't edit usages inside those includes yet).
            if (!usages.Any(x => x.Kind == SymbolSpanKind.Definition))
            {
                return new WorkspaceEdit();
            }

            var edits = usages
                .Select(x => new TextEdit
                {
                    Range = Helpers.ToRange(document.SourceText, x.Span.Span),
                    NewText = request.NewName
                })
                .ToArray();

            return new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                {
                    [request.TextDocument.Uri] = edits
                }
            };
        }

        RenameRegistrationOptions IRegistration<RenameRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<RenameCapability>.SetCapability(RenameCapability capability) { }
    }
}
