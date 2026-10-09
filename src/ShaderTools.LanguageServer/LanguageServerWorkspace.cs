using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis;
using ShaderTools.CodeAnalysis.Host.Mef;
using ShaderTools.CodeAnalysis.Text;

namespace ShaderTools.LanguageServer
{
    internal sealed class LanguageServerWorkspace : Workspace
    {
        private readonly IMefHostExportProvider _hostServices;

        public LanguageServerWorkspace(MefHostServices hostServices)
            : base(hostServices)
        {
            _hostServices = hostServices;
        }

        public Document GetDocument(DocumentUri uri)
        {
            return CurrentDocuments.GetDocumentWithFilePath(Helpers.FromUri(uri));
        }

        /// <summary>
        /// Resolves a document + position from an LSP request. Returns a null document if the
        /// document was never opened (or has been closed); callers must check. The position is
        /// clamped to the document, because the editor's copy and ours can briefly disagree about
        /// its length - a request for a line past the end must not fault.
        /// </summary>
        public (Document logicalDocument, int position) GetLogicalDocument(TextDocumentPositionParams textDocumentPositionParams)
        {
            var document = GetDocument(textDocumentPositionParams.TextDocument.Uri);

            if (document == null)
            {
                return (null, 0);
            }

            var documentPosition = Helpers.ToPosition(document.SourceText, textDocumentPositionParams.Position);

            return (document, documentPosition);
        }

        public Document OpenDocument(DocumentUri uri, string text, string languageId)
        {
            var filePath = Helpers.FromUri(uri);

            var documentId = DocumentId.CreateNewId(filePath);
            var sourceText = SourceText.From(text);

            var document = CreateDocument(documentId, languageId, new SourceFile(sourceText, filePath));
            OnDocumentOpened(document);
            return document;
        }

        public Document UpdateDocument(Document document, SourceText newText)
        {
            OnDocumentTextChanged(document.Id, newText);
            return CurrentDocuments.GetDocument(document.Id);
        }

        public void CloseDocument(DocumentId documentId)
        {
            OnDocumentClosed(documentId);
        }

        /// <summary>
        /// Forces open documents to re-parse (and re-publish diagnostics), picking up
        /// external changes such as an edited shadertoolsconfig.json.
        /// </summary>
        public void RefreshOpenDocuments()
        {
            foreach (var document in CurrentDocuments.Documents.ToArray())
            {
                // A fresh SourceText instance guarantees the document is rebuilt.
                OnDocumentTextChanged(document.Id, SourceText.From(document.SourceText.ToString()));
            }
        }

        public T GetGlobalService<T>()
            where T : class
        {
            return _hostServices.GetExports<T>().FirstOrDefault()?.Value;
        }
    }
}
