# Mark requirement folder as complete by adding (done) prefix
# Usage: .\mark-complete.ps1 [folder_path]
# If no folder_path provided, searches for task_plan.md in .pending/

param(
    [string]$FolderPath = ""
)

$ErrorActionPreference = "Stop"

# If no folder path provided, try to find it
if ([string]::IsNullOrEmpty($FolderPath)) {
    # Look for most recently modified task_plan.md in .pending/
    if (Test-Path ".pending") {
        $taskPlans = Get-ChildItem -Path ".pending" -Recurse -Filter "task_plan.md" -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($taskPlans) {
            $FolderPath = $taskPlans.DirectoryName
        }
    }
}

# Exit if no folder found
if ([string]::IsNullOrEmpty($FolderPath) -or -not (Test-Path $FolderPath)) {
    exit 0
}

# Check if task_plan.md exists
$taskPlanFile = Join-Path $FolderPath "task_plan.md"
if (-not (Test-Path $taskPlanFile)) {
    exit 0
}

# Check if folder already has (done) prefix (with or without space)
$folderName = Split-Path $FolderPath -Leaf
if ($folderName -like "(done)*" -or $folderName -like "(done) *") {
    exit 0
}

# Check if all phases are complete
# Look for "Status:" lines and check if any are not "complete"
$content = Get-Content $taskPlanFile -Raw
$statusLines = Select-String -InputObject $content -Pattern "Status:" -AllMatches
$incompleteCount = 0

foreach ($match in $statusLines.Matches) {
    $line = $content.Substring($match.Index, [Math]::Min(100, $content.Length - $match.Index))
    if ($line -notmatch "complete") {
        $incompleteCount++
    }
}

if ($incompleteCount -eq 0 -and $statusLines.Matches.Count -gt 0) {
    # All phases are complete, rename the folder (no space between (done) and folder name)
    $parentDir = Split-Path $FolderPath -Parent
    $newFolderName = "(done)$folderName"
    $newFolderPath = Join-Path $parentDir $newFolderName

    Move-Item -Path $FolderPath -Destination $newFolderPath -Force
    Write-Output "[planning-with-files] ✓ Task complete! Folder renamed to: $newFolderName"
}
