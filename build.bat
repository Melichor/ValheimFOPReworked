@echo off
rem Build the mod. The work is done by tools\build.py; build.bat --help lists the options.
setlocal
set "PYTHON="
where py >nul 2>nul && set "PYTHON=py -3"
if not defined PYTHON where python >nul 2>nul && set "PYTHON=python"
if not defined PYTHON (
    echo error: Python 3 is needed but not installed 1>&2
    exit /b 1
)
%PYTHON% "%~dp0tools\build.py" %*
exit /b %errorlevel%
