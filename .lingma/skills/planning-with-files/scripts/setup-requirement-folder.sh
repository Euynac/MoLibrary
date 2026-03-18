#!/bin/bash
# Setup requirement folder under .pending/
# Usage: ./setup-requirement-folder.sh "description"
# Returns: Full path to the created folder

set -e

DESCRIPTION="${1:-task}"
PENDING_DIR=".pending"

# Create .pending directory if it doesn't exist
mkdir -p "$PENDING_DIR"

# Check if a folder with the same description already exists
if [ -d "$PENDING_DIR" ]; then
    for dir in "$PENDING_DIR"/*; do
        if [ -d "$dir" ]; then
            basename_dir=$(basename "$dir")
            if [[ "$basename_dir" =~ ^[0-9]+-(.+)$ ]]; then
                existing_desc="${BASH_REMATCH[1]}"
                if [ "$existing_desc" = "$DESCRIPTION" ]; then
                    echo "${PENDING_DIR}/${basename_dir}"
                    exit 0
                fi
            fi
        fi
    done
fi

# Find the highest existing folder number
MAX_NUM=0
if [ -d "$PENDING_DIR" ]; then
    for dir in "$PENDING_DIR"/*; do
        if [ -d "$dir" ]; then
            basename_dir=$(basename "$dir")
            if [[ "$basename_dir" =~ ^([0-9]+)- ]]; then
                num="${BASH_REMATCH[1]}"
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
