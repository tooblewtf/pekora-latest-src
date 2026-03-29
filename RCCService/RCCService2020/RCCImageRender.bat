@echo off
:loop
echo "Starting Image Render RCC"
RCCService.exe -Console -verbose -settingsfile "Settings.json" -port 2621