# Watch Dogs

GUI-программа и служба Windows для **контроля службы 1С-сервера**.

Служба 1С запускается с системой. Watch Dogs мониторит её состояние и, если служба
не отвечает (остановлена / не проходит проверку TCP / HTTP), автоматически
перезапускает её. После перезагрузки системы программа **ждёт 5 минут**
(настраивается) и только затем начинает мониторинг.

## Возможности

- Мониторинг службы 1С через `ServiceController` (статус `Running`)
- Опциональные глубокие проверки: TCP-порт и HTTP-URL
- Автоперезапуск отказавшей службы 1С с ограничением числа попыток и паузой
- Задержка старта мониторинга после загрузки системы (по умолчанию 5 минут)
- Полностью настраиваемый GUI: все параметры настраиваются в интерфейсе
- Журнал событий, счётчики проверок и перезапусков
- Установка через `setup.exe`; **полное удаление**, включая службу мониторинга

## Структура

| Компонент | Описание |
|-----------|----------|
| `WatchDogs.exe` | GUI (WinForms): состояние, настройки, журнал |
| `WatchDogs.Service.exe` | Служба мониторинга (`WatchDogsMonitor`) |
| `WatchDogs.Core.dll` | Общая логика: конфиг, проверки, перезапуск |
| `WatchDogs-Setup-1.0.0.exe` | Установщик (Inno Setup) |

## Параметры (все настраиваются в GUI)

| Параметр | По умолчанию | Назначение |
|----------|--------------|------------|
| Имя службы 1С | `1CV8S` | Windows service name для мониторинга |
| Тип проверки | Статус службы | Статус / +TCP / +HTTP |
| TCP хост / порт | localhost:1541 | Глубокая проверка |
| HTTP URL | http://localhost/health | Глубокая проверка |
| Интервал проверки | 30 с | Период опроса |
| Задержка после загрузки | **5 мин** | Ожидание после reboot перед мониторингом |
| Таймаут ответа | 10 с | TCP/HTTP timeout |
| Автоперезапуск | вкл. | Перезапуск при отказе |
| Макс. перезапусков подряд | 5 | После — длинная пауза (back-off) |
| Пауза между перезапусками | 5 мин | Защита от дребезга |
| Мониторинг включён | вкл. | Общий выключатель |

Конфигурация хранится в `%ProgramData%\WatchDogs\config.json`,
статус — `status.json`, журналы — `%ProgramData%\WatchDogs\logs\`.

## Служба мониторинга

- Имя: **WatchDogsMonitor**
- Тип: Windows Service, автозапуск (`start= auto`)
- Утилиты CLI (от имени администратора):

```text
WatchDogs.Service.exe --install [--start]
WatchDogs.Service.exe --uninstall
WatchDogs.Service.exe --start | --stop | --status
WatchDogs.Service.exe --console     # отладка без установки в службу
```

## Сборка на GitHub Actions

Workflow: `.github/workflows/build.yml`

1. Сборка решения MSBuild (`Release`)
2. Установка Inno Setup (choco)
3. Компиляция `installer/WatchDogs.iss` → **`WatchDogs-Setup-1.0.0.exe`**
4. Загрузка артефакта `WatchDogs-Setup`
5. Для тегов `v*` — автоматический GitHub Release с setup.exe

Ручной запуск: **Actions → Build setup.exe → Run workflow**.

Локальная сборка (Windows + VS / Build Tools):

```powershell
msbuild WatchDogs.sln /p:Configuration=Release
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\WatchDogs.iss
```

## Установка и удаление

1. Запустите `WatchDogs-Setup-1.0.0.exe` **от администратора**
2. Отметьте «Установить и запустить службу мониторинга»
3. В GUI укажите имя службы 1С и сохраните настройки

**Полное удаление:** «Установка и удаления программ» Windows →
**Watch Dogs** → Удалить. Удалитель:

- останавливает и удаляет службу `WatchDogsMonitor`
- завершает процессы GUI и сервиса
- удаляет все файлы программы и данные `%ProgramData%\WatchDogs`

## Требования

- Windows 10/11 или Windows Server 2016+
- .NET Framework 4.8 (предустановлен на актуальных системах)
- Права администратора для установки/удаления службы

## Работа с git

Репозиторий: `zigorminsk-debug/1-watch-dogs`  
Основная ветка: `main`. Фичи и фиксы — в своих ветках, merge через PR.

```bash
# клонирование
git clone https://github.com/zigorminsk-debug/1-watch-dogs.git
cd 1-watch-dogs

# свежая основная ветка
git checkout main
git pull origin main

# новая работа
git checkout -b feature/short-name
git add -A
git commit -m "feat: краткое описание изменения"
git push origin feature/short-name
```

**Что коммитить:** исходники (`src/`), `installer/`, `.github/`, `assets/`, `README.md`.  
**Что не коммитить:** `bin/`, `obj/`, `*.user`, локальные логи — уже в `.gitignore`.  
`releases/*.exe` обновляет CI после зелёной сборки (коммит с `[skip ci]`).

Полезные команды:

```bash
git status                 # что изменилось
git log --oneline -10      # последние коммиты
git diff                   # дифф без индекса
git pull origin <branch>   # забрать правки (в т.ч. свежий setup.exe)
```

Сборка `setup.exe` — только на **GitHub Actions** (Actions → Build setup.exe → Run workflow), не локально.

## Разработчик

**csl.by** · +375 29 337-14-12  
http://csl.by
