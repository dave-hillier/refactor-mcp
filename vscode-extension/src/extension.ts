import * as vscode from 'vscode';
import { execFile } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';

function getWorkspaceFolder(): string | undefined {
    const folder = vscode.workspace.workspaceFolders?.[0];
    if (!folder) {
        vscode.window.showErrorMessage('RefactorMCP requires an open workspace containing RefactorMCP.ConsoleApp');
        return undefined;
    }
    return folder.uri.fsPath;
}

const executableName = process.platform === 'win32' ? 'RefactorMCP.ConsoleApp.exe' : 'RefactorMCP.ConsoleApp';

/// The built application, or the dotnet host with its dll when only that exists.
function resolveApplication(workspaceFolder: string): { command: string; leadingArgs: string[] } | undefined {
    const configured = vscode.workspace.getConfiguration().get<string>('refactorMcp.executablePath', '');
    if (configured && configured.length > 0) {
        return { command: configured, leadingArgs: [] };
    }

    const outputDirectory = path.join(workspaceFolder, 'RefactorMCP.ConsoleApp', 'bin', 'Debug', 'net9.0');
    const appHost = path.join(outputDirectory, executableName);
    if (fs.existsSync(appHost)) {
        return { command: appHost, leadingArgs: [] };
    }

    const dll = path.join(outputDirectory, 'RefactorMCP.ConsoleApp.dll');
    if (fs.existsSync(dll)) {
        const dotnetPath = vscode.workspace.getConfiguration().get<string>('refactorMcp.dotnetPath', 'dotnet');
        return { command: dotnetPath, leadingArgs: [dll] };
    }

    return undefined;
}

/// Runs a tool through the CLI, which serves the call from a daemon holding the
/// solution rather than loading it again for every invocation.
function runJson(toolName: string, json: string): Thenable<string> {
    const workspaceFolder = getWorkspaceFolder();
    if (!workspaceFolder) {
        return Promise.reject('No workspace');
    }

    const application = resolveApplication(workspaceFolder);
    if (!application) {
        vscode.window.showErrorMessage(
            'RefactorMCP is not built. Run "dotnet build" in the workspace, or set refactorMcp.executablePath.');
        return Promise.reject('RefactorMCP.ConsoleApp is not built');
    }

    const commandArgs = [...application.leadingArgs, '--json', toolName, json];
    return new Promise((resolve, reject) => {
        execFile(application.command, commandArgs, { cwd: workspaceFolder }, (err, stdout, stderr) => {
            if (err) {
                reject((stderr || err.message).trim());
            } else {
                resolve(stdout);
            }
        });
    });
}

async function getAvailableTools(): Promise<string[]> {
    try {
        const output = await runJson('ListTools', '{}');
        return output
            .split(/\r?\n/)
            .map(l => l.trim())
            .filter(l => l.length > 0);
    } catch {
        return [];
    }
}

function toPascalCase(name: string): string {
    return name
        .split('-')
        .map(part => part.charAt(0).toUpperCase() + part.slice(1))
        .join('');
}

export function activate(context: vscode.ExtensionContext) {
    const disposable = vscode.commands.registerCommand('refactorMcp.extractMethod', async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor) {
            return vscode.window.showWarningMessage('No active editor');
        }
        const document = editor.document.uri.fsPath;
        const selection = editor.selection;
        const start = documentPosition(selection.start);
        const end = documentPosition(selection.end);

        // Prefer the workspace solution, which lets the daemon serve the call;
        // without one the tool works on the single file.
        const solutionPath = (await findSolutionFile()) ?? '';

        const methodName = await vscode.window.showInputBox({ prompt: 'Name for the new method' });
        if (!methodName) {
            return;
        }

        const range = `${start}-${end}`;
        const json = JSON.stringify({ solutionPath, filePath: document, selectionRange: range, methodName });

        try {
            await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: 'RefactorMCP: Extract Method' }, async () => {
                const output = await runJson('ExtractMethod', json);
                vscode.window.showInformationMessage('RefactorMCP completed');
                console.log(output);
            });
        } catch (err: any) {
            vscode.window.showErrorMessage(`RefactorMCP failed: ${err}`);
        }
    });

    const runTool = vscode.commands.registerCommand('refactorMcp.runTool', async () => {
        const tools = await getAvailableTools();
        if (tools.length === 0) {
            vscode.window.showErrorMessage('Failed to retrieve tool list');
            return;
        }

        const toolPick = await vscode.window.showQuickPick(tools, { placeHolder: 'Select RefactorMCP tool' });
        if (!toolPick) {
            return;
        }

        const paramJson = await vscode.window.showInputBox({ prompt: 'Tool parameters as JSON' });
        if (paramJson === undefined) {
            return;
        }

        const pascal = toPascalCase(toolPick);
        try {
            await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: `RefactorMCP: ${toolPick}` }, async () => {
                const output = await runJson(pascal, paramJson);
                vscode.window.showInformationMessage('RefactorMCP completed');
                console.log(output);
            });
        } catch (err: any) {
            vscode.window.showErrorMessage(`RefactorMCP failed: ${err}`);
        }
    });

    context.subscriptions.push(runTool);

    context.subscriptions.push(disposable);
}

async function findSolutionFile(): Promise<string | undefined> {
    const folder = vscode.workspace.workspaceFolders?.[0];
    if (!folder) {
        return undefined;
    }

    const matches = await vscode.workspace.findFiles('**/*.{sln,slnx}', '**/node_modules/**', 1);
    return matches.length > 0 ? matches[0].fsPath : undefined;
}

function documentPosition(pos: vscode.Position): string {
    return `${pos.line + 1}:${pos.character + 1}`;
}

export function deactivate() {}
