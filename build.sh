#!/usr/bin/env bash
# Сборка «Напоминалки» из исходников.
# Собирается на Linux, получается готовый exe для Windows 10 и 11.

set -e

export DOTNET_ROOT="$HOME/.local/dotnet"
export PATH="$HOME/.local/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# На этой машине нет библиотеки ICU — без флага dotnet не запускается.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export TMPDIR="$HOME/.local/tmp"
mkdir -p "$TMPDIR"

cd "$(dirname "$0")"

echo "1. Проверки логики"
dotnet run --project src/Tests/Tests.csproj -c Release

echo
echo "2. Сборка программы для Windows"
dotnet publish src/App/App.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o dist

echo
echo "3. Раскладка на части для отправки"
# Telegram не принимает файлы больше 50 МБ, поэтому готовый exe режется
# на части по 40 МБ, а рядом кладётся скрипт, который их склеивает.
python3 tools/split_for_telegram.py

echo
echo "Готово. Что получилось:"
ls -la dist
ls -la delivery
