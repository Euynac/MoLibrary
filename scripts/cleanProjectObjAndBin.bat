@echo off
cd ..
for /r %%i in (bin) do (
    if exist "%%i" (
        echo Deleting %%i
        rd /s /q "%%i"
    )
)
for /r %%j in (obj) do (
    if exist "%%j" (
        echo Deleting %%j
        rd /s /q "%%j"
    )
)
pause