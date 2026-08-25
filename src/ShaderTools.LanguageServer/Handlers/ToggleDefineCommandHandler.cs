using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace ShaderTools.LanguageServer.Handlers
{
    /// <summary>
    /// Toggles a preprocessor definition in the shadertoolsconfig.json that governs a
    /// document, then re-parses open documents so diagnostics, folding, and inactive
    /// regions reflect the new preprocessor state.
    /// </summary>
    internal sealed class ToggleDefineCommandHandler : IExecuteCommandHandler
    {
        public const string CommandName = "hlslTools.toggleDefine";

        private const string ConfigFileName = "shadertoolsconfig.json";
        private const string PreprocessorDefinitionsKey = "hlsl.preprocessorDefinitions";

        private readonly LanguageServerWorkspace _workspace;
        private readonly ExecuteCommandRegistrationOptions _registrationOptions;

        /// <summary>Set by the host once the server is built; used for user-visible feedback.</summary>
        public ILanguageServer Server { get; set; }

        public ToggleDefineCommandHandler(LanguageServerWorkspace workspace)
        {
            _workspace = workspace;
            _registrationOptions = new ExecuteCommandRegistrationOptions
            {
                Commands = new Container<string>(CommandName)
            };
        }

        public Task<Unit> Handle(ExecuteCommandParams request, CancellationToken token)
        {
            try
            {
                HandleCore(request);
            }
            catch (Exception ex)
            {
                ShowMessage(MessageType.Error, $"HLSL Tools: toggling define failed: {ex.Message}");
            }

            return Unit.Task;
        }

        private void HandleCore(ExecuteCommandParams request)
        {
            if (request.Command != CommandName || request.Arguments == null || request.Arguments.Count < 2)
            {
                ShowMessage(MessageType.Error, $"HLSL Tools: '{CommandName}' called with missing arguments.");
                return;
            }

            var documentUri = request.Arguments[0].ToString();
            var macroName = request.Arguments[1].ToString();

            var document = _workspace.GetDocument(DocumentUri.Parse(documentUri));
            if (document?.FilePath == null || string.IsNullOrEmpty(macroName))
            {
                ShowMessage(MessageType.Error, $"HLSL Tools: can't resolve document '{documentUri}' to toggle '{macroName}'.");
                return;
            }

            var configPath = FindConfigFile(Path.GetDirectoryName(document.FilePath))
                ?? Path.Combine(Path.GetDirectoryName(document.FilePath), ConfigFileName);

            var nowDefined = ToggleDefineInConfigFile(configPath, macroName);

            // Make sure the next parse re-reads the config, then rebuild open documents so
            // diagnostics and inactive regions update immediately.
            _workspace.InvalidateConfigFileCache();
            _workspace.RefreshOpenDocuments();

            ShowMessage(
                MessageType.Info,
                nowDefined
                    ? $"HLSL Tools: defined {macroName} in {configPath}"
                    : $"HLSL Tools: removed {macroName} from {configPath}");
        }

        private void ShowMessage(MessageType type, string message)
        {
            Server?.Window.ShowMessage(new ShowMessageParams
            {
                Type = type,
                Message = message
            });
        }

        private static string FindConfigFile(string directory)
        {
            while (!string.IsNullOrEmpty(directory))
            {
                var candidate = Path.Combine(directory, ConfigFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private static bool ToggleDefineInConfigFile(string configPath, string macroName)
        {
            var json = File.Exists(configPath)
                ? JObject.Parse(File.ReadAllText(configPath))
                : new JObject();

            if (!(json[PreprocessorDefinitionsKey] is JObject definitions))
            {
                definitions = new JObject();
                json[PreprocessorDefinitionsKey] = definitions;
            }

            bool nowDefined;
            if (definitions.ContainsKey(macroName))
            {
                definitions.Remove(macroName);
                nowDefined = false;
            }
            else
            {
                definitions[macroName] = "1";
                nowDefined = true;
            }

            File.WriteAllText(configPath, json.ToString(Formatting.Indented));

            return nowDefined;
        }

        ExecuteCommandRegistrationOptions IRegistration<ExecuteCommandRegistrationOptions>.GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        void ICapability<ExecuteCommandCapability>.SetCapability(ExecuteCommandCapability capability) { }
    }
}
