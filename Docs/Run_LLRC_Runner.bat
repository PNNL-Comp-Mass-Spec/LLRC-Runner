@echo off
rem Note that the LLRC Runner is run every 4 hours by the DMS Program Runner
rem
c:
cd \DMS_Programs\LLRCRunner\
LLRCRunner.exe 168h /DB /Skip > Processing_Log.txt
