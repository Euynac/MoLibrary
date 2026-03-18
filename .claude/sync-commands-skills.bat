@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

for %%I in ("%SCRIPT_DIR%") do set "CURRENT_AGENT=%%~nxI"
for %%I in ("%SCRIPT_DIR%\..") do set "ROOT_DIR=%%~fI"

set "CONFIG_FILE=%ROOT_DIR%\.agentfolder"
set "SOURCE_COMMANDS=%ROOT_DIR%\%CURRENT_AGENT%\commands"
set "SOURCE_SKILLS=%ROOT_DIR%\%CURRENT_AGENT%\skills"
set /a syncCount=0

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

echo Source agent: %CURRENT_AGENT%
echo Root folder: %ROOT_DIR%

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

echo Sync completed. Updated !syncCount! agent folder(s).
exit /b 0
