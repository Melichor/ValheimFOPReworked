@echo off
rem Download and prepare the prerequisites. The work is done by tools\setup.py; setup.bat --help lists the options.
setlocal
set "PYTHON="
where py >nul 2>nul && set "PYTHON=py -3"
if not defined PYTHON where python >nul 2>nul && set "PYTHON=python"
if not defined PYTHON (
    echo error: Python 3 is needed but not installed 1>&2
    exit /b 1
)
%PYTHON% "%~dp0tools\setup.py" %*
exit /b %errorlevel%
