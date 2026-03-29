@echo off
:loop
echo "Starting Game Render RCC"
RCCService.exe -Console -verbose -settingsfile "Settings.json" -port 3621