@echo off
REM This is a convenience file that calls through to the main one 
setlocal EnableDelayedExpansion
call "%~dp0Build\BatchFiles\RunUAT.bat" %*
exit /b !ERRORLEVEL!