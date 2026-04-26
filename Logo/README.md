# Конвертація Logo.png в Logo.ico

## Спосіб 1: Онлайн конвертер (найпростіше)
1. Відкрий https://convertio.co/png-ico/ або https://cloudconvert.com/png-to-ico
2. Завантаж файл `Logo\Logo.png`
3. Конвертуй в `.ico`
4. Збережи як `Logo\Logo.ico`

## Спосіб 2: PowerShell (якщо встановлений ImageMagick)
```powershell
magick convert Logo\Logo.png -define icon:auto-resize=256,128,64,48,32,16 Logo\Logo.ico
```

## Спосіб 3: Використати Paint.NET або GIMP
1. Відкрий Logo.png
2. File → Save As
3. Вибери формат .ico
4. Збережи як Logo.ico в папці Logo\

## Після створення Logo.ico:
1. Переконайся що файл Logo\Logo.ico існує
2. Перезбери проект: `dotnet build`
3. Запусти програму
4. Іконка з'явиться на панелі задач і в заголовку вікна

## Розміри іконки
Для Windows рекомендується мати такі розміри в ICO файлі:
- 256x256 (для Windows 10/11)
- 128x128
- 64x64
- 48x48
- 32x32
- 16x16 (для панелі задач)
