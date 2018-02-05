#!/bin/bash

if [ ! -d orig ]; then
  mkdir orig
  mv Celeste.exe orig/Celeste.exe
fi

cp orig/Celeste.exe Celeste.exe

mono MonoMod.exe Celeste.exe
mv MONOMODDED_Celeste.exe Celeste.exe
chmod u+x Celeste.exe
