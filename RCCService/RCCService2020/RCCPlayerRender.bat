@echo off
:loop
echo "Starting Player Render RCC"
RCCService.exe -Console -verbose -settingsfile "Settings.json" -port 1621