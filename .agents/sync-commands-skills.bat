@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

for %%I in ("%SCRIPT_DIR%") do set "CURRENT_AGENT=%%~nxI"
for %%I in ("%SCRIPT_DIR%\..") do set "ROOT_DIR=%%~fI"

set "CONFIG_FILE=%ROOT_DIR%\.agentfolder"
set "SOURCE_COMMANDS=%ROOT_DIR%\%CURRENT_AGENT%\commands"
set "SOURCE_SKILLS=%ROOT_DIR%\%CURRENT_AGENT%\skills"
call :get_doc_name "%CURRENT_AGENT%" SOURCE_DOC_NAME
set "SOURCE_DOC_FILE=%ROOT_DIR%\%SOURCE_DOC_NAME%"
set /a syncCount=0
set /a docSyncCount=0
set "SYNCED_DOC_NAMES=|"

if not exist "%CONFIG_FILE%" (
    echo [error] Missing config file: %CONFIG_FILE%
    exit /b 1
)

if not exist "%SOURCE_COMMANDS%" (
    echo [error] Missing source commands folder: %SOURCE_COMMANDS%
    exit /b 1
)

if not exist "%SOURCE_SKILLS%" (
    echo [error] Missing source skills folder: %SOURCE_SKILLS%
    exit /b 1
)

if not exist "%SOURCE_DOC_FILE%" (
    echo [error] Missing source doc file: %SOURCE_DOC_FILE%
    exit /b 1
)

echo Source agent: %CURRENT_AGENT%
echo Root folder: %ROOT_DIR%
echo Source doc: %SOURCE_DOC_NAME%

for /f "usebackq tokens=* delims=" %%L in ("%CONFIG_FILE%") do (
    set "line=%%L"
    if defined line (
        for %%F in (!line!) do (
            set "TARGET_AGENT=%%~F"

            if /I "!TARGET_AGENT!"=="%CURRENT_AGENT%" (
                echo [skip] !TARGET_AGENT!
            ) else (
                set "SYNC_TARGET_DIR=%ROOT_DIR%\!TARGET_AGENT!"

                if not exist "!SYNC_TARGET_DIR!" (
                    mkdir "!SYNC_TARGET_DIR!" >nul 2>&1
                    if not exist "!SYNC_TARGET_DIR!" (
                        echo [error] Failed to create target folder: !SYNC_TARGET_DIR!
                        exit /b 1
                    )
                )

                echo [sync] %CURRENT_AGENT% -^> !TARGET_AGENT!

                if exist "!SYNC_TARGET_DIR!\commands" (
                    rmdir /s /q "!SYNC_TARGET_DIR!\commands" >nul 2>&1
                    if exist "!SYNC_TARGET_DIR!\commands" (
                        echo [error] Failed to remove folder: !SYNC_TARGET_DIR!\commands
                        exit /b 1
                    )
                )

                if exist "!SYNC_TARGET_DIR!\skills" (
                    rmdir /s /q "!SYNC_TARGET_DIR!\skills" >nul 2>&1
                    if exist "!SYNC_TARGET_DIR!\skills" (
                        echo [error] Failed to remove folder: !SYNC_TARGET_DIR!\skills
                        exit /b 1
                    )
                )

                robocopy "%SOURCE_COMMANDS%" "!SYNC_TARGET_DIR!\commands" /E /NFL /NDL /NJH /NJS /NC /NS >nul 2>&1
                set "ROBOCOPY_EXIT=!ERRORLEVEL!"
                if !ROBOCOPY_EXIT! GEQ 8 (
                    echo [error] Failed to copy commands to !TARGET_AGENT!. Robocopy exit code: !ROBOCOPY_EXIT!
                    exit /b 1
                )

                robocopy "%SOURCE_SKILLS%" "!SYNC_TARGET_DIR!\skills" /E /NFL /NDL /NJH /NJS /NC /NS >nul 2>&1
                set "ROBOCOPY_EXIT=!ERRORLEVEL!"
                if !ROBOCOPY_EXIT! GEQ 8 (
                    echo [error] Failed to copy skills to !TARGET_AGENT!. Robocopy exit code: !ROBOCOPY_EXIT!
                    exit /b 1
                )

                call :get_doc_name "!TARGET_AGENT!" TARGET_DOC_NAME

                call :replace_in_path "!SYNC_TARGET_DIR!\commands" "%CURRENT_AGENT%" "!TARGET_AGENT!" "%SOURCE_DOC_NAME%" "!TARGET_DOC_NAME!"
                if errorlevel 1 exit /b 1

                call :replace_in_path "!SYNC_TARGET_DIR!\skills" "%CURRENT_AGENT%" "!TARGET_AGENT!" "%SOURCE_DOC_NAME%" "!TARGET_DOC_NAME!"
                if errorlevel 1 exit /b 1

                call :post_process_commands "!TARGET_AGENT!" "!SYNC_TARGET_DIR!\commands"
                if errorlevel 1 exit /b 1

                if /I not "!TARGET_DOC_NAME!"=="%SOURCE_DOC_NAME%" (
                    echo "!SYNCED_DOC_NAMES!" | findstr /I /L /C:"|!TARGET_DOC_NAME!|" >nul
                    if errorlevel 1 (
                        copy /y "%SOURCE_DOC_FILE%" "%ROOT_DIR%\!TARGET_DOC_NAME!" >nul
                        if errorlevel 1 (
                            echo [error] Failed to copy doc file to !TARGET_DOC_NAME!.
                            exit /b 1
                        )

                        call :replace_in_path "%ROOT_DIR%\!TARGET_DOC_NAME!" "%CURRENT_AGENT%" "%CURRENT_AGENT%" "%SOURCE_DOC_NAME%" "!TARGET_DOC_NAME!"
                        if errorlevel 1 exit /b 1

                        set "SYNCED_DOC_NAMES=!SYNCED_DOC_NAMES!!TARGET_DOC_NAME!|"
                        set /a docSyncCount+=1
                        echo [doc] !TARGET_DOC_NAME!
                    )
                )

                set /a syncCount+=1
                echo [ok] !TARGET_AGENT!
            )
        )
    )
)

if !syncCount! EQU 0 (
    echo No target agent folders were found in %CONFIG_FILE%.
    exit /b 0
)

echo Sync completed. Updated !syncCount! agent folder(s) and !docSyncCount! doc file(s).
exit /b 0

:get_doc_name
set "%~2=AGENTS.md"
if /I "%~1"==".claude" set "%~2=CLAUDE.md"
if /I "%~1"==".gemini" set "%~2=GEMINI.md"
exit /b 0

:post_process_commands
set "TARGET_AGENT_NAME=%~1"
set "TARGET_COMMANDS_DIR=%~2"

if /I "%TARGET_AGENT_NAME%"==".gemini" (
    call :convert_markdown_commands_to_gemini "%TARGET_COMMANDS_DIR%"
    if errorlevel 1 exit /b 1
)

exit /b 0

:convert_markdown_commands_to_gemini
set "TARGET_COMMANDS_DIR=%~1"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$path = [System.IO.Path]::GetFullPath($env:TARGET_COMMANDS_DIR);" ^
    "if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw ('Commands folder not found: ' + $path) }" ^
    "$files = Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*.md';" ^
    "foreach ($file in $files) {" ^
    "    $content = [System.IO.File]::ReadAllText($file.FullName);" ^
    "    $description = '';" ^
    "    $prompt = $content;" ^
    "    $match = [regex]::Match($content, '\A---\r?\n(?<frontmatter>.*?)\r?\n---\r?\n(?<body>.*)\z', [System.Text.RegularExpressions.RegexOptions]::Singleline);" ^
    "    if ($match.Success) {" ^
    "        $prompt = $match.Groups['body'].Value.TrimStart();" ^
    "        foreach ($line in ($match.Groups['frontmatter'].Value -split '\r?\n')) {" ^
    "            if ($line -match '^\s*description\s*:\s*(?<value>.*)\s*$') { $description = $Matches['value'].Trim(); break }" ^
    "        }" ^
    "    }" ^
    "    $tomlPath = [System.IO.Path]::ChangeExtension($file.FullName, '.toml');" ^
    "    $toml = New-Object System.Text.StringBuilder;" ^
    "    if (-not [string]::IsNullOrWhiteSpace($description)) {" ^
    "        [void]$toml.Append('description = ');" ^
    "        [void]$toml.AppendLine(($description | ConvertTo-Json -Compress));" ^
    "        [void]$toml.AppendLine();" ^
    "    }" ^
    "    [void]$toml.Append('prompt = ');" ^
    "    [void]$toml.AppendLine(($prompt | ConvertTo-Json -Compress));" ^
    "    [System.IO.File]::WriteAllText($tomlPath, $toml.ToString());" ^
    "    Remove-Item -LiteralPath $file.FullName -Force;" ^
    "}"

if errorlevel 1 (
    echo [error] Failed to convert Gemini commands in %TARGET_COMMANDS_DIR%
    exit /b 1
)

exit /b 0

:replace_in_path
set "TARGET_PATH=%~1"
set "SOURCE_AGENT_NAME=%~2"
set "TARGET_AGENT_NAME=%~3"
set "SOURCE_DOC_BASENAME=%~4"
set "TARGET_DOC_BASENAME=%~5"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$path = [System.IO.Path]::GetFullPath($env:TARGET_PATH);" ^
    "$sourceAgent = $env:SOURCE_AGENT_NAME;" ^
    "$targetAgent = $env:TARGET_AGENT_NAME;" ^
    "$sourceDoc = $env:SOURCE_DOC_BASENAME;" ^
    "$targetDoc = $env:TARGET_DOC_BASENAME;" ^
    "$replacements = New-Object 'System.Collections.Specialized.OrderedDictionary';" ^
    "if (-not [string]::IsNullOrEmpty($sourceAgent) -and -not [string]::IsNullOrEmpty($targetAgent) -and $sourceAgent -ne $targetAgent) { $replacements[$sourceAgent] = $targetAgent }" ^
    "$replacements[$sourceDoc] = $targetDoc;" ^
    "if ($sourceDoc -eq 'AGENTS.md') { $replacements['AGNETS.md'] = $targetDoc }" ^
    "if ($sourceDoc -eq 'CLAUDE.md') { $replacements['CLAUD.md'] = $targetDoc }" ^
    "$allowedExtensions = @('.md', '.json', '.ps1', '.py', '.razor', '.sh', '.yaml', '.yml', '.bat', '.txt');" ^
    "if (Test-Path -LiteralPath $path -PathType Leaf) { $files = @(Get-Item -LiteralPath $path) }" ^
    "elseif (Test-Path -LiteralPath $path -PathType Container) { $files = Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object { $allowedExtensions -contains $_.Extension.ToLowerInvariant() } }" ^
    "else { throw ('Path not found: ' + $path) }" ^
    "foreach ($file in $files) {" ^
    "    $content = [System.IO.File]::ReadAllText($file.FullName);" ^
    "    $updated = $content;" ^
    "    foreach ($entry in $replacements.GetEnumerator()) { $updated = $updated.Replace([string]$entry.Key, [string]$entry.Value) }" ^
    "    if ($updated -ne $content) { [System.IO.File]::WriteAllText($file.FullName, $updated) }" ^
    "}"

if errorlevel 1 (
    echo [error] Failed to replace text in %TARGET_PATH%
    exit /b 1
)

exit /b 0
