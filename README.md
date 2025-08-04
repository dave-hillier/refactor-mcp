# RefactorMCP

RefactorMCP is a Model Context Protocol server that exposes Roslyn-based refactoring tools for C#.

Run the console application directly or host it as an MCP server:

## Setup as MCP Server

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed
- Clone this repository and build the project:
```bash
git clone <repository-url>
cd refactor-mcp
dotnet build RefactorMCP.ConsoleApp
```
### Claude Desktop Integration

1. **Locate your Claude Desktop config file:**
   - **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
   - **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - **Linux**: `~/.config/Claude/claude_desktop_config.json`

2. **Add RefactorMCP to your config:**
```json
{
  "mcpServers": {
    "refactor-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/refactor-mcp/RefactorMCP.ConsoleApp"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```
**Important**: Replace `/absolute/path/to/refactor-mcp/` with the actual absolute path to your cloned repository.

3. **Restart Claude Desktop** for the changes to take effect.

4. **Verify the connection:**
   - Open a new conversation in Claude Desktop
   - You should see a small hammer/tool icon (🔨) indicating MCP tools are available
   - Ask Claude: "What refactoring tools do you have available?"

### VS Code with GitHub Copilot Integration

RefactorMCP works seamlessly with GitHub Copilot in VS Code through MCP support:

1. **Install GitHub Copilot extension** if not already installed from the VS Code marketplace

2. **Configure RefactorMCP in VS Code settings:**
   - Open VS Code settings (`Ctrl/Cmd + ,`)
   - Search for "MCP" or go to Extensions > GitHub Copilot > MCP Servers
   - Add a new MCP server configuration:
```json
{
  "github.copilot.chat.mcp.servers": {
    "refactor-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/refactor-mcp/RefactorMCP.ConsoleApp"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```
**Important**: Replace `/absolute/path/to/refactor-mcp/` with the actual absolute path to your cloned repository.

3. **Restart VS Code** for the configuration to take effect.

4. **Usage in VS Code:**
   - Open the GitHub Copilot Chat panel (`Ctrl/Cmd + Shift + I`)
   - Use natural language to request refactoring operations:
     - "Move the `CalculateTotal` method from `OrderService` to `PriceCalculator`"
     - "Extract this complex logic into a separate method"
     - "Convert this instance method to static"
     - "Show me metrics for the current C# file"

     5. **Verify the integration:**
   - In Copilot Chat, ask: "What refactoring tools are available?"
   - You should see a list of all RefactorMCP tools
   - The tools will work directly on your open workspace files

## Direct Usage

```bash
dotnet run --project RefactorMCP.ConsoleApp
```

For usage examples see [EXAMPLES.md](./EXAMPLES.md).

## Available Refactorings

- **Extract Method** – create a new method from selected code and replace the original with a call.
- **Introduce Field/Parameter/Variable** – turn expressions into new members; fails if a field already exists.
- **Convert to Static** – make instance methods static using parameters or an instance argument.
- **Move Static Method** – relocate a static method and keep a wrapper in the original class.
- **Move Instance Method** – move one or more instance methods to another class and delegate from the source. If a moved method no longer accesses instance members, it is made static automatically. Provide a `methodNames` list along with optional `constructor-injections` and `parameter-injections` to control dependencies.
- **Move Multiple Methods (instance)** – move several methods and keep them as instance members of the target class. The source instance is injected via the constructor when required.
- **Move Multiple Methods (static)** – move multiple methods and convert them to static, adding a `this` parameter.
- **Make Static Then Move** – convert an instance method to static and relocate it to another class in one step.
- **Move Type to Separate File** – move a top-level type into its own file named after the type.
- **Make Field Readonly** – move initialization into constructors and mark the field readonly.
- **Transform Setter to Init** – convert property setters to init-only and initialize in constructors.
- **Constructor Injection** – convert method parameters to constructor-injected fields or properties.
- **Safe Delete** – remove fields or variables only after dependency checks.
- **Extract Class** – create a new class from selected members and compose it with the original.
- **Inline Method** – replace calls with the method body and delete the original.
- **Extract Decorator** – create a decorator class that delegates to an existing method.
- **Create Adapter** – generate an adapter class wrapping an existing method.
- **Add Observer** – introduce an event and raise it from a method.
- **Use Interface** – change a method parameter type to one of its implemented interfaces.
- **List Tools** – display all available refactoring tools as kebab-case names.

Metrics and summaries are also available via the `metrics://` and `summary://` resource schemes.

## Contributing

* Run `dotnet test` to ensure all tests pass.
* Format the code with `dotnet format` before opening a pull request.

## License

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
