#!/bin/bash
# Mark requirement folder as complete by adding (done) prefix
# Usage: ./mark-complete.sh [folder_path]
# If no folder_path provided, searches for task_plan.md in .pending/

set -e

FOLDER_PATH="${1:-}"

# If no folder path provided, try to find it
if [ -z "$FOLDER_PATH" ]; then
    # Look for most recently modified task_plan.md in .pending/
    if [ -d ".pending" ]; then
        TASK_PLAN=$(find .pending -name "task_plan.md" -type f -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
        if [ -n "$TASK_PLAN" ]; then
            FOLDER_PATH=$(dirname "$TASK_PLAN")
        fi
    fi
fi

# Exit if no folder found
if [ -z "$FOLDER_PATH" ] || [ ! -d "$FOLDER_PATH" ]; then
    exit 0
fi

# Check if task_plan.md exists
TASK_PLAN_FILE="${FOLDER_PATH}/task_plan.md"
if [ ! -f "$TASK_PLAN_FILE" ]; then
    exit 0
fi

# Check if folder already has (done) prefix (with or without space)
FOLDER_NAME=$(basename "$FOLDER_PATH")
if [[ "$FOLDER_NAME" == "(done)"* ]] || [[ "$FOLDER_NAME" == "(done) "* ]]; then
    exit 0
fi

# Check if all phases are complete
# Look for "Status:" lines and check if any are not "complete"
STATUS_COUNT=$(grep -ci "Status:" "$TASK_PLAN_FILE" || echo "0")
INCOMPLETE_COUNT=$(grep -i "Status:" "$TASK_PLAN_FILE" | grep -iv "complete" | wc -l)

# Only rename if there's at least one status line and all are complete
if [ "$STATUS_COUNT" -gt 0 ] && [ "$INCOMPLETE_COUNT" -eq 0 ]; then
    # All phases are complete, rename the folder (no space between (done) and folder name)
    PARENT_DIR=$(dirname "$FOLDER_PATH")
    NEW_FOLDER_NAME="(done)${FOLDER_NAME}"
    NEW_FOLDER_PATH="${PARENT_DIR}/${NEW_FOLDER_NAME}"

    mv "$FOLDER_PATH" "$NEW_FOLDER_PATH"
    echo "[planning-with-files] ✓ Task complete! Folder renamed to: $NEW_FOLDER_NAME"
fi
