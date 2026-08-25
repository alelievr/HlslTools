using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis.Shared.Extensions;
using ShaderTools.CodeAnalysis.Structure;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class FoldingRangeHandler : IFoldingRangeHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly FoldingRangeRegistrationOptions _registrationOptions;

        public FoldingRangeHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new FoldingRangeRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public async Task<Container<FoldingRange>> Handle(FoldingRangeRequestParam request, CancellationToken token)
        {
            var document = _workspace.GetDocument(request.TextDocument.Uri);

            var blockStructureProvider = document.GetLanguageService<IBlockStructureProvider>();
            if (blockStructureProvider == null)
            {
                return new Container<FoldingRange>();
            }

            var blockSpans = await blockStructureProvider.ProvideBlockStructureAsync(document, token);

            var results = new List<FoldingRange>();

            foreach (var blockSpan in blockSpans)
            {
                if (!blockSpan.IsCollapsible)
                {
                    continue;
                }

                var linePositionSpan = document.SourceText.Lines.GetLinePositionSpan(blockSpan.TextSpan);
                if (linePositionSpan.Start.Line == linePositionSpan.End.Line)
                {
                    continue;
                }

                results.Add(new FoldingRange
                {
                    StartLine = linePositionSpan.Start.Line,
                    StartCharacter = linePositionSpan.Start.Character,
                    EndLine = linePositionSpan.End.Line,
                    EndCharacter = linePositionSpan.End.Character,
                    Kind = blockSpan.Type == BlockSpanType.PreprocessorRegion
                        ? FoldingRangeKind.Region
                        : (FoldingRangeKind?) null
                });
            }

            return new Container<FoldingRange>(results);
        }

        FoldingRangeRegistrationOptions IRegistration<FoldingRangeRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<FoldingRangeCapability>.SetCapability(FoldingRangeCapability capability) { }
    }
}
