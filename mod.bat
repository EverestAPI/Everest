@echo off

IF not exist orig\ GOTO BACKUP
GOTO RESTORE

:BACKUP
mkdir orig
copy Celeste.exe orig\Celeste.exe
GOTO MOD

:RESTORE
copy orig\Celeste.exe Celeste.exe
GOTO MOD

:MOD
MonoMod.exe Celeste.exe
move MONOMODDED_Celeste.exe Celeste.exe
