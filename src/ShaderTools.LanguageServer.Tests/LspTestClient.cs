using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ShaderTools.LanguageServer.Tests
{
    /// <summary>
    /// Minimal JSON-RPC / LSP client that talks to an in-process LanguageServerHost
    /// over anonymous pipes, for integration-testing the real server end-to-end.
    /// </summary>
    internal sealed class LspTestClient : IDisposable
    {
        private readonly AnonymousPipeServerStream _clientToServer;
        private readonly AnonymousPipeServerStream _serverToClient;
        private readonly Stream _serverInput;
        private readonly Stream _serverOutput;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<JToken>> _pendingRequests =
            new ConcurrentDictionary<int, TaskCompletionSource<JToken>>();

        private readonly BlockingCollection<JObject> _notifications = new BlockingCollection<JObject>();

        private Task<LanguageServerHost> _hostTask;
        private int _nextId = 1;

        public LspTestClient()
        {
            _clientToServer = new AnonymousPipeServerStream(PipeDirection.Out);
            _serverToClient = new AnonymousPipeServerStream(PipeDirection.In);

            _serverInput = new AnonymousPipeClientStream(PipeDirection.In, _clientToServer.GetClientHandleAsString());
            _serverOutput = new AnonymousPipeClientStream(PipeDirection.Out, _serverToClient.GetClientHandleAsString());
        }

        public Task StartAsync(string logFilePath = null)
        {
            var logPath = logFilePath ?? Path.Combine(Path.GetTempPath(), "ShaderToolsTest-" + Guid.NewGuid().ToString("N"));

            // Note: Create doesn't complete until the client sends the initialize request,
            // so it must not be awaited here.
            _hostTask = LanguageServerHost.Create(
                _serverInput,
                _serverOutput,
                logPath,
                Microsoft.Extensions.Logging.LogLevel.Trace);

            _ = Task.Run(ReadLoop);

            return Task.CompletedTask;
        }

        public async Task<JToken> SendRequestAsync(string method, JToken @params, int timeoutSeconds = 15)
        {
            var id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[id] = tcs;

            Send(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = @params
            });

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            if (completed != tcs.Task)
            {
                throw new TimeoutException($"No response to '{method}' within {timeoutSeconds}s");
            }

            return await tcs.Task;
        }

        public void SendNotification(string method, JToken @params)
        {
            Send(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = @params
            });
        }

        /// <summary>Waits for a notification with the given method, buffering others.</summary>
        public JObject WaitForNotification(string method, int timeoutSeconds = 15)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            var requeue = new List<JObject>();
            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    if (!_notifications.TryTake(out var notification, TimeSpan.FromMilliseconds(250)))
                    {
                        continue;
                    }

                    if (notification["method"]?.ToString() == method)
                    {
                        return notification;
                    }

                    requeue.Add(notification);
                }
            }
            finally
            {
                foreach (var item in requeue)
                    _notifications.Add(item);
            }

            throw new TimeoutException($"No '{method}' notification within {timeoutSeconds}s");
        }

        public void DrainNotifications()
        {
            while (_notifications.TryTake(out _)) { }
        }

        private readonly object _writeLock = new object();

        private void Send(JObject message)
        {
            var json = message.ToString(Newtonsoft.Json.Formatting.None);
            var contentBytes = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {contentBytes.Length}\r\n\r\n");

            lock (_writeLock)
            {
                _clientToServer.Write(header, 0, header.Length);
                _clientToServer.Write(contentBytes, 0, contentBytes.Length);
                _clientToServer.Flush();
            }
        }

        private void ReadLoop()
        {
            try
            {
                while (true)
                {
                    var message = ReadMessage();
                    if (message == null)
                    {
                        return;
                    }

                    if (message["id"] != null && message["method"] != null)
                    {
                        // Server-to-client request: reply with an empty-ish result so the server never stalls.
                        var result = message["method"].ToString() == "workspace/configuration"
                            ? (JToken) new JArray(new JObject())
                            : JValue.CreateNull();

                        Send(new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = message["id"],
                            ["result"] = result
                        });
                    }
                    else if (message["id"] != null)
                    {
                        var id = message["id"].Value<int>();
                        if (_pendingRequests.TryRemove(id, out var tcs))
                        {
                            if (message["error"] != null)
                            {
                                tcs.SetException(new Exception("LSP error response: " + message["error"].ToString()));
                            }
                            else
                            {
                                tcs.SetResult(message["result"]);
                            }
                        }
                    }
                    else
                    {
                        _notifications.Add(message);
                    }
                }
            }
            catch
            {
                // Stream closed - test is shutting down.
            }
        }

        private JObject ReadMessage()
        {
            var headerBuilder = new StringBuilder();
            var contentLength = -1;

            while (true)
            {
                var line = ReadHeaderLine();
                if (line == null)
                {
                    return null;
                }

                if (line.Length == 0)
                {
                    break;
                }

                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim());
                }
            }

            if (contentLength < 0)
            {
                return null;
            }

            var buffer = new byte[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var n = _serverToClient.Read(buffer, read, contentLength - read);
                if (n <= 0)
                {
                    return null;
                }
                read += n;
            }

            return JObject.Parse(Encoding.UTF8.GetString(buffer));
        }

        private string ReadHeaderLine()
        {
            var bytes = new List<byte>();
            while (true)
            {
                var b = _serverToClient.ReadByte();
                if (b < 0)
                {
                    return null;
                }

                if (b == '\n')
                {
                    // Strip trailing \r
                    if (bytes.Count > 0 && bytes[bytes.Count - 1] == '\r')
                    {
                        bytes.RemoveAt(bytes.Count - 1);
                    }

                    return Encoding.ASCII.GetString(bytes.ToArray());
                }

                bytes.Add((byte) b);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_hostTask != null && _hostTask.IsCompletedSuccessfully)
                {
                    _hostTask.Result.Dispose();
                }
            }
            catch { }
            _clientToServer.Dispose();
            _serverToClient.Dispose();
            _serverInput.Dispose();
            _serverOutput.Dispose();
        }
    }
}
