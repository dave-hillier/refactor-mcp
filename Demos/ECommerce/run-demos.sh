#!/usr/bin/env bash
#
# RefactorMCP Demo Runner
# =======================
# Runs all refactoring demos against the ECommerce sample project.
# Each demo starts from a fresh copy of the source files.
#
# Prerequisites:
#   - .NET 9.0 SDK installed
#   - RefactorMCP built: dotnet build (from repo root)
#
# Usage:
#   ./run-demos.sh              # Run all demos
#   ./run-demos.sh <number>     # Run a specific demo (1-7)
#   ./run-demos.sh --reset      # Reset source files to originals
#

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TOOL="dotnet run --project $REPO_ROOT/RefactorMCP.ConsoleApp --"
SLN="$SCRIPT_DIR/ECommerce.sln"
SRC="$SCRIPT_DIR/ECommerce"
ORIGINALS="$SCRIPT_DIR/originals"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

banner() {
    echo ""
    echo -e "${BLUE}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║${BOLD}  $1${NC}${BLUE}$(printf '%*s' $((60 - ${#1})) '')║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

step() {
    echo -e "  ${CYAN}▸${NC} ${BOLD}$1${NC}"
}

show_before() {
    echo -e "  ${YELLOW}[BEFORE]${NC} $1"
}

show_after() {
    echo -e "  ${GREEN}[AFTER]${NC} $1"
}

run_tool() {
    local tool_name="$1"
    local params="$2"
    echo -e "  ${CYAN}⚡ Running:${NC} $tool_name"
    echo -e "     ${CYAN}Params:${NC} $params"
    $TOOL --json "$tool_name" "$params"
    echo ""
}

# ── Setup: save originals on first run ─────────────────────────────

setup_originals() {
    if [ ! -d "$ORIGINALS" ]; then
        echo -e "${YELLOW}Saving original source files...${NC}"
        mkdir -p "$ORIGINALS"
        cp "$SRC"/*.cs "$ORIGINALS/"
    fi
}

reset_files() {
    echo -e "${YELLOW}Resetting source files to originals...${NC}"
    cp "$ORIGINALS"/*.cs "$SRC/"
    echo -e "${GREEN}Done.${NC}"
}

# ── Demo 1: Analysis & Metrics ────────────────────────────────────

demo_1_analysis() {
    banner "Demo 1: Analysis & Code Metrics"

    step "1a. Analyze OrderProcessor.cs for refactoring opportunities"
    run_tool "analyze-refactoring-opportunities" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\"
    }"

    step "1b. Measure class lengths across the entire solution"
    run_tool "list-class-lengths" "{
        \"solutionPath\": \"$SLN\"
    }"

    step "1c. Analyze PricingEngine.cs for code smells"
    run_tool "analyze-refactoring-opportunities" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\"
    }"
}

# ── Demo 2: Method Transformation (Extract & Inline) ──────────────

demo_2_method_transformation() {
    banner "Demo 2: Extract Method & Inline Method"

    step "2a. Extract validation logic from OrderProcessor.ProcessOrder (lines 47-68)"
    show_before "ProcessOrder is ~80 lines with validation, pricing, payment, and notifications mixed together"
    run_tool "extract-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"selectionRange\": \"47:9-68:100\",
        \"methodName\": \"ValidateOrder\"
    }"
    show_after "Validation is now in a clean ValidateOrder method, ProcessOrder calls it"

    step "2b. Inline the trivial GetBaseMultiplier wrapper in PricingEngine"
    show_before "GetBaseMultiplier is a one-liner called in one place"
    run_tool "inline-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\",
        \"methodName\": \"GetBaseMultiplier\"
    }"
    show_after "GetBaseMultiplier body is inlined at call sites, method removed"
}

# ── Demo 3: Introduce Variable, Field & Parameter ─────────────────

demo_3_introduction() {
    banner "Demo 3: Introduce Variable, Field & Parameter"

    step "3a. Introduce variable for complex pricing expression in PricingEngine"
    show_before "CalculateLineTotal has an unreadable one-line expression"
    run_tool "introduce-variable" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\",
        \"selectionRange\": \"31:16-31:65\",
        \"variableName\": \"tierDiscount\"
    }"
    show_after "Complex ternary extracted into a named 'tierDiscount' variable"

    step "3b. Introduce parameter for hardcoded tax rate in OrderProcessor"
    show_before "Tax rate 0.08m is hardcoded in ProcessOrder"
    run_tool "introduce-parameter" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"methodName\": \"ProcessOrder\",
        \"selectionRange\": \"93:40-93:44\",
        \"parameterName\": \"taxRate\"
    }"
    show_after "0.08m is now a 'taxRate' parameter, callers updated"

    step "3c. Introduce field for max discount cap in PricingEngine"
    show_before "0.25m max discount cap is a magic number"
    run_tool "introduce-field" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\",
        \"selectionRange\": \"114:60-114:64\",
        \"fieldName\": \"MaxDiscountCap\"
    }"
    show_after "Magic number extracted to _maxDiscountCap field"
}

# ── Demo 4: Method Moving ─────────────────────────────────────────

demo_4_method_moving() {
    banner "Demo 4: Move Methods Between Classes"

    step "4a. Move FormatAuditLogEntry from OrderProcessor to AuditLogger"
    show_before "FormatAuditLogEntry is in OrderProcessor but logically belongs in AuditLogger"
    run_tool "move-instance-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"sourceClass\": \"OrderProcessor\",
        \"methodNames\": [\"FormatAuditLogEntry\"],
        \"targetClass\": \"AuditLogger\",
        \"targetFilePath\": \"$SRC/AuditLogger.cs\"
    }"
    show_after "FormatAuditLogEntry moved to AuditLogger, OrderProcessor calls through _auditLogger"

    step "4b. Move static FormatAsTable from ReportGenerator to TableFormatter"
    show_before "FormatAsTable is a generic table utility living in ReportGenerator"
    run_tool "move-static-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/ReportGenerator.cs\",
        \"methodName\": \"FormatAsTable\",
        \"targetClass\": \"TableFormatter\",
        \"targetFilePath\": \"$SRC/ReportGenerator.cs\"
    }"
    show_after "FormatAsTable now lives in TableFormatter class"

    step "4c. Move NotificationTemplate to its own file"
    show_before "NotificationTemplate is defined in NotificationService.cs"
    run_tool "move-to-separate-file" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/NotificationService.cs\",
        \"typeName\": \"NotificationTemplate\"
    }"
    show_after "NotificationTemplate is now in its own NotificationTemplate.cs file"
}

# ── Demo 5: Conversion, DI & Interfaces ──────────────────────────

demo_5_conversion() {
    banner "Demo 5: Static Conversion, DI & Interface Usage"

    step "5a. Convert PricingEngine.CalculateShippingCost to static (uses no instance state)"
    show_before "CalculateShippingCost is an instance method but uses no fields"
    run_tool "convert-to-static-with-parameters" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\",
        \"methodName\": \"CalculateShippingCost\"
    }"
    show_after "CalculateShippingCost is now a static method"

    step "5b. Convert NotificationService.FormatCurrency to extension method on decimal"
    show_before "FormatCurrency takes a decimal and uses no instance state"
    run_tool "convert-to-extension-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/NotificationService.cs\",
        \"methodName\": \"FormatCurrency\"
    }"
    show_after "FormatCurrency is now a decimal extension method: amount.FormatCurrency()"

    step "5c. Use ICustomerRepository interface instead of concrete CustomerRepository"
    show_before "UpdateCustomerTier takes concrete CustomerRepository parameter"
    run_tool "use-interface" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/CustomerService.cs\",
        \"methodName\": \"UpdateCustomerTier\",
        \"parameterName\": \"repository\",
        \"interfaceName\": \"ICustomerRepository\"
    }"
    show_after "Parameter type changed to ICustomerRepository — now testable with mocks"

    step "5d. Convert EmailService parameter to constructor injection"
    show_before "RegisterCustomer takes EmailService as method parameter"
    run_tool "convert-to-constructor-injection" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/CustomerService.cs\",
        \"methodParameters\": [{\"method\": \"RegisterCustomer\", \"parameter\": \"emailService\"}],
        \"useProperty\": false
    }"
    show_after "EmailService is now constructor-injected as a field"

    step "5e. Make OrderProcessor._paymentGateway readonly"
    show_before "_paymentGateway is set only in constructor but not marked readonly"
    run_tool "make-field-readonly" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"fieldName\": \"_paymentGateway\"
    }"
    show_after "_paymentGateway is now readonly — compiler enforces immutability"

    step "5f. Transform StockSnapshot setters to init-only"
    show_before "StockSnapshot properties have regular setters but are only set during creation"
    run_tool "transform-setter-to-init" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/InventoryManager.cs\",
        \"propertyName\": \"ProductId\"
    }"
    show_after "ProductId now uses init accessor — immutable after construction"
}

# ── Demo 6: Design Patterns ──────────────────────────────────────

demo_6_design_patterns() {
    banner "Demo 6: Extract Interface, Decorator & Adapter"

    step "6a. Extract INotificationService interface"
    show_before "NotificationService is a concrete class with no interface"
    run_tool "extract-interface" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/NotificationService.cs\",
        \"className\": \"NotificationService\",
        \"memberList\": \"SendOrderConfirmation,SendShippingNotification,SendPaymentFailedNotification,SendTierUpgradeNotification\",
        \"interfaceFilePath\": \"$SRC/INotificationService.cs\"
    }"
    show_after "INotificationService interface created, NotificationService implements it"

    step "6b. Extract logging decorator around NotificationService.SendOrderConfirmation"
    show_before "No cross-cutting logging around notification sending"
    run_tool "extract-decorator" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/NotificationService.cs\",
        \"className\": \"NotificationService\",
        \"methodName\": \"SendOrderConfirmation\"
    }"
    show_after "LoggingNotificationServiceDecorator wraps SendOrderConfirmation with logging"

    step "6c. Create adapter for InventoryManager to match IWarehouseApi"
    show_before "InventoryManager has its own API, external system expects IWarehouseApi"
    run_tool "create-adapter" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/InventoryManager.cs\",
        \"className\": \"InventoryManager\",
        \"methodName\": \"ReserveStock\",
        \"adapterName\": \"WarehouseApiAdapter\"
    }"
    show_after "WarehouseApiAdapter bridges InventoryManager to IWarehouseApi consumers"

    step "6d. Add observer event to InventoryManager.ReserveStock"
    show_before "No event fires when stock is reserved — hard to monitor"
    run_tool "add-observer" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/InventoryManager.cs\",
        \"className\": \"InventoryManager\",
        \"methodName\": \"ReserveStock\",
        \"eventName\": \"StockReserved\"
    }"
    show_after "StockReserved event raised after each reservation"

    step "6e. Refactor feature flag to strategy pattern in PricingEngine"
    show_before "ApplyDynamicPricing has if/else branching on EnableNewPricingEngine flag"
    run_tool "feature-flag-refactor" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/PricingEngine.cs\",
        \"flagName\": \"EnableNewPricingEngine\"
    }"
    show_after "Feature flag replaced with strategy interface — clean extensibility"
}

# ── Demo 7: Safe Deletion, Rename & Cleanup ──────────────────────

demo_7_cleanup() {
    banner "Demo 7: Safe Delete, Rename & Cleanup"

    step "7a. Safe-delete unused LegacyExportXml method from OrderProcessor"
    show_before "LegacyExportXml is never called anywhere in the solution"
    run_tool "safe-delete-method" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"methodName\": \"LegacyExportXml\"
    }"
    show_after "Dead method removed safely — tool verified no callers exist"

    step "7b. Safe-delete unused _migrationTimestamp field from OrderProcessor"
    show_before "_migrationTimestamp is declared but never read or written"
    run_tool "safe-delete-field" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"fieldName\": \"_migrationTimestamp\"
    }"
    show_after "Unused field removed — no references found"

    step "7c. Safe-delete unused 'verbose' parameter from GetCustomerSummary"
    show_before "GetCustomerSummary has 'verbose' parameter but never uses it"
    run_tool "safe-delete-parameter" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/CustomerService.cs\",
        \"methodName\": \"GetCustomerSummary\",
        \"parameterName\": \"verbose\"
    }"
    show_after "Unused parameter removed from method and all call sites"

    step "7d. Safe-delete unused 'separator' variable in ReportGenerator"
    run_tool "safe-delete-variable" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/ReportGenerator.cs\",
        \"selectionRange\": \"68:9-68:58\"
    }"
    show_after "Unused local variable removed"

    step "7e. Rename poorly-named 'x' variable to 'tierDiscountRate' in OrderProcessor"
    show_before "Variable 'x' on line 80 holds a tier discount rate"
    run_tool "rename-symbol" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/OrderProcessor.cs\",
        \"oldName\": \"x\",
        \"newName\": \"tierDiscountRate\",
        \"line\": 80,
        \"column\": 17
    }"
    show_after "Variable renamed from 'x' to 'tierDiscountRate' — clear intent"

    step "7f. Rename poorly-named 'q' parameter to 'quantity' in InventoryManager"
    run_tool "rename-symbol" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/InventoryManager.cs\",
        \"oldName\": \"q\",
        \"newName\": \"quantity\",
        \"line\": 30,
        \"column\": 49
    }"
    show_after "Parameter renamed from 'q' to 'quantity' across all usages"

    step "7g. Clean up unused using directives in NotificationService.cs"
    show_before "System.Diagnostics is imported but never used"
    run_tool "cleanup-usings" "{
        \"solutionPath\": \"$SLN\",
        \"filePath\": \"$SRC/NotificationService.cs\"
    }"
    show_after "Unused 'using System.Diagnostics' removed"
}

# ── Main ──────────────────────────────────────────────────────────

main() {
    setup_originals

    if [ "${1:-}" = "--reset" ]; then
        reset_files
        exit 0
    fi

    # Build the demo project first
    echo -e "${BOLD}Building demo project...${NC}"
    dotnet build "$SLN" --verbosity quiet
    echo -e "${GREEN}Build successful.${NC}"
    echo ""

    local demo="${1:-all}"

    case "$demo" in
        1) demo_1_analysis ;;
        2) reset_files && demo_2_method_transformation ;;
        3) reset_files && demo_3_introduction ;;
        4) reset_files && demo_4_method_moving ;;
        5) reset_files && demo_5_conversion ;;
        6) reset_files && demo_6_design_patterns ;;
        7) reset_files && demo_7_cleanup ;;
        all)
            demo_1_analysis
            reset_files && demo_2_method_transformation
            reset_files && demo_3_introduction
            reset_files && demo_4_method_moving
            reset_files && demo_5_conversion
            reset_files && demo_6_design_patterns
            reset_files && demo_7_cleanup
            ;;
        *)
            echo "Usage: $0 [1-7|all|--reset]"
            exit 1
            ;;
    esac

    echo ""
    echo -e "${GREEN}${BOLD}All demos complete!${NC}"
}

main "$@"
