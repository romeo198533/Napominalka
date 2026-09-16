#!/usr/bin/env python3
"""Режет готовый exe на части по 40 МБ и кладёт рядом скрипт сборки.

Telegram не принимает файлы больше 50 МБ, а самораспаковывающаяся сборка
Windows Forms весит около 63 МБ. Части склеиваются побайтово обратно
в тот же самый файл — проверяется сравнением sha256.
"""

import hashlib
import os
import sys

CHUNK = 40 * 1024 * 1024

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'dist', 'Напоминалка.exe')
OUT = os.path.join(ROOT, 'delivery')

BAT = '''@echo off
cd /d "%~dp0"
echo Собираю программу из частей, подождите...
copy /b {parts} "Напоминалка.exe"
if errorlevel 1 goto bad
echo.
echo Готово. Рядом появился файл Напоминалка.exe
echo Запустите его, установка не нужна.
goto end
:bad
echo Не получилось. Проверьте, что все части лежат в этой же папке.
:end
echo.
pause
'''


def main():
    if not os.path.exists(SRC):
        sys.exit('нет файла ' + SRC + ' — сначала соберите программу')

    data = open(SRC, 'rb').read()
    os.makedirs(OUT, exist_ok=True)

    names = []
    for i in range(0, len(data), CHUNK):
        name = 'Напоминалка.часть%d' % (len(names) + 1)
        open(os.path.join(OUT, name), 'wb').write(data[i:i + CHUNK])
        names.append(name)

    parts = ' + '.join('"%s"' % n for n in names)
    # cmd.exe на русской Windows читает bat-файлы в cp866
    open(os.path.join(OUT, 'собери программу.bat'), 'wb').write(
        BAT.format(parts=parts).encode('cp866'))

    # склейка обязана дать исходный файл байт в байт
    joined = b''.join(open(os.path.join(OUT, n), 'rb').read() for n in names)
    if joined != data:
        sys.exit('части не склеиваются обратно — сборку отдавать нельзя')

    print('частей: %d, sha256 %s' % (len(names), hashlib.sha256(data).hexdigest()[:16]))
    for n in names:
        print('  %s — %.1f МБ' % (n, os.path.getsize(os.path.join(OUT, n)) / 1048576))
    print('  собери программу.bat')


if __name__ == '__main__':
    main()
