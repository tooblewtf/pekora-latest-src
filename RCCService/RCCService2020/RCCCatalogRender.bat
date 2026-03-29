@echo off
:loop
echo "Starting Catalog Render RCC"
RCCService.exe -Console -verbose -settingsfile "Settings.json" -port 4621