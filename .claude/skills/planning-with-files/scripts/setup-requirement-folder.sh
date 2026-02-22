#!/bin/bash
# Setup requirement folder under .pending/
# Usage: ./setup-requirement-folder.sh "description"
# Returns: Full path to the created folder

set -e

DESCRIPTION="${1:-task}"
PENDING_DIR=".pending"

# Create .pending directory if it doesn't exist
mkdir -p "$PENDING_DIR"

# Find the highest existing folder number
MAX_NUM=0
if [ -d "$PENDING_DIR" ]; then
    for dir in "$PENDING_DIR"/*; do
        if [ -d "$dir" ]; then
            # Extract number from folder name (handles both "NNN-name" and "(done) NNN-name")
            basename_dir=$(basename "$dir")
            # Remove "(done) " prefix if present
            clean_name="${basename_dir#\(done\) }"
            # Extract the number part
            if [[ "$clean_name" =~ ^([0-9]+)- ]]; then
                num="${BASH_REMATCH[1]}"
                # Remove leading zeros for comparison
                num=$((10#$num))
                if [ "$num" -gt "$MAX_NUM" ]; then
                    MAX_NUM=$num
                fi
            fi
        fi
    done
fi

# Calculate next number
NEXT_NUM=$((MAX_NUM + 1))
# Zero-pad to 3 digits
FOLDER_NUM=$(printf "%03d" $NEXT_NUM)

# Create folder name
FOLDER_NAME="${FOLDER_NUM}-${DESCRIPTION}"
FOLDER_PATH="${PENDING_DIR}/${FOLDER_NAME}"

# Create the folder
mkdir -p "$FOLDER_PATH"

# Output the folder path (this will be captured by Claude)
echo "$FOLDER_PATH"
