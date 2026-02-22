# Setup requirement folder under .pending/
# Usage: .\setup-requirement-folder.ps1 "description"
# Returns: Full path to the created folder

param(
    [string]$Description = "task"
)

$ErrorActionPreference = "Stop"

$PENDING_DIR = ".pending"

# Create .pending directory if it doesn't exist
if (-not (Test-Path $PENDING_DIR)) {
    New-Item -ItemType Directory -Path $PENDING_DIR | Out-Null
}

# Find the highest existing folder number
$MAX_NUM = 0
if (Test-Path $PENDING_DIR) {
    Get-ChildItem -Path $PENDING_DIR -Directory | ForEach-Object {
        $basename = $_.Name
        # Remove "(done) " prefix if present
        $cleanName = $basename -replace '^\(done\) ', ''
        # Extract the number part
        if ($cleanName -match '^(\d+)-') {
            $num = [int]$matches[1]
            if ($num -gt $MAX_NUM) {
                $MAX_NUM = $num
            }
        }
    }
}

# Calculate next number
$NEXT_NUM = $MAX_NUM + 1
# Zero-pad to 3 digits
$FOLDER_NUM = $NEXT_NUM.ToString("000")

# Create folder name
$FOLDER_NAME = "${FOLDER_NUM}-${Description}"
$FOLDER_PATH = Join-Path $PENDING_DIR $FOLDER_NAME

# Create the folder
New-Item -ItemType Directory -Path $FOLDER_PATH -Force | Out-Null

# Output the folder path (this will be captured by Claude)
Write-Output $FOLDER_PATH
