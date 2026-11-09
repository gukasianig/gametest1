@echo off
set "UNITY_EDITOR=C:\Program Files\Unity 2022.3.62f3\Editor\Unity.exe"
if not exist "%UNITY_EDITOR%" (
    echo Unity 2022.3.62f3 was not found at "%UNITY_EDITOR%".
    pause
    exit /b 1
)
start "" "%UNITY_EDITOR%" -projectPath "%~dp0." -openfile "%~dp0Assets\Scenes\GreenArenaPlay.unity"
