using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using ShaderTools.CodeAnalysis;

namespace ShaderTools.LanguageServer.Handlers
{
    internal sealed class TextDocumentSyncHandler : ITextDocumentSyncHandler
    {
        private readonly LanguageServerWorkspace _workspace;
        private readonly TextDocumentChangeRegistrationOptions _changeRegistrationOptions;

        public TextDocumentSyncHandler(LanguageServerWorkspace workspace, DocumentSelector documentSelector)
        {
            _workspace = workspace;
            _changeRegistrationOptions = new TextDocumentChangeRegistrationOptions
            {
                DocumentSelector = documentSelector,
                SyncKind = TextDocumentSyncKind.Incremental,
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            // OmniSharp calls this while *routing* a request, on the input-processing thread.
            // Anything thrown here escapes as an InputProcessingException that tears down the
            // message pump and kills the server, so this must never throw.
            //
            // The document is legitimately unknown whenever a request arrives without a preceding
            // didOpen. That happens after the server restarts: the client still considers its open
            // documents synced, so it doesn't re-send didOpen, but it does ask the new server for
            // document symbols / links of the visible editors. Falling back to the language implied
            // by the file extension lets routing finish; the handler then returns an empty result.
            var document = _workspace.GetDocument(uri);

            return new TextDocumentAttributes(
                uri,
                Helpers.ToLspLanguage(document?.Language ?? GuessLanguage(uri)));
        }

        private static string GuessLanguage(DocumentUri uri)
        {
            var path = Helpers.FromUri(uri);

            return path != null && path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                ? LanguageNames.ShaderLab
                : LanguageNames.Hlsl;
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var document = _workspace.GetDocument(notification.TextDocument.Uri);

            if (document == null)
            {
                return Unit.Task;
            }

            // Content changes are sequential: each one's range is expressed against the text
            // produced by the previous one, so they can't all be converted against the original
            // text and applied as a batch. A change with no range replaces the whole document.
            foreach (var contentChange in notification.ContentChanges)
            {
                var sourceText = document.SourceText;

                var newText = contentChange.Range == null
                    ? SourceText.From(contentChange.Text)
                    : sourceText.WithChanges(
                        Helpers.ToTextChange(sourceText, contentChange.Range, contentChange.Text));

                document = _workspace.UpdateDocument(document, newText);
            }

            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken cancellationToken)
        {
            _workspace.OpenDocument(
                notification.TextDocument.Uri,
                notification.TextDocument.Text,
                notification.TextDocument.LanguageId);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken cancellationToken)
        {
            var document = _workspace.GetDocument(notification.TextDocument.Uri);

            if (document != null)
            {
                _workspace.CloseDocument(document.Id);
            }

            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken cancellationToken) => Unit.Task;

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions() => _changeRegistrationOptions;

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return _changeRegistrationOptions;
        }

        void ICapability<SynchronizationCapability>.SetCapability(SynchronizationCapability capability) { }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions() => null;
    }
}
