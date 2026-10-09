using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ShaderTools.LanguageServer.Tests
{
    /// <summary>
    /// The server must survive requests that don't line up with its view of the world: a request
    /// for a document it was never told about, or a position past the end of the document it has.
    /// Both used to kill the process - the first fatally, because the null reference was thrown on
    /// OmniSharp's input-processing thread while routing, which tears down the message pump.
    /// </summary>
    public class ServerResilienceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDir;

        public ServerResilienceTests(ITestOutputHelper output)
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
            var forward = path.Replace('\\', '/');
            var drive = char.ToLowerInvariant(forward[0]);
            return $"file:///{drive}%3A{Uri.EscapeUriString(forward.Substring(2))}";
        }

        private async Task<LspTestClient> StartServerAsync()
        {
            var client = new LspTestClient();
            await client.StartAsync();

            await client.SendRequestAsync("initialize", new JObject
            {
                ["processId"] = JValue.CreateNull(),
                ["rootUri"] = JValue.CreateNull(),
                ["capabilities"] = new JObject()
            });

            client.SendNotification("initialized", new JObject());

            return client;
        }

        public static TheoryData<string> DocumentRequests => new TheoryData<string>
        {
            "textDocument/documentSymbol",
            "textDocument/documentLink",
            "textDocument/foldingRange",
            "textDocument/formatting",
        };

        [Theory]
        [MemberData(nameof(DocumentRequests))]
        public async Task RequestForNeverOpenedDocument_ServerStaysAlive(string method)
        {
            // VS Code asks for document symbols / links of the visible editor as soon as the
            // server starts. After a server restart vscode-languageclient does not re-send
            // didOpen for documents it still considers synced, so these arrive for a document
            // the fresh server has never seen.
            var unopenedUri = ToVsCodeUri(Path.Combine(_testDir, "NeverOpened.hlsl"));

            using (var client = await StartServerAsync())
            {
                var @params = new JObject
                {
                    ["textDocument"] = new JObject { ["uri"] = unopenedUri }
                };

                if (method == "textDocument/formatting")
                {
                    @params["options"] = new JObject { ["tabSize"] = 4, ["insertSpaces"] = true };
                }

                var result = await client.SendRequestAsync(method, @params);
                _output.WriteLine($"{method} -> {result?.ToString() ?? "null"}");

                // The server must still be answering afterwards.
                await AssertStillAliveAsync(client);
            }
        }

        [Fact]
        public async Task PositionRequestForNeverOpenedDocument_ServerStaysAlive()
        {
            var unopenedUri = ToVsCodeUri(Path.Combine(_testDir, "NeverOpened.hlsl"));

            using (var client = await StartServerAsync())
            {
                foreach (var method in new[]
                {
                    "textDocument/definition",
                    "textDocument/hover",
                    "textDocument/completion",
                    "textDocument/signatureHelp",
                    "textDocument/documentHighlight",
                    "textDocument/references",
                })
                {
                    var @params = new JObject
                    {
                        ["textDocument"] = new JObject { ["uri"] = unopenedUri },
                        ["position"] = new JObject { ["line"] = 0, ["character"] = 0 },
                    };

                    if (method == "textDocument/references")
                    {
                        @params["context"] = new JObject { ["includeDeclaration"] = true };
                    }

                    var result = await client.SendRequestAsync(method, @params);
                    _output.WriteLine($"{method} -> {result?.ToString() ?? "null"}");
                }

                await AssertStillAliveAsync(client);
            }
        }

        [Fact]
        public async Task PositionPastEndOfDocument_ServerStaysAlive()
        {
            // The editor and the server can briefly disagree about a document's length. A
            // position past the end must not fault the request (it used to throw
            // ArgumentOutOfRangeException out of SourceText.Lines.GetPosition).
            var shaderPath = Path.Combine(_testDir, "Short.hlsl");
            var shaderContent = "float4 Main() { return 1; }\r\n";
            File.WriteAllText(shaderPath, shaderContent);
            var shaderUri = ToVsCodeUri(shaderPath);

            using (var client = await StartServerAsync())
            {
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

                // Messages are processed in order, so a response to any request for this document
                // proves the didOpen above has been applied. (A clean shader publishes no
                // diagnostics, so there's no notification to wait for.)
                await client.SendRequestAsync("textDocument/documentSymbol", new JObject
                {
                    ["textDocument"] = new JObject { ["uri"] = shaderUri }
                });

                foreach (var method in new[] { "textDocument/definition", "textDocument/hover", "textDocument/completion" })
                {
                    var result = await client.SendRequestAsync(method, new JObject
                    {
                        ["textDocument"] = new JObject { ["uri"] = shaderUri },
                        // Way past the end of a two-line document.
                        ["position"] = new JObject { ["line"] = 900, ["character"] = 5 },
                    });

                    _output.WriteLine($"{method} -> {result?.ToString() ?? "null"}");
                }

                await AssertStillAliveAsync(client);
            }
        }

        [Fact]
        public async Task MultipleContentChangesInOneNotification_AppliedSequentially()
        {
            // Each content change's range is expressed against the text left by the previous one
            // (LSP 3.x). Converting them all against the original text - as a batch - desynchronizes
            // the server from the editor, which is what later produces out-of-range positions.
            // VS Code sends notifications shaped like this for multi-cursor edits.
            var shaderPath = Path.Combine(_testDir, "Sync.hlsl");
            var shaderContent = "float4 AAA() { return 1; }\r\nfloat4 BBB() { return 2; }\r\n";
            File.WriteAllText(shaderPath, shaderContent);
            var shaderUri = ToVsCodeUri(shaderPath);

            using (var client = await StartServerAsync())
            {
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

                client.SendNotification("textDocument/didChange", new JObject
                {
                    ["textDocument"] = new JObject { ["uri"] = shaderUri, ["version"] = 2 },
                    ["contentChanges"] = new JArray
                    {
                        // 1. Insert a whole new function above everything, pushing BBB down a line.
                        new JObject
                        {
                            ["range"] = Range(0, 0, 0, 0),
                            ["text"] = "float4 ZZZ() { return 0; }\r\n"
                        },
                        // 2. Rename BBB, addressed at its position *after* the insertion above.
                        new JObject
                        {
                            ["range"] = Range(2, 7, 2, 10),
                            ["text"] = "CCC"
                        },
                    }
                });

                var names = await GetSymbolNamesAsync(client, shaderUri);

                Assert.Contains("ZZZ", names);
                Assert.Contains("AAA", names);
                Assert.Contains("CCC", names);
                Assert.DoesNotContain("BBB", names);
            }
        }

        [Fact]
        public async Task ContentChangeWithoutRange_ReplacesWholeDocument()
        {
            // A change with no range is a full-document replacement. It's legal even under
            // incremental sync, and used to throw (and drop the edit) on the null range.
            var shaderPath = Path.Combine(_testDir, "Replace.hlsl");
            var shaderContent = "float4 AAA() { return 1; }\r\n";
            File.WriteAllText(shaderPath, shaderContent);
            var shaderUri = ToVsCodeUri(shaderPath);

            using (var client = await StartServerAsync())
            {
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

                client.SendNotification("textDocument/didChange", new JObject
                {
                    ["textDocument"] = new JObject { ["uri"] = shaderUri, ["version"] = 2 },
                    ["contentChanges"] = new JArray
                    {
                        new JObject { ["text"] = "float4 REPLACED() { return 3; }\r\n" }
                    }
                });

                var names = await GetSymbolNamesAsync(client, shaderUri);

                Assert.Contains("REPLACED", names);
                Assert.DoesNotContain("AAA", names);
            }
        }

        private static JObject Range(int startLine, int startCharacter, int endLine, int endCharacter)
        {
            return new JObject
            {
                ["start"] = new JObject { ["line"] = startLine, ["character"] = startCharacter },
                ["end"] = new JObject { ["line"] = endLine, ["character"] = endCharacter },
            };
        }

        private static async Task<string[]> GetSymbolNamesAsync(LspTestClient client, string uri)
        {
            var symbols = await client.SendRequestAsync("textDocument/documentSymbol", new JObject
            {
                ["textDocument"] = new JObject { ["uri"] = uri }
            });

            return symbols.Select(x => x["name"]?.ToString()).ToArray();
        }

        [Fact]
        public async Task ConcurrentServersShareTheLogFile()
        {
            // VS Code runs one server per window, all pointed at the same log path. The file sink
            // has to be opened shared, or only the first instance can write and a crash in any
            // other one goes unrecorded.
            var logPath = Path.Combine(_testDir, "SharedLog.log");

            using (var first = new LspTestClient())
            using (var second = new LspTestClient())
            {
                foreach (var client in new[] { first, second })
                {
                    await client.StartAsync(logPath);
                    await client.SendRequestAsync("initialize", new JObject
                    {
                        ["processId"] = JValue.CreateNull(),
                        ["rootUri"] = JValue.CreateNull(),
                        ["capabilities"] = new JObject()
                    });
                    client.SendNotification("initialized", new JObject());
                }

                var contents = ReadAllTextShared(logPath);
                _output.WriteLine(contents);

                // Each instance logs its own startup line. Both have to be in there: without a
                // shared file handle the second instance can't open the log and writes nothing.
                // (This harness hosts both servers in one process, so they share a pid - what's
                // being proven here is concurrent access to the file, not distinct processes.)
                var startupLines = System.Text.RegularExpressions.Regex
                    .Matches(contents, @"\(pid \d+\) Language server starting")
                    .Count;

                Assert.True(
                    startupLines == 2,
                    $"expected 2 pid-tagged startup lines, saw {startupLines} in: {contents}");
            }
        }

        private static string ReadAllTextShared(string path)
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static async Task AssertStillAliveAsync(LspTestClient client)
        {
            // Any round-trip proves the message pump survived; a dead server times out instead.
            var symbols = await client.SendRequestAsync("workspace/symbol", new JObject
            {
                ["query"] = "Main"
            });

            Assert.NotNull(symbols);
        }
    }
}
