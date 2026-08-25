using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis;
using ShaderTools.CodeAnalysis.Hlsl;
using ShaderTools.CodeAnalysis.Hlsl.Syntax;
using ShaderTools.CodeAnalysis.Hlsl.Text;
using ShaderTools.CodeAnalysis.Text;
using SourceText = Microsoft.CodeAnalysis.Text.SourceText;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class DocumentLinkHandler : IDocumentLinkHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly DocumentLinkRegistrationOptions _registrationOptions;

        public DocumentLinkHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _registrationOptions = new DocumentLinkRegistrationOptions
            {
                DocumentSelector = documentSelector,
                ResolveProvider = false
            };
        }

        public async Task<DocumentLinkContainer> Handle(DocumentLinkParams request, CancellationToken token)
        {
            var document = _workspace.GetDocument(request.TextDocument.Uri);
            if (document == null || document.Language != LanguageNames.Hlsl || document.FilePath == null)
            {
                return new DocumentLinkContainer();
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(token);
            var collector = HlslDirectiveCollector.Collect((SyntaxNode) syntaxTree.Root);

            if (collector.Includes.Count == 0)
            {
                return new DocumentLinkContainer();
            }

            var resolver = CreateIncludeFileResolver(document);
            var rootFile = new SourceFile(document.SourceText, document.FilePath);

            var links = new List<DocumentLink>();

            foreach (var include in collector.Includes)
            {
                if (include.Filename == null || !include.Filename.FileSpan.File.IsRootFile)
                {
                    continue;
                }

                var resolved = resolver.OpenInclude(include.TrimmedFilename, rootFile);
                if (resolved?.FilePath == null)
                {
                    continue;
                }

                links.Add(new DocumentLink
                {
                    Range = Helpers.ToRange(document.SourceText, include.Filename.FileSpan.Span),
                    Target = Helpers.ToUri(resolved.FilePath).AbsoluteUri
                });
            }

            return links;
        }

        private static IncludeFileResolver CreateIncludeFileResolver(Document document)
        {
            var configFile = document.Workspace.LoadConfigFile(new SourceFile(document.SourceText, document.FilePath));

            var parseOptions = new HlslParseOptions();
            parseOptions.AdditionalIncludeDirectories.AddRange(configFile.HlslAdditionalIncludeDirectories);

            foreach (var kvp in configFile.HlslVirtualDirectoryMappings)
                parseOptions.VirtualDirectoryMappings.Add(kvp.Key, kvp.Value);

            return new IncludeFileResolver(new DiskFileSystem(), parseOptions);
        }

        private sealed class DiskFileSystem : IIncludeFileSystem
        {
            public bool TryGetFile(string path, out SourceText text)
            {
                if (File.Exists(path))
                {
                    text = SourceText.From(File.ReadAllText(path));
                    return true;
                }

                text = null;
                return false;
            }
        }

        DocumentLinkRegistrationOptions IRegistration<DocumentLinkRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<DocumentLinkCapability>.SetCapability(DocumentLinkCapability capability) { }
    }
}
