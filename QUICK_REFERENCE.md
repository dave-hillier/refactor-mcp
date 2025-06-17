# Tool Quick Reference

This document provides a terse overview of the available MCP tools.

- **LoadSolutionTool** – `LoadSolution(string solutionPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)`
- **MoveMethodsTool.MoveStaticMethod** – `MoveStaticMethod(string solutionPath, string filePath, string methodName, string targetClass, string? targetFilePath = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)`
- **MoveMethodsTool.MoveInstanceMethod** – `MoveInstanceMethod(string solutionPath, string filePath, string sourceClass, string methodNames, string targetClass, string? targetFilePath = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)`

All other tools continue to operate as before.

