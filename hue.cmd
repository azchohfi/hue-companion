@echo off
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0tools\hue-cli.ps1" %*
