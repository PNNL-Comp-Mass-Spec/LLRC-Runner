@echo off
rem Note that the LLRC Runner is run every 4 hours via a Sql Server Agent job
rem
c:
cd \DMS_Programs\LLRCRunner\
LLRCRunner.exe 12h /DB /Skip > Processing_Log.txt
