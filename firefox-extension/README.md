# Установка расширения Yandex Music Bridge

## Способ 1: Firefox Developer Edition (Рекомендуется)

1. Скачайте Firefox Developer Edition: https://www.mozilla.org/firefox/developer/
2. Откройте `about:config`
3. Найдите `xpinstall.signatures.required` и установите в `false`
4. Откройте `about:addons`
5. Нажмите на шестеренку → "Install Add-on From File"
6. Выберите файл `manifest.json` из папки расширения
7. Расширение останется после перезапуска

## Способ 2: Временная установка (текущий)

1. Откройте `about:debugging#/runtime/this-firefox`
2. Нажмите "Load Temporary Add-on"
3. Выберите файл `manifest.json`
4. **Минус**: Удаляется при закрытии Firefox

## Способ 3: Упаковка и подпись (для постоянной установки)

### Шаг 1: Создайте аккаунт на addons.mozilla.org
1. Зарегистрируйтесь на https://addons.mozilla.org
2. Перейдите в Developer Hub
3. Получите API ключи

### Шаг 2: Установите web-ext
```bash
npm install -g web-ext
```

### Шаг 3: Упакуйте расширение
```bash
cd firefox-extension
web-ext build
```

### Шаг 4: Подпишите расширение
```bash
web-ext sign --api-key=YOUR_API_KEY --api-secret=YOUR_API_SECRET
```

### Шаг 5: Установите подписанное расширение
1. Откройте `about:addons`
2. Нажмите на шестеренку → "Install Add-on From File"
3. Выберите созданный .xpi файл
4. Расширение останется после перезапуска

## Способ 4: Использовать Chrome/Edge (альтернатива)

Если не хотите возиться с подписью, можно использовать Chrome/Edge:
1. Откройте `chrome://extensions/`
2. Включите "Developer mode"
3. Нажмите "Load unpacked"
4. Выберите папку `firefox-extension`
5. Расширение останется после перезапуска

## Рекомендация

Для постоянного использования лучше всего:
- **Firefox Developer Edition** (способ 1) - проще всего
- **Chrome/Edge** (способ 4) - не требует подписи
