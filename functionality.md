# RefactorMCP

A Model Context Protocol (MCP) server providing automated refactoring tools for C# code transformation.

Using:
- Microsoft.CodeAnalysis.CSharp (4.14.0), 
- Microsoft.CodeAnalysis.CSharp.Workspaces (4.14.0), 
- Microsoft.CodeAnalysis.CSharp.MSBuild
- ModelContextProtocol (0.2.0-preview.3)

## Available Refactorings

These refactorings are focused on maintaining the public API and moving the code as little as possible from its original place to aid review.

### Extract Method
Creates a new method from selected code and replaces the original with a method call. Specify ranges using `line:column-line:column` format.

### Introduce Field/Parameter/Variable
Creates a new field, parameter, or variable from selected code. Use `line:column-line:column` ranges to specify the target code. Introducing a field fails if the class already defines a field with the same name.

### Convert to Static
**With Parameters**: Transforms instance methods to static by converting instance dependencies into method parameters.

**With Instance Parameter**: Transforms instance methods to static by adding an instance parameter to replace `this` references.

### Move Static Method
Relocates a static method to another class (new or existing). A wrapper method is left in the source class to delegate to the moved implementation.

### Move Instance Method
Relocates an instance method to another class and introduces a variable, field, or property on the origin class to maintain access. The original method becomes a delegating wrapper so callers see no interface change.

### Make Field Readonly
Moves field initialization to all constructors and marks the field as readonly.

### Transform Setter to Init
Converts property setters to init-only setters and moves initialization to constructors.

### Constructor Injection
Converts method parameters to constructor-injected fields or properties.

### Safe Delete
Safely removes fields, parameters, or variables with dependency warnings before deletion.

### Extract Class
Creates a new class from selected fields and methods, establishing a composition relationship with the original class.

### Inline Method
Replaces method calls with the method's body content, removing the method definition.

### Use Interface
Changes a method parameter type to one of its implemented interfaces when possible.
