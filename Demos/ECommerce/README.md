# RefactorMCP Live Demos — E-Commerce Refactoring Showcase

An end-to-end demonstration of **30 refactoring tools** applied to a realistic e-commerce codebase. Each demo starts from intentionally messy but realistic code and transforms it step-by-step.

## The Scenario

You've inherited a small e-commerce platform with typical code smells:

| File | What's Wrong |
|------|-------------|
| `OrderProcessor.cs` | God class — validation, pricing, payment, logging all mixed together. Unused methods and fields. Poorly named variables. |
| `PricingEngine.cs` | Unreadable one-liner expressions, pure functions trapped as instance methods, feature flag branching. |
| `CustomerService.cs` | Concrete dependencies instead of interfaces, method-level injection that should be constructor-level, unused parameters. |
| `NotificationService.cs` | No interface for testing, unused imports, utility methods that should be extension methods, types crammed into one file. |
| `ReportGenerator.cs` | Static utilities in the wrong class, unused local variables. |
| `InventoryManager.cs` | Poorly named parameters, no events for monitoring, no adapter for external APIs, mutable DTOs. |

## Demo Categories

| # | Category | Tools Demonstrated | Doc |
|---|----------|-------------------|-----|
| 1 | **Analysis & Metrics** | `analyze-refactoring-opportunities`, `list-class-lengths` | [demo-01](docs/demo-01-analysis.md) |
| 2 | **Method Transformation** | `extract-method`, `inline-method` | [demo-02](docs/demo-02-method-transformation.md) |
| 3 | **Introduce Variable/Field/Parameter** | `introduce-variable`, `introduce-field`, `introduce-parameter` | [demo-03](docs/demo-03-introduction.md) |
| 4 | **Method Moving** | `move-instance-method`, `move-static-method`, `move-to-separate-file` | [demo-04](docs/demo-04-method-moving.md) |
| 5 | **Conversion & DI** | `convert-to-static-with-parameters`, `convert-to-extension-method`, `use-interface`, `convert-to-constructor-injection`, `make-field-readonly`, `transform-setter-to-init` | [demo-05](docs/demo-05-conversion.md) |
| 6 | **Design Patterns** | `extract-interface`, `extract-decorator`, `create-adapter`, `add-observer`, `feature-flag-refactor` | [demo-06](docs/demo-06-design-patterns.md) |
| 7 | **Cleanup & Safe Deletion** | `safe-delete-method`, `safe-delete-field`, `safe-delete-parameter`, `safe-delete-variable`, `rename-symbol`, `cleanup-usings` | [demo-07](docs/demo-07-cleanup.md) |

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- RefactorMCP built from repo root: `dotnet build`

### Run All Demos

```bash
cd Demos/ECommerce
./run-demos.sh
```

### Run a Single Demo Category

```bash
./run-demos.sh 1    # Analysis only
./run-demos.sh 2    # Method transformation only
./run-demos.sh 5    # Conversion & DI only
```

### Reset Files to Original State

```bash
./run-demos.sh --reset
```

## Architecture of the Demo

```
ECommerce/
├── ECommerce.sln              # Solution file (required for solution-wide refactoring)
├── ECommerce/
│   ├── ECommerce.csproj       # .NET 9.0 library project
│   ├── Models.cs              # Domain models (Order, Customer, Product, etc.)
│   ├── OrderProcessor.cs      # God class — main refactoring target
│   ├── PricingEngine.cs       # Complex pricing logic
│   ├── CustomerService.cs     # Customer management with DI issues
│   ├── NotificationService.cs # Notifications — interface/decorator target
│   ├── ReportGenerator.cs     # Reports with misplaced static methods
│   ├── InventoryManager.cs    # Inventory with naming/pattern issues
│   ├── PaymentGateway.cs      # Payment processing
│   └── AuditLogger.cs         # Audit logging — move target
├── docs/                      # Detailed before/after documentation
│   ├── demo-01-analysis.md
│   ├── demo-02-method-transformation.md
│   ├── demo-03-introduction.md
│   ├── demo-04-method-moving.md
│   ├── demo-05-conversion.md
│   ├── demo-06-design-patterns.md
│   └── demo-07-cleanup.md
├── run-demos.sh               # Master demo runner script
└── README.md                  # This file
```

## Demo Flow

Each demo category follows this pattern:

1. **Reset** — Source files restored to their original "messy" state
2. **Before** — Show the problematic code section
3. **Refactor** — Run the RefactorMCP tool via CLI
4. **After** — Show the transformed code
5. **Verify** — Project still compiles

The runner script handles reset between categories automatically, so each demo starts clean.

## What Makes These Demos Realistic

- **Real domain logic**: Order processing, pricing tiers, shipping calculations, inventory management
- **Genuine code smells**: Feature envy, god classes, magic numbers, dead code — not contrived examples
- **Interdependencies**: Moving a method requires tracking which fields/parameters it accesses
- **Progressive improvement**: Demos can be applied sequentially to watch the codebase improve

## Individual Tool Reference

Each demo doc contains the exact CLI command, before/after code, and explanation of benefits. See the [docs/](docs/) directory for detailed walkthroughs.
