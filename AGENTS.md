# Coding Refactoring Tool Guidelines

This repository implements a Model Context Protocol (MCP) server that exposes **C#** refactoring tools. When adding new tools or modifying existing ones, follow these guidelines.

## Model Context Protocol Overview

The Model Context Protocol (MCP) is a communication protocol designed for AI-assisted coding tools. This project leverages the MCP C# SDK to provide structured interaction between AI models and our refactoring capabilities. Key components include:

- **IMcpEndpoint**: The core interface for client-server communication
- **McpErrorCode**: Standard error codes for consistent error handling
- **McpJsonUtilities**: Utilities for JSON processing within MCP

## MCP Component Usage Guidelines

When implementing functionality in this project, use these MCP components appropriately:

- **Tools**: Use for discrete, actionable operations that transform code (refactorings, analysis tasks)
  - Implemented as static classes with `[McpServerToolType]` attribute
  - Methods have `[McpServerTool]` attribute with descriptive parameters
  - Can handle various parameter types including CancellationToken, IServiceProvider, IMcpServer, and IProgress<T>
  - Return values are automatically converted to appropriate McpToolResponse objects
  - Examples: Extract Method, Rename Symbol, Move Method

- **Resources**: Use for providing contextual information or read-only data
  - Implemented as static classes with `[McpServerResourceType]` attribute
  - Methods have `[McpServerResource]` attribute with descriptive parameters
  - Can represent both direct resources (e.g. "resource://example") and templated resources (e.g. "resource://example/{id}")
  - Support URI-based parameter binding for templated resources
  - Examples: Class metrics, method dependencies, refactoring opportunities

- **Prompts**: Use for guiding AI with specific instructions or context
  - Implemented as static classes with `[McpServerPromptType]` attribute
  - Methods have `[McpServerPrompt]` attribute with descriptive parameters
  - Help shape AI behavior for specific refactoring scenarios
  - Provide domain knowledge or project-specific guidance
  - Examples: Style guidelines, architecture constraints, naming conventions

## MCP Attribute Usage

### Tool Attributes

```csharp
[McpServerToolType] // Applied to the class containing tool methods
public static class ExampleTool
{
    [McpServerTool] // Applied to each method implementing a tool
    [Description("Performs a specific refactoring operation")] // Top-level tool description
    public static async Task<string> ToolName(
        [Description("First parameter description")] string param1,
        [Description("Second parameter description")] int param2,
        CancellationToken cancellationToken = default)
    {
        // Protocol-level validation
        if (string.IsNullOrEmpty(param1))
        {
            throw new McpException("Parameter cannot be empty", McpErrorCode.InvalidParams);
        }

        // Business logic validation - return error as string
        if (param2 < 0)
        {
            return "Error: Value must be non-negative";
        }

        // Success case
        return "Operation completed successfully";
    }
}
```

- **`[McpServerToolType]`**: Marks a class as containing MCP tool methods
- **`[McpServerTool]`**: Marks a method as an MCP tool operation
- **`[Description]`**: Provides documentation for the tool and its parameters
- Return a string for both success and business-level errors
- For protocol-level errors only, throw McpException with appropriate McpErrorCode:
  - `InvalidParams`: When parameters don't meet requirements
  - `InvalidRequest`: When request format is incorrect
  - `MethodNotFound`: When operation doesn't exist
  - `ParseError`: When input cannot be parsed
  - `InternalError`: For unexpected internal failures
- Special parameter types:
  - `CancellationToken`: Automatically bound to the request's cancellation token
  - `IServiceProvider`: Bound from the request context
  - `IMcpServer`: Bound to the current server instance
  - `IProgress<T>`: For progress notifications to the client

### Resource Attributes

```csharp
[McpServerResourceType] // Applied to the class containing resource methods
public static class ExampleResource
{
    [McpServerResource] // Applied to each method implementing a resource
    [Description("Provides specific information about the code")] // Resource description
    public static async Task<object> ResourceName(
        [Description("Input parameter description")] string input,
        CancellationToken cancellationToken = default)
    {
        // Protocol-level validation
        if (string.IsNullOrEmpty(input))
        {
            throw new McpException("Input parameter cannot be empty", McpErrorCode.InvalidParams);
        }

        // Business logic validation - return error as string
        if (!input.StartsWith("resource://"))
        {
            return "Error: Invalid resource URI format";
        }

        // Success case
        object data = await GetResourceDataAsync(input, cancellationToken);
        return data;
    }
}
```

- **`[McpServerResourceType]`**: Marks a class as containing MCP resource methods
- **`[McpServerResource]`**: Marks a method as an MCP resource provider
- **`[Description]`**: Documents the resource and its parameters
- Return an object for success, string for business-level errors
- For protocol-level errors only, throw McpException with appropriate McpErrorCode
- Support for URI-based parameter binding in templated resources

### Prompt Attributes

```csharp
[McpServerPromptType] // Applied to the class containing prompt methods
public static class ExamplePrompts
{
    [McpServerPrompt] // Applied to each method implementing a prompt
    [Description("Provides guidance for a specific scenario")] // Prompt description
    public static async Task<string> PromptName(
        [Description("Context parameter")] string context,
        CancellationToken cancellationToken = default)
    {
        // Protocol-level validation
        if (string.IsNullOrEmpty(context))
        {
            throw new McpException("Context parameter cannot be empty", McpErrorCode.InvalidParams);
        }

        // Business logic validation - return error as string
        if (context.Length > 1000)
        {
            return "Error: Context is too long (max 1000 characters)";
        }

        // Success case
        string guidance = await GenerateGuidanceAsync(context, cancellationToken);
        return guidance;
    }
}
```

- **`[McpServerPromptType]`**: Marks a class as containing MCP prompt methods
- **`[McpServerPrompt]`**: Marks a method as an MCP prompt provider
- **`[Description]`**: Explains the prompt's purpose and its parameters
- Return a string for both success and business-level errors
- For protocol-level errors only, throw McpException with appropriate McpErrorCode

## Error Handling in MCP

The MCP framework provides structured error handling mechanisms that should be used consistently:

### Error Response vs. Exceptions

- **Return Error Responses**: For expected error conditions that clients should handle
  ```csharp
  // Tool method returning an error response for invalid input
  public static McpToolResponse ExtractMethod(string code, string methodName)
  {
      if (string.IsNullOrEmpty(methodName))
      {
          return new McpToolResponse.Error("Method name cannot be empty", McpErrorCode.InvalidParams);
      }
      // Implementation continues...
  }
  ```

- **Throw McpException**: For protocol-level errors or unexpected conditions
  ```csharp
  // Throwing an exception for protocol violations
  if (context == null)
  {
      throw new McpException("Context object cannot be null", McpErrorCode.InvalidRequest);
  }
  ```

### Common McpErrorCode Values

Use these standard error codes appropriately:

- **`McpErrorCode.InvalidParams`**: When tool parameters don't meet requirements
- **`McpErrorCode.InvalidRequest`**: When the request format is incorrect
- **`McpErrorCode.MethodNotFound`**: When the requested operation doesn't exist
- **`McpErrorCode.ParseError`**: When input cannot be parsed
- **`McpErrorCode.ResourceNotFound`**: When a requested resource is unavailable
- **`McpErrorCode.InternalError`**: For unexpected internal failures

### Error Message Guidelines

- Be specific about what went wrong
- Suggest potential fixes when possible
- Include context information that helps diagnose the issue
- Use consistent terminology across error messages

Example with actionable guidance:
```csharp
// Good error message with guidance
return new McpToolResponse.Error(
    "Failed to extract method: Selected code contains references to local variables outside the selection. " +
    "Consider including those variables in your selection or passing them as parameters.",
    McpErrorCode.InvalidParams);
```

## Project Architecture

- **Core Components**: The project is built around Roslyn (Microsoft's .NET compiler platform) to perform code analysis and transformations.
- **Rewriters and Walkers**: We use Roslyn's `SyntaxRewriter` and `SyntaxWalker` classes as foundational building blocks.
  - `SyntaxWalker`: Used to analyze code without modifying it
  - `SyntaxRewriter`: Used to transform code by producing new syntax trees
- **Tools**: Higher-level agents composed of multiple rewriters/walkers to implement specific refactoring operations.
  
## Creating a New Tool

1. Add a new static class in the `RefactorMCP.ConsoleApp/Tools/` directory and decorate it with `[McpServerToolType]`.
2. Inside that class, add a static method decorated with `[McpServerTool]` and include a `[Description]` for every parameter so clients can display helpful text.
3. Keep method names concise and use `CamelCase`.
4. For complex logic, extract helper functions rather than writing large methods.
5. Use existing Roslyn Syntax Rewriters and Walkers when possible - compose them to build your tool.

## Tool Design Principles

- **Simplicity**: Prefer smaller, focused tools over complex ones with many parameters.
- **Descriptive Parameters**: Include clear descriptions for all parameters.
- **Error Guidance**: When a tool fails, provide specific error messages explaining:
  - What caused the failure
  - Potential solutions or alternatives
  - Any prerequisites that were missing
- **Composition**: Design tools to be composable with other tools when possible.

## Updating Documentation

- Document the new agent in **README.md** and **EXAMPLES.md**.
- Provide a short usage snippet in **QUICK_REFERENCE.md**.
- Include a minimal example in the test suite under `RefactorMCP.Tests`.

## Formatting and Testing

- Always run tests from the project root: `dotnet test`
- Format your code before committing: `dotnet format`
- Run the full test suite before submitting changes

The build will happen automatically when running tests. There's no need to include `--no-build` or other build parameters.

