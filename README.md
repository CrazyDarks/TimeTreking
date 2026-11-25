# TimeTraking

[English](#english) | [Українська](#українська)

---

## English

A desktop application time tracker for Windows built with **C# (.NET 9)** and **Avalonia UI 12**.

### Features

- **Automatic App Tracking** — detects running windowed applications every second and tracks usage time in `HH:mm:ss` format.
- **Native Windows Icons** — automatically extracts and caches application icons directly from process `.exe` files.
- **Two Tracking Modes** — track all open apps in the list or toggle **Active Window Only** to count only the focused application.
- **Ignore List** — remove any process via **✕** or **Delete & Ignore** so it is no longer recorded; restore ignored processes anytime from the **Ignored** tab.
- **Manual Add** — click **+ Add App** to pick any running process, browse for an `.exe` file on disk, or type a process name manually.
- **Auto-Save** — tracked time, manually added apps, and ignored processes are persisted locally in `%LocalAppData%\TimeTraking\tracker-data.json`.

### Project Structure

| File | Purpose |
| --- | --- |
| `TimeTraking/Program.cs` | Entry point and Avalonia Desktop bootstrap |
| `TimeTraking/App.axaml(.cs)` | Application configuration and dark Fluent theme |
| `TimeTraking/MainWindow.axaml(.cs)` | Main window, tabs, tracking timer, and live search |
| `TimeTraking/AddProcessWindow.axaml(.cs)` | Dialog for adding running processes or `.exe` files |
| `TimeTraking/Models/TrackedApp.cs` | Tracked application model with `INotifyPropertyChanged` |
| `TimeTraking/Services/WindowsProcessHelper.cs` | Win32 API helpers for foreground window, `.exe` paths, and icon extraction |
| `TimeTraking/Services/TrackerStorage.cs` | Local JSON persistence for tracked state |

### Requirements

- Windows 10 / 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Getting Started

```bash
git clone https://github.com/CrazyDarks/TimeTreking.git
cd TimeTreking
dotnet run --project TimeTraking/TimeTraking.csproj
```

To build without running:

```bash
dotnet build
```

---

## Українська

Десктопний трекер часу роботи програм для Windows, написаний на **C# (.NET 9)** та **Avalonia UI 12**.

### Можливості

- **Автоматичне відстеження програм** — щосекунди визначає запущені програми з відкритими вікнами та рахує час роботи у форматі `ГГ:ХХ:СС`.
- **Реальні іконки Windows** — автоматично витягує та кешує іконки прямо з `.exe` файлів процесів.
- **Два режими підрахунку** — облік усіх відкритих програм зі списку або перемикач **«Тільки активне вікно»**.
- **Список ігнорування** — будь-який процес можна видалити кнопкою **✕** або **«Видалити і не записувати»**, щоб він більше не фіксувався; у вкладці **«Ігноровані»** його можна будь-коли повернути.
- **Ручне додавання** — через **«+ Додати програму»** можна обрати будь-який запущений процес, вказати `.exe` файл на диску або ввести назву вручну.
- **Автозбереження** — статистика часу, додані та ігноровані процеси зберігаються локально в `%LocalAppData%\TimeTraking\tracker-data.json`.

### Структура проєкту

| Файл | Призначення |
| --- | --- |
| `TimeTraking/Program.cs` | Точка входу та запуск Avalonia Desktop |
| `TimeTraking/App.axaml(.cs)` | Налаштування додатку та темної Fluent-теми |
| `TimeTraking/MainWindow.axaml(.cs)` | Головне вікно, вкладки, таймер відстеження та пошук |
| `TimeTraking/AddProcessWindow.axaml(.cs)` | Діалог додавання процесів або `.exe` файлів |
| `TimeTraking/Models/TrackedApp.cs` | Модель відстежуваної програми з `INotifyPropertyChanged` |
| `TimeTraking/Services/WindowsProcessHelper.cs` | Win32 API: активне вікно, шляхи `.exe` та витяг іконок |
| `TimeTraking/Services/TrackerStorage.cs` | Збереження та завантаження стану в JSON |

### Вимоги

- Windows 10 / 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Запуск

```bash
git clone https://github.com/CrazyDarks/TimeTreking.git
cd TimeTreking
dotnet run --project TimeTraking/TimeTraking.csproj
```

Для збірки без запуску:

```bash
dotnet build
```
