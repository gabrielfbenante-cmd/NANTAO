@echo off
rem Instala os mods NANTAO no perfil "NantaoTerror" (crie ele antes importando o NantaoTerror.r2z).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-no-jogo.ps1" -Perfil "NantaoTerror"
