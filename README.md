# TimeTraking

Десктопний трекер часу роботи програм для Windows, написаний на **C# (.NET 9)** та **Avalonia UI 12**.

## Можливості

- **Автоматичне відстеження програм** — щосекунди визначає запущені програми з відкритими вікнами та рахує час роботи у форматі `ГГ:ХХ:СС`.
- **Реальні іконки Windows** — автоматично витягує та кешує іконки прямо з `.exe` файлів процесів.
- **Два режими підрахунку** — облік усіх відкритих програм зі списку або перемикач **«Тільки активне вікно»**.
- **Список ігнорування** — будь-який процес можна видалити кнопкою **✕** або **«Видалити і не записувати»**, щоб він більше не фіксувався; у вкладці **«Ігноровані»** його можна будь-коли повернути.
- **Ручне додавання** — через **«+ Додати програму»** можна обрати будь-який запущений процес, вказати `.exe` файл на диску або ввести назву вручну.
- **Автозбереження** — статистика часу, додані та ігноровані процеси зберігаються локально в `%LocalAppData%\TimeTraking\tracker-data.json`.

## Структура проєкту

| Файл | Призначення |
| --- | --- |
| `TimeTraking/Program.cs` | Точка входу та запуск Avalonia Desktop |
| `TimeTraking/App.axaml(.cs)` | Налаштування додатку та темної Fluent-теми |
| `TimeTraking/MainWindow.axaml(.cs)` | Головне вікно, вкладки, таймер відстеження та пошук |
| `TimeTraking/AddProcessWindow.axaml(.cs)` | Діалог додавання процесів або `.exe` файлів |
| `TimeTraking/Models/TrackedApp.cs` | Модель відстежуваної програми з `INotifyPropertyChanged` |
| `TimeTraking/Services/WindowsProcessHelper.cs` | Win32 API: активне вікно, шляхи `.exe` та витяг іконок |
| `TimeTraking/Services/TrackerStorage.cs` | Збереження та завантаження стану в JSON |

## Вимоги

- Windows 10 / 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Запуск

```bash
git clone https://github.com/CrazyDarks/TimeTreking.git
cd TimeTreking
dotnet run --project TimeTraking/TimeTraking.csproj
```

Для збірки без запуску:

```bash
dotnet build
```
