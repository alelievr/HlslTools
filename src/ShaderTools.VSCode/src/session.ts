import fs = require('fs');
import os = require('os');
import path = require('path');
import vscode = require('vscode');

import { CloseAction, ErrorAction, ErrorHandler, LanguageClient, LanguageClientOptions, Message, RevealOutputChannelOn, ServerOptions } from 'vscode-languageclient';
import { DirectiveToggleProvider } from './directiveToggles';

let HlslLanguageId = 'hlsl';

let LanguageIds = [HlslLanguageId];

enum SessionStatus {
    NotStarted,
    Initializing,
    Running,
    Stopping,
    Failed
}

export class SessionManager {
    private ShowSessionMenuCommandName = "ShaderTools.ShowSessionMenu";

    private sessionStatus: SessionStatus;
    private statusBarItem: vscode.StatusBarItem;
    private registeredCommands: vscode.Disposable[] = [];
    private languageServerClient: LanguageClient = undefined;
    private platform: NodeJS.Platform;
    private extensionContext: vscode.ExtensionContext;
    private directiveToggleProvider: DirectiveToggleProvider = undefined;
    private outputChannel: vscode.OutputChannel;

    constructor(context: vscode.ExtensionContext) {
        this.platform = os.platform();
        this.extensionContext = context;

        // Created eagerly, and handed to the language client below as its output channel.
        // The client creates its own channel lazily - only the first time something is logged -
        // so with revealOutputChannelOn.Never and tracing off, a healthy session never produced a
        // channel at all. "HLSL Tools" was then missing from the Output list precisely when
        // everything was fine, which is no way to tell whether the extension loaded.
        this.outputChannel = vscode.window.createOutputChannel('HLSL Tools');
        context.subscriptions.push(this.outputChannel);

        this.registerCommands();
    }

    public start() {
        this.createStatusBarItem();
        this.startEditorServices();
    }

    public log(message: string) {
        // Local time, to line up with the timestamps the language client writes into this same
        // channel. toISOString() is UTC, which made the two sets of entries look hours apart.
        const now = new Date();
        const pad = (n: number, width: number = 2) => {
            let s = n.toString();
            while (s.length < width) { s = '0' + s; }
            return s;
        };
        const timestamp =
            `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}.${pad(now.getMilliseconds(), 3)}`;

        this.outputChannel.appendLine(`[${timestamp}] ${message}`);
    }

    /// Called by ServerErrorHandler once the server has exited too many times in a row to keep
    /// restarting it. The default handler just stops, leaving the extension silently dead until
    /// the whole window is reloaded - which is a miserable outcome for the most common cause,
    /// namely the extension having just been updated underneath a running server.
    public onServerStoppedRestarting() {
        this.setSessionStatus("HLSL Tools (stopped)", SessionStatus.Failed);
        this.log('The language server exited repeatedly, so it will not be restarted automatically.');

        vscode.window
            .showWarningMessage(
                'HLSL Tools stopped: the language server exited several times in a row. ' +
                'If the extension was just updated or reinstalled, restarting is enough.',
                'Restart', 'Show Log')
            .then(choice => {
                if (choice === 'Restart') {
                    this.restartSession();
                } else if (choice === 'Show Log') {
                    this.outputChannel.show(true);
                }
            });
    }

    public stop(): Promise<void> {
        if (this.sessionStatus === SessionStatus.Failed) {
            // Before moving further, clear out the client and process if
            // the process is already dead (i.e. it crashed)
            this.languageServerClient = undefined;
        }

        this.sessionStatus = SessionStatus.Stopping;

        var promise = Promise.resolve();

        if (this.directiveToggleProvider !== undefined) {
            this.directiveToggleProvider.dispose();
            this.directiveToggleProvider = undefined;
        }

        // Close the language server client
        if (this.languageServerClient !== undefined) {
            promise = this.languageServerClient.stop();
            this.languageServerClient = undefined;
        }

        this.sessionStatus = SessionStatus.NotStarted;
        
        return promise;
    }

    public dispose() : void {
        // Stop the current session
        this.stop();

        // Dispose of all commands
        this.registeredCommands.forEach(command => { command.dispose(); });
    }

    private registerCommands() : void {
        this.registeredCommands = [
            vscode.commands.registerCommand('ShaderTools.RestartSession', () => { this.restartSession(); }),
            vscode.commands.registerCommand(this.ShowSessionMenuCommandName, () => { this.showSessionMenu(); })
        ]
    }

    private startEditorServices() {
        try {
            this.setSessionStatus(
                "Starting HLSL Tools...",
                SessionStatus.Initializing);

            var serverPath = this.getServerPath();
            var serverExe = path.resolve(__dirname, `../bin/${serverPath}`);

            this.log(`Extension version ${this.getExtensionVersion()}, ${this.platform}.`);
            this.log(`Language server: ${serverExe}`);

            if (!fs.existsSync(serverExe)) {
                this.setSessionFailure(
                    'The language server executable is missing. The extension package is incomplete - try reinstalling it.',
                    serverExe);
                return;
            }

            var startArgs = [ ];
            //startArgs.push("--logfilepath", editorServicesLogPath);

            var debugArgs = startArgs.slice(0);
            debugArgs.push("--launch-debugger");

            let serverOptions: ServerOptions = {
                run: { command: serverExe, args: startArgs },
                debug: {command: serverExe, args: debugArgs }
            };

            let clientOptions: LanguageClientOptions = {
                documentSelector: LanguageIds,
                synchronize: {
                    configurationSection: LanguageIds
                },
                // Errors are still written to the output channel, but don't steal focus by
                // revealing the panel every time a request fails.
                revealOutputChannelOn: RevealOutputChannelOn.Never,
                outputChannel: this.outputChannel,
                errorHandler: new ServerErrorHandler(this)
            };

            this.languageServerClient =
                new LanguageClient(
                    'hlsl-client',
                    'HLSL Tools Language Client',
                    serverOptions,
                    clientOptions);

            this.languageServerClient.onReady().then(
                () => {
                    this.setSessionStatus(
                        'HLSL Tools',
                        SessionStatus.Running);

                    this.log('Language server is ready.');

                    this.directiveToggleProvider = new DirectiveToggleProvider(
                        this.languageServerClient,
                        relativePath => this.extensionContext.asAbsolutePath(relativePath));

                    this.ensureToggleDefineCommand();
                },
                (reason) => {
                    this.setSessionFailure('Could not start the language server.', reason);
                });

            this.log('Starting the language server...');
            this.languageServerClient.start();
        } catch (e)
        {
            this.setSessionFailure('The language server could not be started.', e);
        }
    }

    // The language client normally auto-registers commands declared by the server's
    // executeCommandProvider capability. If that didn't happen for any reason, register a
    // forwarder ourselves so hover/command links always work.
    private ensureToggleDefineCommand() {
        const client = this.languageServerClient;

        vscode.commands.getCommands(true).then(existingCommands => {
            if (existingCommands.indexOf('hlslTools.toggleDefine') !== -1) {
                return;
            }

            this.registeredCommands.push(
                vscode.commands.registerCommand('hlslTools.toggleDefine', (...args: any[]) => {
                    return client.sendRequest('workspace/executeCommand', {
                        command: 'hlslTools.toggleDefine',
                        arguments: args
                    });
                }));
        });
    }

    private getServerPath() {
        switch (this.platform) {
            case "win32":
                return "win-x64/ShaderTools.LanguageServer.exe";

            case "darwin":
                return "osx-x64/ShaderTools.LanguageServer";

            default:
                throw `Platform ${this.platform} is not currently supported by HLSL Tools.`;
        }
    }

    private restartSession() {
        this.stop();
        this.start();
    }

    private createStatusBarItem() {
        if (this.statusBarItem == undefined) {
            // Create the status bar item and place it right next
            // to the language indicator
            this.statusBarItem =
                vscode.window.createStatusBarItem(
                    vscode.StatusBarAlignment.Right,
                    1);

            this.statusBarItem.command = this.ShowSessionMenuCommandName;
            this.statusBarItem.show();
            vscode.window.onDidChangeActiveTextEditor(textEditor => {
                if (textEditor === undefined
                    || LanguageIds.indexOf(textEditor.document.languageId) === -1) {
                    this.statusBarItem.hide();
                }
                else {
                    this.statusBarItem.show();
                }
            })
        }
    }

    private setSessionStatus(statusText: string, status: SessionStatus): void {
        var statusIconText = "$(code) ";
        var statusColor = "#affc74";

        if (status == SessionStatus.Initializing) {
            statusIconText = "$(sync) ";
            statusColor = "#f3fc74";
        }
        else if (status == SessionStatus.Failed) {
            statusIconText = "$(alert) ";
            statusColor = "#fcc174";
        }

        this.sessionStatus = status;
        this.statusBarItem.color = statusColor;
        this.statusBarItem.text = statusIconText + statusText;
    }

    private setSessionFailure(message: string, ...additionalMessages: any[]) {
        this.setSessionStatus(
            "HLSL Tools Initialization Error",
            SessionStatus.Failed);

        // These used to be dropped on the floor, which made a failed start indistinguishable
        // from a working one - nothing in the log, nothing on screen.
        this.log(`ERROR: ${message}`);

        for (const additional of additionalMessages) {
            if (additional === undefined || additional === null) {
                continue;
            }

            this.log(additional.stack ? additional.stack.toString() : additional.toString());
        }

        vscode.window
            .showErrorMessage(`HLSL Tools: ${message}`, 'Show Log')
            .then(choice => {
                if (choice === 'Show Log') {
                    this.outputChannel.show(true);
                }
            });
    }

    private getExtensionVersion(): string {
        // context.extension needs a newer @types/vscode than this extension targets.
        try {
            const packageJsonPath = path.resolve(__dirname, '../package.json');
            return JSON.parse(fs.readFileSync(packageJsonPath, 'utf8')).version;
        } catch (e) {
            return 'unknown';
        }
    }

    private showSessionMenu() {
        var menuItems: SessionMenuItem[] = [];

        if (this.sessionStatus === SessionStatus.Initializing ||
            this.sessionStatus === SessionStatus.NotStarted ||
            this.sessionStatus === SessionStatus.Stopping) {

            // Don't show a menu for these states
            return;
        }

        if (this.sessionStatus === SessionStatus.Running) {
            menuItems = [
                new SessionMenuItem(
                    "Restart Current Session",
                    () => { this.restartSession(); }),
                new SessionMenuItem(
                    "Show Log",
                    () => { this.outputChannel.show(true); }),
            ];
        }
        else if (this.sessionStatus === SessionStatus.Failed) {
            menuItems = [
                new SessionMenuItem(
                    "Session initialization failed - show log",
                    () => { this.outputChannel.show(true); }),
                new SessionMenuItem(
                    "Restart Current Session",
                    () => { this.restartSession(); }),
            ];
        }

        vscode
            .window
            .showQuickPick<SessionMenuItem>(menuItems)
            // selectedItem is undefined when the quick pick is dismissed.
            .then((selectedItem) => { if (selectedItem) { selectedItem.callback(); } });
    }
}

/**
 * Restarts the server when it exits, up to a limit, then hands over to the SessionManager so the
 * user is told about it and offered a one-click restart. The stock handler does the counting but
 * then dies quietly, which is indistinguishable from the extension never having loaded.
 */
class ServerErrorHandler implements ErrorHandler {
    private static readonly MaxRestarts = 5;
    private static readonly RestartWindowMs = 3 * 60 * 1000;

    private restarts: number[] = [];

    constructor(private readonly session: SessionManager) { }

    public error(error: Error, message: Message, count: number): ErrorAction {
        if (count <= 3) {
            return ErrorAction.Continue;
        }

        this.session.log(`Shutting the language server down after ${count} connection errors: ${error.message}`);
        return ErrorAction.Shutdown;
    }

    public closed(): CloseAction {
        const now = Date.now();

        this.restarts.push(now);
        this.restarts = this.restarts.filter(t => now - t < ServerErrorHandler.RestartWindowMs);

        if (this.restarts.length <= ServerErrorHandler.MaxRestarts) {
            this.session.log(
                `The language server exited - restarting it ` +
                `(${this.restarts.length}/${ServerErrorHandler.MaxRestarts}).`);
            return CloseAction.Restart;
        }

        // Start the count over, so a manual restart isn't immediately capped again.
        this.restarts = [];
        this.session.onServerStoppedRestarting();
        return CloseAction.DoNotRestart;
    }
}

class SessionMenuItem implements vscode.QuickPickItem {
    public description: string;

    constructor(
        public readonly label: string,
        public readonly callback: () => void = () => { })
    {
    }
}