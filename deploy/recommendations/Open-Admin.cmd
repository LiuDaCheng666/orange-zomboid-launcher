@echo off
setlocal DisableDelayedExpansion
pushd "%~dp0..\.."
if exist "%LOCALAPPDATA%\OrangeLauncher\recommendations-production\initial-credentials.json" (
  echo Private credentials file: "%LOCALAPPDATA%\OrangeLauncher\recommendations-production\initial-credentials.json"
  python "%~dp0admin_remote.py" --open
) else (
  python "%~dp0admin_remote.py" --credentials --open
)
if errorlevel 1 (
  echo Administrator tunnel could not start. Close the local preview on port 18083 and retry.
  pause
)
popd
endlocal
