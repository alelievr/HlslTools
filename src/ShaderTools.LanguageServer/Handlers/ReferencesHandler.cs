using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis.SymbolSearch;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class ReferencesHandler : IReferencesHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly ReferenceRegistrationOptions _registrationOptions;

        public ReferencesHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new ReferenceRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken token)
        {
            var (document, position) = _workspace.GetLogicalDocument(request);

            var semanticModel = await document.GetSemanticModelAsync(token);
            if (semanticModel == null)
            {
                return new LocationContainer();
            }

            var symbolSearchService = document.LanguageServices.GetService<ISymbolSearchService>();
            if (symbolSearchService == null)
            {
                return new LocationContainer();
            }

            var mappedPosition = semanticModel.SyntaxTree.MapRootFilePosition(position);

            var symbolAtPosition = symbolSearchService.FindSymbol(semanticModel, mappedPosition);
            if (symbolAtPosition == null)
            {
                return new LocationContainer();
            }

            var includeDeclaration = request.Context == null || request.Context.IncludeDeclaration;

            var locations = symbolSearchService.FindUsages(semanticModel, symbolAtPosition.Value.Symbol)
                .Where(x => x.Span.File.IsRootFile)
                .Where(x => includeDeclaration || x.Kind != SymbolSpanKind.Definition)
                .Select(x => new Location
                {
                    Uri = request.TextDocument.Uri,
                    Range = Helpers.ToRange(document.SourceText, x.Span.Span)
                })
                .ToArray();

            return locations;
        }

        ReferenceRegistrationOptions IRegistration<ReferenceRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<ReferenceCapability>.SetCapability(ReferenceCapability capability) { }
    }
}
