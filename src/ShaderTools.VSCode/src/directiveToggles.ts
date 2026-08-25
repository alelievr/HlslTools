'use strict';

import vscode = require('vscode');
import { LanguageClient } from 'vscode-languageclient';

interface DirectiveMacro {
    name: string;
    definedInConfig: boolean;
}

interface ConditionalDirective {
    range: { start: { line: number; character: number }, end: { line: number; character: number } };
    branchTaken: boolean;
    macros: DirectiveMacro[];
}

interface ConditionalDirectivesParams {
    uri: string;
    directives: ConditionalDirective[];
}

/**
 * Renders a gutter icon on every #if / #ifdef / #ifndef / #elif line. Hovering the line
 * (or the icon) shows a popup with links that toggle the referenced macros in
 * shadertoolsconfig.json, via the server's hlslTools.toggleDefine command.
 */
export class DirectiveToggleProvider implements vscode.Disposable {

    private directivesByPath = new Map<string, ConditionalDirective[]>();
    private decorationType: vscode.TextEditorDecorationType;
    private disposables: vscode.Disposable[] = [];

    constructor(client: LanguageClient, extensionPath: (relativePath: string) => string) {
        this.decorationType = vscode.window.createTextEditorDecorationType({
            light: { gutterIconPath: extensionPath('media/toggle-define-light.svg') },
            dark: { gutterIconPath: extensionPath('media/toggle-define-dark.svg') },
            gutterIconSize: '75%'
        });

        client.onNotification('hlslTools/conditionalDirectives', (params: ConditionalDirectivesParams) => {
            const fsPath = vscode.Uri.parse(params.uri).fsPath.toLowerCase();
            this.directivesByPath.set(fsPath, params.directives);
            this.refreshVisibleEditors();
        });

        this.disposables.push(vscode.window.onDidChangeActiveTextEditor(() => this.refreshVisibleEditors()));
        this.disposables.push(vscode.window.onDidChangeVisibleTextEditors(() => this.refreshVisibleEditors()));

        this.refreshVisibleEditors();
    }

    private refreshVisibleEditors() {
        for (const editor of vscode.window.visibleTextEditors) {
            this.applyDecorations(editor);
        }
    }

    private applyDecorations(editor: vscode.TextEditor) {
        if (editor.document.languageId !== 'hlsl') {
            return;
        }

        const directives = this.directivesByPath.get(editor.document.uri.fsPath.toLowerCase());
        if (directives === undefined) {
            return;
        }

        const documentUri = editor.document.uri.toString();

        const options: vscode.DecorationOptions[] = directives.map(directive => {
            const branchState = directive.branchTaken ? 'active' : 'inactive';

            let markdown = `**Preprocessor branch ${branchState}**`;
            for (const macro of directive.macros) {
                const commandArgs = encodeURIComponent(JSON.stringify([documentUri, macro.name]));
                const action = macro.definedInConfig ? 'Undefine' : 'Define';
                const state = macro.definedInConfig ? 'defined' : 'not defined';
                markdown += `\n\n[${action} \`${macro.name}\`](command:hlslTools.toggleDefine?${commandArgs}) — currently ${state} in shadertoolsconfig.json`;
            }

            const hoverMessage = new vscode.MarkdownString(markdown);
            hoverMessage.isTrusted = true;

            return {
                range: new vscode.Range(
                    directive.range.start.line, directive.range.start.character,
                    directive.range.end.line, directive.range.end.character),
                hoverMessage: hoverMessage
            };
        });

        editor.setDecorations(this.decorationType, options);
    }

    public dispose() {
        this.decorationType.dispose();
        this.disposables.forEach(d => d.dispose());
        this.directivesByPath.clear();
    }
}
