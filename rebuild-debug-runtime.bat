@echo off
_r %~dp0build -clean
call %~dp0build-debug-runtime.bat
