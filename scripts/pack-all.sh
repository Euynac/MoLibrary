#!/bin/bash
set -e

# Navigate to repository root
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== Monica Framework NuGet Packaging ==="
echo ""

# Parse arguments
SKIP_BUILD=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--skip-build]"
            exit 1
            ;;
    esac
done

# Create output directory
OUTPUT_DIR="$REPO_ROOT/artifacts/packages"
if [ -d "$OUTPUT_DIR" ]; then
    echo "Cleaning existing packages..."
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"
echo "Output directory: $OUTPUT_DIR"
echo ""

# Find all Monica projects (exclude tests, examples, rules)
mapfile -t PROJECTS < <(find "$REPO_ROOT" -name "Monica.*.csproj" -type f | \
    grep -v "/tests/" | \
    grep -v "/examples/" | \
    grep -v "/rules/" | \
    sort)

echo "Found ${#PROJECTS[@]} projects to package:"
for project in "${PROJECTS[@]}"; do
    echo "  - $(basename "$project")"
done
echo ""

# Build if not skipped
if [ "$SKIP_BUILD" = false ]; then
    echo "Building all projects in Release configuration..."
    dotnet build -c Release
    echo "Build completed successfully!"
    echo ""
else
    echo "Skipping build (using existing binaries)..."
    echo ""
fi

# Pack each project
SUCCESS_COUNT=0
FAILED_PROJECTS=()

for project in "${PROJECTS[@]}"; do
    PROJECT_NAME=$(basename "$project" .csproj)
    echo "Packing $PROJECT_NAME..."

    PACK_ARGS=(
        "pack"
        "$project"
        "-c" "Release"
        "-o" "$OUTPUT_DIR"
        "--no-restore"
    )

    if [ "$SKIP_BUILD" = true ]; then
        PACK_ARGS+=("--no-build")
    fi

    if dotnet "${PACK_ARGS[@]}"; then
        echo "  ✓ $PROJECT_NAME packed successfully"
        ((SUCCESS_COUNT++))
    else
        echo "  ✗ $PROJECT_NAME failed to pack"
        FAILED_PROJECTS+=("$PROJECT_NAME")
    fi
    echo ""
done

# Summary
echo "=== Packaging Summary ==="
echo "Total projects: ${#PROJECTS[@]}"
echo "Successful: $SUCCESS_COUNT"
echo "Failed: ${#FAILED_PROJECTS[@]}"

if [ ${#FAILED_PROJECTS[@]} -gt 0 ]; then
    echo ""
    echo "Failed projects:"
    for project in "${FAILED_PROJECTS[@]}"; do
        echo "  - $project"
    done
fi

echo ""
echo "Packages location: $OUTPUT_DIR"

# List generated packages
mapfile -t PACKAGES < <(find "$OUTPUT_DIR" -name "*.nupkg" -type f | grep -v "\.symbols\.nupkg$" | sort)
echo "Generated ${#PACKAGES[@]} packages:"
for package in "${PACKAGES[@]}"; do
    echo "  - $(basename "$package")"
done

if [ ${#FAILED_PROJECTS[@]} -gt 0 ]; then
    exit 1
fi
