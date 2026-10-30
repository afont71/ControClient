@echo off
rem Arranque de ControClient en modo desarrollo para este equipo.
rem La tarea programada "ControClient" ejecuta este fichero al iniciar sesion.
cd /d C:\organizaCLI\ControClient
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src\ControClient.Web --urls http://localhost:5080 --no-launch-profile >> C:\organizaCLI\contoclient-data\app.out.log 2>&1
