<div align="center">

# 🏝️ DynamicIslandPC

**A sleek, fluid, and powerful Dynamic Island media widget crafted for Windows 10 & 11.**

[![Latest Release](https://img.shields.io/github/v/release/RilleSB/DynamicIslandForWindows?color=8E44AD&label=Latest%20Release&style=for-the-badge)](https://github.com/RilleSB/DynamicIslandForWindows/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows)](https://github.com/RilleSB/DynamicIslandForWindows)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Refresh Rate](https://img.shields.io/badge/Display-165Hz%2B%20Unlocked-FF4757?style=for-the-badge)](https://github.com/RilleSB/DynamicIslandForWindows)
[![Stealth Mode](https://img.shields.io/badge/Stealth%20Mode-OBS%20%2F%20Discord%20Safe-2ED573?style=for-the-badge)](https://github.com/RilleSB/DynamicIslandForWindows)

---

### [English](#-english) &nbsp;•&nbsp; [Русский](#-русский)

---

<img src="flex.gif" width="64" alt="Flex Mascot" />

*Experience your music playback on desktop with fluid animations, adaptive album aura, and glassmorphic beauty.*

</div>

---

<a name="-english"></a>
# 🇬🇧 English

## ✨ Overview

**DynamicIslandPC** brings the modern, interactive iOS Dynamic Island experience straight to your Windows desktop. Designed with attention to detail, it automatically detects playing media across Spotify, Yandex Music, VK, Apple Music, YouTube, and desktop browsers, morphing gracefully between compact capsules and full-fledged media hubs.

---

## 🚀 Key Features

### 🏝️ 3 Intelligent Display Modes
* **Minimal Mode (138×60)**: Ultra-compact capsule showing squircle album art, an animated 4-bar equalizer wave, and an optional playful companion mascot.
* **Compact Mode (335×70)**: Smooth slide-in marquee with song title, artist, audio source badge (Spotify, Browser, etc.), and animated equalizer.
* **Expanded Mode (500×176)**: Full OLED media command center featuring large album cover, ambient glowing aura, interactive progress scrubber with remaining time, vector playback controls (Prev / Play / Pause / Next), and quick diagnostics/settings access.
* **Paused Mode**: Subtle circular capsule with pause indicator.
* **Track Reveal Overlay**: Non-intrusive floating pill that glides in for a few seconds when a new track starts.

### 🔮 Liquid Glass Style (Beta)
* Next-generation optical glassmorphism theme toggleable in Settings.
* Translucent capsule (~40% opacity) that reveals desktop wallpapers and background apps beneath it.
* Multi-layer optical physics: specular convex lens glare, 1.5px chamfered crystal bevel, diagonal light sheen, and fluid interior album caustics.

### ⚡ 165Hz+ High Refresh Rate Sync
* Native monitor refresh rate detection using Win32 APIs.
* Unlocks WPF's default 60 FPS cap — all expansions, text marquees, track cross-fades, and waveforms render at native **120Hz / 144Hz / 165Hz / 240Hz+**.

### 🖥️ Seamless Multi-Monitor Positioning
* Built-in display monitor selector with resolution detection.
* Full support for multi-monitor desktop bounds (including negative coordinates). The island centers flawlessly on any selected screen.
* Quick-snap buttons: **Top**, **Bottom**, and **Center**, plus fine-grained pixel sliders.

### 👻 Stealth Capture Exclusion (OBS & Stream Friendly)
* Built-in privacy toggle using Windows `WDA_EXCLUDEFROMCAPTURE`.
* **Visible to you, invisible to your stream**: stays on your monitor, but automatically vanishes on screenshots, OBS Studio capture, Discord screen shares, and video recordings.

### 🎨 Adaptive Album Aura & Color Engine
* Dynamically extracts primary, secondary, and ambient colors from the active song's album art.
* Deep OLED dark theme, light theme, custom HEX/RGB color picker, and background opacity sliders (10% - 100%).

### 🎮 Gaming & Lock Modes
* **Gaming Mode (Click-Through)**: Makes the island completely click-through (`WS_EX_TRANSPARENT`), so you can aim, shoot, and click windows underneath without interruption.
* **Lock Mode**: Locks the widget in your preferred mode and prevents accidental clicks.
* **Global Hotkey**: Press <kbd>Ctrl</kbd> + <kbd>Space</kbd> anywhere to cycle modes without touching the mouse.

### 🐱 Animated Companion Mascot
* Keep a companion next to your player! Bundled with the energetic `flex.gif` cat.
* Supports custom GIFs, PNGs, and video loops (**MP4, WebM, WMV, AVI, MOV**).

---

## 🎮 Controls & Shortcuts

| Action | Control |
| :--- | :--- |
| **Cycle Display Modes** *(Minimal ➔ Compact ➔ Expanded)* | <kbd>Left Click</kbd> |
| **Previous / Next Track** | <kbd>Mouse Scroll Up / Down</kbd> |
| **Play / Pause** | <kbd>Middle Click</kbd> |
| **Global Mode Switch** | <kbd>Ctrl</kbd> + <kbd>Space</kbd> |
| **Quick Settings Menu** | <kbd>Right Click</kbd> |
| **Open Debug Logs** | <kbd>Double Right Click</kbd> |
| **Reposition Window** | Drag anywhere on the island |

---

## 🎵 Supported Media Players

DynamicIslandPC hooks directly into the **Windows System Media Transport Controls (SMTC)** API. If an application supports Windows media keys, it works out of the box:

* 🟢 **Spotify** (Desktop & Web)
* 🟡 **Yandex Music** (Desktop & Browser)
* 🔵 **VK Music**
* 🍎 **Apple Music**
* 🔴 **YouTube & YouTube Music** (via Chrome, Edge, Firefox, Brave, Opera, Vivaldi, Yandex Browser)
* 🎵 **AIMP, Foobar2000, Windows Media Player, Tidal, Deezer, VLC**

*(Browser source detection can be easily toggled on/off in the right-click tray menu).*

---

## 💻 Requirements

* **Operating System**: Windows 10 (19041+) or Windows 11
* **Runtime**: **.NET 8 Desktop Runtime (x64)**
  * 👉 [Download .NET Desktop Runtime 8.x (Windows x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

## 📦 Installation

1. Go to the [Releases](../../releases) page and download `DynamicIslandPC.exe` (or the portable zip archive).
2. Ensure you have [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed.
3. Launch `DynamicIslandPC.exe` — **no installation required**.
4. *(Optional)* Right-click the island or tray icon and enable **"Run on Windows Startup"**.

---

<br/>

---

<a name="-русский"></a>
# 🇷🇺 Русский

## ✨ О проекте

**DynamicIslandPC** — это стильный, функциональный и плавный медиа-виджет в стиле Apple Dynamic Island, созданный специально для Windows 10 и 11. Он автоматически считывает информацию о воспроизводимом треке из Spotify, Яндекс Музыки, ВКонтакте, браузеров и других плееров, элегантно трансформируясь между компактной капсулой и полноценным пультом управления музыкой.

---

## 🚀 Основные возможности

### 🏝️ 3 умных режима отображения
* **Минимальный (Minimal, 138×60)**: Аккуратная капсула со скруглённой обложкой альбома, анимированным 4-полосным эквалайзером и возможностью включить анимированного маскота.
* **Компактный (Compact, 335×70)**: Плавная бегущая строка с названием трека, исполнителем, бейджем источника (Spotify, Яндекс Музыка, Браузер) и эквалайзером.
* **Развёрнутый (Expanded, 500×176)**: Полноценный OLED-центр управления: крупная обложка с мягким глубинным свечением (Aura), интерактивная шкала прогресса с отображением прошедшего и оставшегося времени, векторные SVG-кнопки управления (Назад / Пауза / Вперёд), а также быстрый переход в настройки и окно диагностики.
* **Режим паузы**: Лаконичная круглая капсула с иконкой паузы.
* **Уведомление о смене трека (Track Reveal)**: Ненавязчивая плашка, мягко выплывающая на несколько секунд при переключении песни.

### 🔮 Стиль Liquid Glass (Жидкое стекло, Beta)
* Реалистичный эффект премиального стекла, включаемый в настройках.
* Честная полупрозрачность корпуса (~40%), сквозь которую проглядывают обои рабочего стола и окна.
* Многослойная оптическая физика: выпуклая верхняя линза (lens glare), кристаллическая фаска 1.5px с преломлением света, диагональный отблеск и глубинная каустика цвета обложки.

### ⚡ Синхронизация с высокой герцовкой (165 Гц+)
* Автоматическое определение частоты обновления монитора через Win32 API.
* Снято стандартное ограничение WPF в 60 FPS — анимации переходов, раскрытия, эквалайзера и бегущей строки работают на нативной частоте **120 Гц / 144 Гц / 165 Гц / 240 Гц+**.

### 🖥️ Полноценная поддержка нескольких мониторов
* Выпадающий список выбора целевого монитора с автоопределением разрешения.
* Корректная обработка отрицательных координат в мультимониторных конфигурациях. Остров центрируется ровно на выбранном экране.
* Кнопки быстрой привязки: **Вверху**, **Внизу**, **По центру**, а также точная настройка положения слайдерами с точностью до пикселя.

### 👻 Скрытие от захвата экрана (Stealth Mode)
* Опция конфиденциальности на базе Windows `WDA_EXCLUDEFROMCAPTURE`.
* **Вы видите остров, а зрители — нет**: виджет отображается на вашем мониторе, но автоматически исчезает на скриншотах, при трансляции в Discord, захвате в OBS Studio и записи видео.

### 🎨 Адаптивный цвет альбома и темы
* Динамическое извлечение акцентных оттенков из обложки текущего трека для создания фоновой ауры.
* Глубокая тёмная OLED-тема, светлая тема, палитра быстрых пресетов, RGB/HEX-редактор цвета и настройка прозрачности (от 10% до 100%).

### 🎮 Игровой режим и блокировка
* **Игровой режим (клики насквозь)**: Пропускает все клики мыши сквозь виджет прямо в игру или приложение под ним (`WS_EX_TRANSPARENT`).
* **Зафиксировать размер (Lock Mode)**: Отключает случайное переключение режимов по клику.
* **Глобальная горячая клавиша**: Сочетание <kbd>Ctrl</kbd> + <kbd>Пробел</kbd> циклически меняет режимы из любой программы или игры.

### 🐱 Анимированный декор рядом
* Возможность поставить рядом с плеером любимого питомца или маскота (в комплекте легендарный флексящий кот `flex.gif`).
* Поддерживает любые файлы: GIF, статические картинки (PNG, JPG), а также видеофайлы в цикле (**MP4, WebM, WMV, AVI, MOV**).

---

## 🎮 Управление и горячие клавиши

| Действие | Управление |
| :--- | :--- |
| **Смена режимов** *(Минимальный ➔ Компактный ➔ Развёрнутый)* | <kbd>Левая кнопка мыши (ЛКМ)</kbd> |
| **Предыдущий / Следующий трек** | <kbd>Колёсико мыши вверх / вниз</kbd> |
| **Воспроизведение / Пауза** | <kbd>Средняя кнопка мыши (СКМ)</kbd> |
| **Глобальное переключение режима** | <kbd>Ctrl</kbd> + <kbd>Пробел</kbd> |
| **Меню быстрых настроек** | <kbd>Правая кнопка мыши (ПКМ)</kbd> |
| **Открыть лог-файл (диагностика)** | <kbd>Двойной правый клик</kbd> |
| **Перемещение по экрану** | Зажать ЛКМ в любом месте острова и тянуть |

---

## 🎵 Поддерживаемые плееры

Приложение работает через стандартный Windows API **System Media Transport Controls (SMTC)**. Поддерживается всё, что реагирует на мультимедийные клавиши клавиатуры:

* 🟢 **Spotify** (приложение и веб-версия)
* 🟡 **Яндекс Музыка** (приложение и сайт)
* 🔵 **Музыка ВКонтакте**
* 🍎 **Apple Music**
* 🔴 **YouTube и YouTube Music** (через Яндекс Браузер, Chrome, Edge, Firefox, Opera, Brave и др.)
* 🎵 **AIMP, Foobar2000, Windows Media Player, Tidal, Deezer, VLC**

*(Отслеживание воспроизведения из браузеров можно мгновенно включить или выключить в контекстном меню трея).*

---

## 💻 Системные требования

* **Операционная система**: Windows 10 (билд 19041 или новее) / Windows 11
* **Среда выполнения**: **.NET 8 Desktop Runtime (x64)**
  * 👉 [Скачать .NET Desktop Runtime 8.x (Windows x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

## 📦 Установка

1. Перейдите во вкладку [Релизы (Releases)](../../releases) и скачайте `DynamicIslandPC.exe` (или zip-архив).
2. Убедитесь, что на компьютере установлена среда [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
3. Запустите файл `DynamicIslandPC.exe` — **установка не требуется**.
4. *(По желанию)* Нажмите правой кнопкой мыши по острову или иконке в трее и включите **«Запуск вместе с Windows»**.

---

<div align="center">

Made with ❤️ for music lovers on Windows.

</div>
