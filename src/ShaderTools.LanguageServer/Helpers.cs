using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ShaderTools.CodeAnalysis;
using ShaderTools.CodeAnalysis.NavigateTo;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace ShaderTools.LanguageServer
{
    internal static class Helpers
    {
        public static string FromUri(DocumentUri uri)
        {
            return uri.GetFileSystemPath();
        }

        public static Uri ToUri(string filePath)
        {
            filePath = filePath.Replace('\\', '/');

            return (!filePath.StartsWith("/"))
                ? new Uri($"file:///{filePath}")
                : new Uri($"file://{filePath}");
        }

        public static Range ToRange(SourceText sourceText, TextSpan textSpan)
        {
            var linePositionSpan = sourceText.Lines.GetLinePositionSpan(textSpan);

            return new Range
            {
                Start = new Position
                {
                    Line = linePositionSpan.Start.Line,
                    Character = linePositionSpan.Start.Character
                },
                End = new Position
                {
                    Line = linePositionSpan.End.Line,
                    Character = linePositionSpan.End.Character
                }
            };
        }

        private const string DiagnosticSourceName = "Shader Tools";

        public static Diagnostic ToDiagnostic(this CodeAnalysis.Diagnostics.MappedDiagnostic diagnostic)
        {
            var sourceFileSpan = diagnostic.FileSpan;

            var linePositionSpan = ToRange(sourceFileSpan.File.Text, sourceFileSpan.Span);

            return new Diagnostic
            {
                Severity = ToDiagnosticSeverity(diagnostic.Diagnostic.Severity),
                Message = diagnostic.Diagnostic.Message,
                Code = diagnostic.Diagnostic.Descriptor.Id,
                Source = DiagnosticSourceName,
                Range = linePositionSpan
            };
        }

        private static DiagnosticSeverity ToDiagnosticSeverity(CodeAnalysis.Diagnostics.DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case CodeAnalysis.Diagnostics.DiagnosticSeverity.Error:
                    return DiagnosticSeverity.Error;

                case CodeAnalysis.Diagnostics.DiagnosticSeverity.Warning:
                    return DiagnosticSeverity.Warning;

                default:
                    return DiagnosticSeverity.Error;
            }
        }

        public static TextChange ToTextChange(SourceText sourceText, Range changeRange, string insertString)
        {
            var startPosition = ToPosition(sourceText, changeRange.Start);
            var endPosition = ToPosition(sourceText, changeRange.End);

            return new TextChange(
                TextSpan.FromBounds(startPosition, Math.Max(startPosition, endPosition)),
                insertString);
        }

        /// <summary>
        /// Converts an LSP line/character to an offset, clamped to the text. The editor's copy of a
        /// document and ours can briefly disagree about its length, and an out-of-range line used to
        /// throw ArgumentOutOfRangeException out of <see cref="TextLineCollection.GetPosition"/> and
        /// fail the request.
        /// </summary>
        public static int ToPosition(SourceText sourceText, Position position)
        {
            var lines = sourceText.Lines;

            if (lines.Count == 0)
            {
                return 0;
            }

            var lineNumber = Math.Max(0, Math.Min((int) position.Line, lines.Count - 1));
            var line = lines[lineNumber];

            var character = Math.Max(0, Math.Min((int) position.Character, line.SpanIncludingLineBreak.Length));

            return Math.Min(line.Start + character, sourceText.Length);
        }

        public static async Task FindSymbolsInDocument(
            INavigateToSearchService searchService,
            Document document,
            string searchPattern,
            CancellationToken cancellationToken,
            ImmutableArray<SymbolInformation>.Builder resultsBuilder)
        {
            var foundSymbols = await searchService.SearchDocumentAsync(document, searchPattern, cancellationToken);

            resultsBuilder.AddRange(foundSymbols
               .Select(r => new SymbolInformation
               {
                   ContainerName = r.AdditionalInformation,
                   Kind = GetSymbolKind(r.Kind),
                   Location = new Location
                   {
                       Uri = ToUri(r.NavigableItem.SourceSpan.File.FilePath),
                       Range = ToRange(r.NavigableItem.Document.SourceText, r.NavigableItem.SourceSpan.Span)
                   },
                   Name = r.Name
               }));
        }

        private static SymbolKind GetSymbolKind(string symbolType)
        {
            switch (symbolType)
            {
                case NavigateToItemKind.Class:
                    return SymbolKind.Class;

                case NavigateToItemKind.Structure:
                    return SymbolKind.Struct;

                case NavigateToItemKind.Module:
                    return SymbolKind.Namespace;

                case NavigateToItemKind.Interface:
                    return SymbolKind.Interface;

                case NavigateToItemKind.Field:
                    return SymbolKind.Field;

                case NavigateToItemKind.Method:
                    return SymbolKind.Method;

                default:
                    return SymbolKind.Variable;
            }
        }

        public static string ToLspLanguage(string language)
        {
            return language.ToLowerInvariant();
        }
    }
}
