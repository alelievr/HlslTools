using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ShaderTools.LanguageServer.Tests
{
    public class ToggleDefineIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDir;

        public ToggleDefineIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _testDir = Path.Combine(Path.GetTempPath(), "HlslToolsLspTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }

        private static string ToVsCodeUri(string path)
        {
            // VS Code sends e.g. file:///c%3A/Github/Foo/Bar.hlsl
            var forward = path.Replace('\\', '/');
            var drive = char.ToLowerInvariant(forward[0]);
            return $"file:///{drive}%3A{Uri.EscapeUriString(forward.Substring(2))}";
        }

        [Fact]
        public async Task ToggleDefine_EndToEnd()
        {
            var shaderPath = Path.Combine(_testDir, "Test.hlsl");
            var shaderContent = "#ifdef MY_FEATURE\r\nfloat4 FeatureCode() { return 0; }\r\n#endif\r\nfloat4 Main() { return 1; }\r\n";
            File.WriteAllText(shaderPath, shaderContent);

            // Mimic VS Code's URI form exactly: lowercase drive letter with percent-encoded colon.
            var shaderUri = ToVsCodeUri(shaderPath);

            using (var client = new LspTestClient())
            {
                await client.StartAsync();

                // --- initialize ---
                var initResult = await client.SendRequestAsync("initialize", new JObject
                {
                    ["processId"] = JValue.CreateNull(),
                    ["rootUri"] = JValue.CreateNull(),
                    ["capabilities"] = new JObject()
                });

                _output.WriteLine("capabilities: " + initResult["capabilities"].ToString());

                var executeCommandProvider = initResult["capabilities"]?["executeCommandProvider"];
                Assert.NotNull(executeCommandProvider);
                Assert.Contains("hlslTools.toggleDefine",
                    executeCommandProvider["commands"].Select(x => x.ToString()));

                client.SendNotification("initialized", new JObject());

                // --- open the document ---
                client.SendNotification("textDocument/didOpen", new JObject
                {
                    ["textDocument"] = new JObject
                    {
                        ["uri"] = shaderUri,
                        ["languageId"] = "hlsl",
                        ["version"] = 1,
                        ["text"] = shaderContent
                    }
                });

                // Wait for first diagnostics: #ifdef MY_FEATURE branch should be inactive.
                var diagnostics = client.WaitForNotification("textDocument/publishDiagnostics");
                _output.WriteLine("diagnostics(1): " + diagnostics["params"].ToString());
                Assert.Contains("inactive-branch", diagnostics["params"].ToString());

                // --- the custom directives notification should describe the #ifdef ---
                var directivesNotification = client.WaitForNotification("hlslTools/conditionalDirectives");
                _output.WriteLine("directives: " + directivesNotification["params"].ToString());

                var directives = (JArray) directivesNotification["params"]["directives"];
                Assert.NotEmpty(directives);

                var macro = directives[0]["macros"][0];
                Assert.Equal("MY_FEATURE", macro["name"].ToString());
                Assert.False(macro["definedInConfig"].Value<bool>());
                Assert.False(directives[0]["branchTaken"].Value<bool>());

                // --- execute the toggle command exactly like the hover command link would ---
                client.DrainNotifications();

                var executeResult = await client.SendRequestAsync("workspace/executeCommand", new JObject
                {
                    ["command"] = "hlslTools.toggleDefine",
                    ["arguments"] = new JArray(shaderUri, "MY_FEATURE")
                });

                _output.WriteLine("executeCommand result: " + (executeResult?.ToString() ?? "null"));

                // --- verify the config file was created with the define ---
                var configPath = Path.Combine(_testDir, "shadertoolsconfig.json");
                Assert.True(File.Exists(configPath), "shadertoolsconfig.json should have been created");
                var configText = File.ReadAllText(configPath);
                _output.WriteLine("config: " + configText);
                Assert.Contains("MY_FEATURE", configText);

                // --- the user should get visible confirmation ---
                var message = client.WaitForNotification("window/showMessage");
                _output.WriteLine("showMessage: " + message["params"].ToString());
                Assert.Contains("MY_FEATURE", message["params"]["message"].ToString());

                // --- refreshed directives should now say defined + branch taken ---
                var refreshedDirectives = client.WaitForNotification("hlslTools/conditionalDirectives");
                _output.WriteLine("directives(2): " + refreshedDirectives["params"].ToString());
                var refreshedMacro = ((JArray) refreshedDirectives["params"]["directives"])[0]["macros"][0];
                Assert.True(refreshedMacro["definedInConfig"].Value<bool>());
            }
        }
    }
}
