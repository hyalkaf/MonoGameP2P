@echo off
%FNR%  --cl --dir ".." 
set /p num=How many clients you want to run? 
for /l %%x in (1, 1, %num%) do (
  start "Client" "%~dp0\bin\Debug\Client.exe"
)