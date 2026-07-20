# Прогресс обучения WorkPulse

Обновлено: **2026-07-20**

## Текущая точка

WorkPulse — работающий Minimal API на .NET 9 с постоянным хранением задач в SQLite через Entity Framework Core 9.0.17.

Текущий CRUD использует асинхронные операции EF Core. Завершены две связанные практические темы:

1. базовые SQL-команды написаны вручную на текущей SQLite-базе;
2. модель задачи расширена необязательным описанием через вторую миграцию.

Текущая вертикальная цепочка:

```text
HTTP-запрос
    ↓
CreateWorkTaskDto
    ↓
валидация
    ↓
WorkTask
    ↓
DbContext и EF Core
    ↓
SQL-команда с параметрами
    ↓
SQLite
    ↓
C#-объект
    ↓
HTTP-ответ
```

Новое поле `Description` прошло полный цикл:

```text
JSON description
    ↓
CreateWorkTaskDto.Description
    ↓
WorkTask.Description
    ↓
EF Core
    ↓
Tasks.Description
    ↓
GET возвращает description
```

Следующая учебная точка — обсудить границы HTTP-контракта и решить, когда нужен отдельный DTO ответа. После этого перейти к первому интеграционному тесту.

## Актуальное состояние репозитория

Репозиторий:

```text
Dmitrii-Ekb/WorkPulse
```

Основная ветка:

```text
master
```

Последний завершённый коммит в удалённом репозитории перед текущими локальными изменениями:

```text
16e95af72bda0ba81ff0af685637990c28bd3c83
docs: synchronize learning documents after SQL inspection
```

Текущие изменения занятия пока могут находиться только в локальной рабочей копии:

- добавлено `WorkTask.Description`;
- добавлено `CreateWorkTaskDto.Description`;
- обновлён `POST /tasks`;
- создана миграция `AddTaskDescription`;
- миграция применена к локальной базе;
- проверены создание и чтение задач с описанием и без него.

## Текущее состояние проекта

### Стек

- .NET 9;
- C#;
- ASP.NET Core Minimal API;
- Entity Framework Core 9.0.17;
- SQLite;
- `dotnet-ef` 9.0.17;
- HTTP-запросы через `WorkPulse.http`;
- Rider Database Tools для просмотра SQLite и выполнения ручного SQL.

### Модель задачи

Сущность находится в `WorkTask.cs`:

```csharp
namespace WorkPulse;

public class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
}
```

Назначение свойств:

```text
Id           — идентификатор, назначается SQLite
Title        — обязательный заголовок
Description  — необязательное описание
IsCompleted  — признак завершения
```

Соответствие модели таблице:

```text
WorkTask.Id           → Tasks.Id
WorkTask.Title        → Tasks.Title
WorkTask.Description  → Tasks.Description
WorkTask.IsCompleted  → Tasks.IsCompleted
```

### Nullable-свойство

```csharp
public string? Description { get; set; }
```

Свойство может содержать строку или `null`. В текущем продуктовом сценарии `null` означает, что описание пока не указано.

Инициализатор `= "";` не требуется, потому что `null` является допустимым состоянием свойства.

Пустая строка и `null` не считаются полностью одинаковыми. Нормализация пустого описания пока не реализована и будет обсуждаться при развитии валидации.

### Входной DTO

```csharp
namespace WorkPulse;

public class CreateWorkTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}
```

Клиент управляет:

- `Title`;
- необязательным `Description`.

Клиент не управляет:

- `Id`;
- начальным значением `IsCompleted`.

DTO не входит в модель EF Core. Изменение только DTO не требует миграции.

### Контекст базы данных

```csharp
public class WorkPulseDbContext : DbContext
{
    public WorkPulseDbContext(
        DbContextOptions<WorkPulseDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkTask> Tasks { get; set; } = null!;
}
```

### Конфигурация подключения

В `appsettings.json`:

```json
"ConnectionStrings": {
  "WorkPulseDatabase": "Data Source=workpulse.db"
}
```

В `Program.cs` строка подключения:

- читается через `GetConnectionString("WorkPulseDatabase")`;
- проверяется на `null`, пустую строку и пробелы;
- передаётся в `UseSqlite`;
- при неправильной настройке приложение завершается с понятной ошибкой.

## Схема базы данных

### Таблица `Tasks`

```text
Id           INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
Title        TEXT NOT NULL
Description  TEXT NULL
IsCompleted  INTEGER NOT NULL
```

`Description` допускает `NULL`, поэтому после применения миграции существующие задачи сохранились, а новое значение у них стало `NULL`.

### Миграции

Созданы и применены:

```text
InitialCreate
AddTaskDescription
```

Миграция `AddTaskDescription`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "Description",
        table: "Tasks",
        type: "TEXT",
        nullable: true);
}
```

Откат:

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "Description",
        table: "Tasks");
}
```

При выполнении `Down` удаляется колонка и все сохранённые в ней описания.

### Как EF Core создаёт миграцию

При выполнении:

```bash
dotnet ef migrations add MigrationName
```

EF Core сравнивает:

```text
текущую модель C#
        ↕
WorkPulseContextModelSnapshot
```

EF Core не определяет изменения путём прямого сравнения C#-модели с текущей SQLite-базой.

Если модель не изменилась, новая миграция обычно будет пустой. Пустая миграция допустима, но без практической причины только засоряет историю.

### Как EF Core узнаёт, что миграция применена

В базе существует служебная таблица:

```text
__EFMigrationsHistory
```

После успешного `database update` EF Core записывает туда идентификатор миграции.

```text
файл миграции
        ↓
dotnet ef database update
        ↓
выполнен Up
        ↓
запись добавлена в __EFMigrationsHistory
```

Ручное изменение схемы базы может рассинхронизировать реальное состояние SQLite и историю EF Core.

## Реализованные маршруты

- `GET /hello`;
- `GET /tasks`;
- `GET /tasks/first`;
- `GET /tasks/{id}`;
- `POST /tasks`;
- `PATCH /tasks/{id}/complete`;
- `DELETE /tasks/{id}`.

### `POST /tasks`

```csharp
app.MapPost("/tasks", async (
    CreateWorkTaskDto request,
    WorkPulseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Название задачи обязательно");
    }

    var newTask = new WorkTask
    {
        Title = request.Title,
        Description = request.Description,
        IsCompleted = false
    };

    db.Tasks.Add(newTask);
    await db.SaveChangesAsync();

    return Results.Created($"/tasks/{newTask.Id}", newTask);
});
```

Путь данных:

```text
JSON
  ↓
CreateWorkTaskDto
  ↓
new WorkTask
  ↓
db.Tasks.Add
  ↓
SaveChangesAsync
  ↓
INSERT
  ↓
SQLite
```

Запрос с описанием успешно вернул `201 Created`, сохранил описание и вернул его в ответе.

Запрос без описания успешно вернул `201 Created`, а свойство получило значение:

```json
"description": null
```

`GET /tasks/{id}` вернул ранее сохранённое описание.

## Завершённая тема: SQL, создаваемый EF Core

Разобрана цепочка:

```text
LINQ-выражение
    ↓
описание запроса
    ↓
метод выполнения
    ↓
SQL
    ↓
SQLite
    ↓
C#-результат
```

Строят запрос, но не выполняют его немедленно:

```csharp
db.Tasks
    .Where(...)
    .OrderBy(...);
```

Выполняют запрос или изменение:

```csharp
ToListAsync()
FirstOrDefaultAsync()
FindAsync()
SaveChangesAsync()
```

Сопоставление CRUD и SQL:

```text
ToListAsync / FirstOrDefaultAsync / FindAsync → SELECT
Add + SaveChangesAsync                       → INSERT
изменение свойства + SaveChangesAsync        → UPDATE
Remove + SaveChangesAsync                    → DELETE
```

## Завершённая тема: ручная практика SQL

SQL выполнялся в Query Console Rider для локального файла `workpulse.db`.

### Выборка всех задач

```sql
SELECT Id, Title, IsCompleted
FROM Tasks;
```

### Фильтрация

```sql
SELECT Id, Title, IsCompleted
FROM Tasks
WHERE IsCompleted = 0;
```

### Сортировка

```sql
SELECT Id, Title, IsCompleted
FROM Tasks
WHERE IsCompleted = 0
ORDER BY Id DESC;
```

### Создание тестовой строки

```sql
INSERT INTO Tasks (Title, IsCompleted)
VALUES ('SQL test task', 0);
```

### Обновление тестовой строки

```sql
UPDATE Tasks
SET IsCompleted = 1
WHERE Id = 1006;
```

### Удаление тестовой строки

```sql
DELETE FROM Tasks
WHERE Id = 1006;
```

После удаления проверочный `SELECT` вернул ноль строк. Тестовая запись удалена, production-код ради ручного SQL не изменялся.

Ученик самостоятельно написал и объяснил:

```text
SELECT       — прочитать данные
WHERE        — отфильтровать строки
ORDER BY     — отсортировать результат
INSERT INTO  — создать строку
UPDATE SET   — изменить данные
DELETE FROM  — удалить строку
```

Критическое правило закреплено:

```text
UPDATE и DELETE без WHERE могут затронуть все строки таблицы.
```

## Проверенное поведение

- `GET /tasks` возвращает список и `200 OK`;
- пустая таблица возвращает пустой список;
- `GET /tasks/first` возвращает первую задачу или `404`;
- `GET /tasks/{id}` возвращает задачу или `404`;
- корректный `POST` возвращает `201 Created`;
- пустой или состоящий из пробелов `Title` возвращает `400 Bad Request`;
- клиентские `id` и `isCompleted` не управляют создаваемой сущностью;
- `Id` назначается SQLite;
- задача может создаваться с описанием и без него;
- отсутствие `description` в JSON приводит к `Description = null`;
- `Description` сохраняется в SQLite и возвращается через GET;
- `PATCH` сохраняет `IsCompleted = true`;
- `DELETE` возвращает `204 No Content`;
- данные сохраняются после перезапуска приложения;
- миграция `AddTaskDescription` сохраняет старые строки;
- старые строки получают `Description = NULL`;
- асинхронность не изменила HTTP-контракт.

## Уже изучено на практике

### C#

- переменные и `var`;
- массивы и `List<T>`;
- классы, объекты и свойства;
- ссылочная природа объектов на базовом уровне;
- `null`;
- nullable reference types на базовом примере `string?`;
- строки и `string.IsNullOrWhiteSpace`;
- условия и ранний `return`;
- интерполяция строк;
- лямда-выражения;
- базовый LINQ;
- `Where`, `OrderBy`, `FirstOrDefault`, `ToList`;
- создание объекта одного типа из данных другого типа;
- организация классов по отдельным файлам;
- `Task`, `async`, `await` на базовом практическом уровне.

### HTTP и ASP.NET Core

- Minimal API;
- маршруты и обработчики;
- GET, POST, PATCH, DELETE;
- JSON-запрос и JSON-ответ;
- коды `200`, `201`, `204`, `400`, `404`;
- DTO входного запроса;
- необязательные свойства входного JSON;
- валидация;
- ограничение полей клиента;
- базовая проблема overposting;
- dependency injection `DbContext`.

### Базы данных и SQL

- база данных и СУБД;
- реляционная таблица;
- строка, столбец, тип данных и схема;
- первичный ключ и автоинкремент;
- SQLite;
- ручные `SELECT`, `WHERE`, `ORDER BY`, `INSERT`, `UPDATE`, `DELETE`;
- SQL-параметры;
- опасность `UPDATE` и `DELETE` без `WHERE`;
- nullable-колонка;
- сохранение старых строк при расширении схемы.

### Entity Framework Core

- EF Core как ORM, а не база данных;
- `DbContext` и `DbSet<T>`;
- `InitialCreate`;
- `AddTaskDescription`;
- `Up`, `Down`, `ModelSnapshot`, `__EFMigrationsHistory`;
- отличие создания миграции от её применения;
- поведение пустой миграции;
- Change Tracker на базовом уровне;
- `Add`, `Remove`, изменение свойства;
- `SaveChangesAsync`;
- отложенное выполнение LINQ;
- перевод LINQ в SQL;
- материализация C#-объектов;
- полный асинхронный CRUD.

## Введено, но пока не считать уверенно изученным

- nullable reference types системно;
- разница между `null`, пустой строкой и отсутствующим JSON-свойством во всех сценариях;
- методы класса как отдельная тема;
- конструкторы глубже текущего использования;
- значимые и ссылочные типы системно;
- исключения глубже текущих примеров;
- делегаты и обобщения системно;
- жизненный цикл `DbContext` глубже текущей практики;
- изменение и удаление миграций в разных состояниях;
- сложный SQL;
- внешние ключи, связи и `JOIN`;
- индексы, уникальные ограничения и транзакции;
- DTO ответа;
- middleware и контроллеры;
- автоматическое тестирование;
- авторизация;
- Docker и развёртывание;
- архитектурные паттерны.

## Уровень и стиль обучения

- обучение через один реальный проект WorkPulse;
- один небольшой шаг за раз;
- сначала изменение или эксперимент, затем объяснение;
- ученик регулярно формулирует происходящее своими словами;
- теория вводится только при практической необходимости;
- не вводить сложную архитектуру раньше времени;
- не выполнять отдельную сборку после очевидного безопасного однострочного изменения;
- выполнять сборку, запуск или тест после значимого набора изменений;
- после завершённого этапа предложить название коммита;
- commit и push ученик выполняет самостоятельно;
- Git подробно разбирать только при ошибке или новой теме.

## Следующий конкретный шаг

### Границы HTTP-контракта

Обсудить текущий ответ:

```csharp
return Results.Created($"/tasks/{newTask.Id}", newTask);
```

Нужно понять:

1. чем сущность отличается от входного и выходного DTO;
2. почему изменение сущности может случайно изменить JSON-контракт;
3. когда отдельный DTO ответа полезен;
4. почему не нужно добавлять AutoMapper преждевременно.

После обсуждения принять осознанное решение:

- либо пока оставить возврат сущности;
- либо создать простой `WorkTaskResponseDto` и выполнить явное преобразование.

### После этого

Создать первый интеграционный тест для сценария:

```text
POST /tasks
    ↓
201 Created
    ↓
проверка тела ответа
    ↓
проверка сохранения данных
```

### Небольшая техническая уборка

Привести `WorkPulse.http` к повторяемому набору запросов:

- не использовать случайные локальные `Id`;
- отделить корректные и некорректные запросы;
- добавить примеры с `description` и без него;
- сохранить маршруты GET, POST, PATCH и DELETE.
