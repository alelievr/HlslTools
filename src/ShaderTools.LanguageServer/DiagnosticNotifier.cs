using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using ShaderTools.CodeAnalysis;
using ShaderTools.CodeAnalysis.Diagnostics;
using ShaderTools.Utilities;
using ShaderTools.Utilities.Threading;

namespace ShaderTools.LanguageServer
{
    internal sealed class DiagnosticNotifier : IDisposable
    {
        private readonly ILanguageServer _server;
        private readonly IDiagnosticService _diagnosticService;
        private readonly SimpleTaskQueue _queue;

        private readonly Dictionary<DocumentId, List<Uri>> _lastUris;

        public DiagnosticNotifier(ILanguageServer server, IDiagnosticService diagnosticService)
        {
            _server = server;
            _diagnosticService = diagnosticService;
            _queue = new SimpleTaskQueue(TaskScheduler.Default);
            _lastUris = new Dictionary<DocumentId, List<Uri>>();

            _diagnosticService.DiagnosticsUpdated += OnDiagnosticsUpdated;
        }

        public void Dispose()
        {
            _diagnosticService.DiagnosticsUpdated -= OnDiagnosticsUpdated;
        }

        private void OnDiagnosticsUpdated(object sender, DiagnosticsUpdatedEventArgs e)
        {
            _queue.ScheduleTask(() => UpdateDiagnostics(e.Document));
        }

        /// <summary>
        /// Custom notification carrying the conditional preprocessor directives of a document,
        /// so the client can render gutter toggles for defines.
        /// </summary>
        public const string ConditionalDirectivesNotificationName = "hlslTools/conditionalDirectives";

        private async Task UpdateDiagnostics(Document document)
        {
            var diagnostics = document != null
                ? await _diagnosticService.GetDiagnosticsAsync(document.Id, CancellationToken.None)
                : ImmutableArray<MappedDiagnostic>.Empty;

            var diagnosticsGroupedByFile = diagnostics
                .GroupBy(x => x.FileSpan.File.FilePath)
                .ToDictionary(x => Helpers.ToUri(x.Key), x => x.Select(Helpers.ToDiagnostic).Distinct(CachedDiagnosticComparer).ToArray());

            await AddInactiveRegionDiagnostics(document, diagnosticsGroupedByFile);

            if (!_lastUris.TryGetValue(document.Id, out var diagnosticUris))
            {
                _lastUris.Add(document.Id, diagnosticUris = new List<Uri>());
            }

            diagnosticUris.AddRange(diagnosticsGroupedByFile.Keys);

            foreach (var diagnosticUri in diagnosticUris)
            {
                if (!diagnosticsGroupedByFile.TryGetValue(diagnosticUri, out var diagnosticsForThisFile))
                {
                    diagnosticsForThisFile = Array.Empty<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>();
                }

                _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
                {
                    Uri = diagnosticUri,
                    Diagnostics = diagnosticsForThisFile
                });
            }

            diagnosticUris.Clear();
            diagnosticUris.AddRange(diagnosticsGroupedByFile.Keys);

            await SendConditionalDirectives(document);
        }

        /// <summary>
        /// Pushes the document's #if / #ifdef / #ifndef / #elif directives (with the macros they
        /// reference and whether each macro is defined in shadertoolsconfig.json) to the client.
        /// </summary>
        private async Task SendConditionalDirectives(Document document)
        {
            if (document == null || document.Language != LanguageNames.Hlsl || document.FilePath == null)
            {
                return;
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(CancellationToken.None);
            var collector = Handlers.HlslDirectiveCollector.Collect((ShaderTools.CodeAnalysis.Hlsl.Syntax.SyntaxNode) syntaxTree.Root);

            var configFile = document.Workspace.LoadConfigFile(
                new ShaderTools.CodeAnalysis.Text.SourceFile(document.SourceText, document.FilePath));

            var directives = new Newtonsoft.Json.Linq.JArray();

            foreach (var directive in collector.Conditionals)
            {
                if (directive.HashToken == null || !directive.HashToken.FileSpan.File.IsRootFile)
                {
                    continue;
                }

                var macros = new Newtonsoft.Json.Linq.JArray();
                foreach (var macroName in Handlers.HlslDirectiveCollector.GetReferencedMacroNames(directive))
                {
                    macros.Add(new Newtonsoft.Json.Linq.JObject
                    {
                        ["name"] = macroName,
                        ["definedInConfig"] = configFile.HlslPreprocessorDefinitions.ContainsKey(macroName)
                    });
                }

                if (macros.Count == 0)
                {
                    continue;
                }

                var directiveSpan = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(
                    directive.HashToken.FileSpan.Span.Start,
                    directive.EndOfDirectiveToken.FileSpan.Span.End);

                var range = Helpers.ToRange(document.SourceText, directiveSpan);

                directives.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["range"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["start"] = new Newtonsoft.Json.Linq.JObject { ["line"] = range.Start.Line, ["character"] = range.Start.Character },
                        ["end"] = new Newtonsoft.Json.Linq.JObject { ["line"] = range.End.Line, ["character"] = range.End.Character }
                    },
                    ["branchTaken"] = directive.BranchTaken,
                    ["macros"] = macros
                });
            }

            _server.SendNotification(ConditionalDirectivesNotificationName, new Newtonsoft.Json.Linq.JObject
            {
                ["uri"] = Helpers.ToUri(document.FilePath).AbsoluteUri,
                ["directives"] = directives
            });
        }

        /// <summary>
        /// Publishes hint diagnostics tagged "Unnecessary" for code excluded by inactive
        /// preprocessor branches, which the client renders as faded text.
        /// </summary>
        private static async Task AddInactiveRegionDiagnostics(
            Document document,
            Dictionary<Uri, OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic[]> diagnosticsGroupedByFile)
        {
            if (document == null || document.Language != LanguageNames.Hlsl)
            {
                return;
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(CancellationToken.None);
            var collector = Handlers.HlslDirectiveCollector.Collect((ShaderTools.CodeAnalysis.Hlsl.Syntax.SyntaxNode) syntaxTree.Root);

            foreach (var disabledGroup in collector.DisabledText.GroupBy(x => x.FileSpan.File))
            {
                var file = disabledGroup.Key;
                if (file.FilePath == null)
                {
                    continue;
                }

                var inactiveDiagnostics = disabledGroup
                    .Select(x => new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
                    {
                        Range = Helpers.ToRange(file.Text, x.FileSpan.Span),
                        Severity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity.Hint,
                        Code = "inactive-branch",
                        Source = "HLSL",
                        Message = "Inactive preprocessor branch",
                        Tags = new Container<DiagnosticTag>(DiagnosticTag.Unnecessary)
                    })
                    .ToArray();

                var uri = Helpers.ToUri(file.FilePath);

                diagnosticsGroupedByFile[uri] = diagnosticsGroupedByFile.TryGetValue(uri, out var existing)
                    ? existing.Concat(inactiveDiagnostics).ToArray()
                    : inactiveDiagnostics;
            }
        }

        private static readonly DiagnosticComparer CachedDiagnosticComparer = new DiagnosticComparer();

        private sealed class DiagnosticComparer : IEqualityComparer<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>
        {
            public bool Equals(OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic x, OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic y)
            {
                return x.Code.Equals(y.Code)
                    && x.Message == y.Message
                    && x.Range == y.Range
                    && x.Severity == y.Severity
                    && x.Source == y.Source;
            }

            public int GetHashCode(OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic obj)
            {
                var hash = obj.Code.GetHashCode();
                hash = Hash.Combine(obj.Message.GetHashCode(), hash);
                hash = Hash.Combine(obj.Range.GetHashCode(), hash);
                hash = Hash.Combine(obj.Severity.GetHashCode(), hash);
                hash = Hash.Combine(obj.Source.GetHashCode(), hash);
                return hash;
            }
        }
    }
}
