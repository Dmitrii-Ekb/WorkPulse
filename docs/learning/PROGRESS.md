# Прогресс обучения WorkPulse

Обновлено: **2026-07-21**

## Текущая точка

WorkPulse — работающий Minimal API на .NET 9 с постоянным хранением задач в SQLite через Entity Framework Core 9.0.17.

Завершён этап разделения внутренней модели и HTTP-контрактов. Теперь используются три типа с разными ролями:

```text
CreateWorkTaskDto
клиент → API

WorkTask
API ↔ EF Core ↔ SQLite

WorkTaskResponseDto
API → клиент
```

Сущность `WorkTask` больше не возвращается клиенту напрямую.

Текущая цепочка создания задачи:

```text
JSON-запрос
    ↓
CreateWorkTaskDto
    ↓
валидация
    ↓
WorkTask
    ↓
EF Core и SQLite
    ↓
WorkTaskResponseDto
    ↓
JSON-ответ
```

Следующая крупная тема — **первый интеграционный тест API**.

## Состояние Git

Репозиторий:

```text
Dmitrii-Ekb/WorkPulse
```

Основная ветка:

```text
master
```

Завершённый функциональный коммит текущего этапа:

```text
refactor: add response DTO for task endpoints
```

На момент подготовки документации GitHub-коннектор ещё не показывал этот локальный коммит в удалённой ветке. Документация составлена по фактическому коду, присланному после коммита.

Предыдущие подтверждённые коммиты:

```text
51b6bf762850a0b40919f3633723610075808f34
feat: add optional task description

eaee8dc8f1ddc18fdfb6f1d291c9ad8df5f8ce7d
docs: synchronize learning documents after task description migration
```

## Текущий стек

- .NET 9;
- C#;
- ASP.NET Core Minimal API;
- Entity Framework Core 9.0.17;
- SQLite;
- `dotnet-ef` 9.0.17;
- LINQ;
- `WorkPulse.http`;
- Rider Database Tools.

## Сущность `WorkTask`

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

Назначение:

- внутренняя модель задачи;
- участвует в модели EF Core;
- сопоставляется с таблицей `Tasks`;
- изменение сохраняемого свойства может потребовать миграцию;
- не является публичным HTTP-контрактом.

## Входной DTO

```csharp
namespace WorkPulse;

public class CreateWorkTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}
```

`CreateWorkTaskDto` описывает, что клиент может прислать при создании задачи.

Клиент управляет:

- `Title`;
- необязательным `Description`.

Клиент не управляет:

- `Id`;
- начальным `IsCompleted`.

DTO не входит в модель EF Core. Изменение только DTO не требует миграции.

## Выходной DTO

```csharp
namespace WorkPulse;

public class WorkTaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }

    public static WorkTaskResponseDto FromEntity(WorkTask task)
    {
        return new WorkTaskResponseDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            IsCompleted = task.IsCompleted
        };
    }
}
```

`WorkTaskResponseDto` описывает данные, которые API возвращает и гарантирует клиенту.

DTO расшифровывается:

```text
Data Transfer Object
объект передачи данных
```

## Почему сущность не возвращается напрямую

Раньше endpoint возвращал:

```csharp
return Results.Ok(task);
```

При таком подходе новое публичное свойство `WorkTask` автоматически могло попасть в JSON.

Риск:

```text
изменили внутреннюю модель
        ↓
непреднамеренно изменили публичный API
```

Возможные последствия:

- публикация внутренних данных;
- нестабильный контракт;
- неожиданные изменения для клиента;
- проблемы у строгих или сгенерированных клиентов.

Теперь сервер явно определяет состав ответа.

## Преобразование сущности в DTO

Для одной загруженной сущности:

```csharp
var response = WorkTaskResponseDto.FromEntity(task);
```

`FromEntity` статический, потому что готового DTO ещё нет. Метод сам создаёт и возвращает объект.

Преобразование вынесено в одно место, чтобы не повторять одинаковый mapping во многих endpoint-ах.

Это практический пример DRY:

```text
Don't Repeat Yourself
не повторяйся
```

## Проекция списка через `Select`

`GET /tasks` использует:

```csharp
var responses = await db.Tasks
    .Select(task => new WorkTaskResponseDto
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        IsCompleted = task.IsCompleted
    })
    .ToListAsync();
```

Новый LINQ-метод:

```text
Where  — фильтрует
Select — преобразует
```

Цепочка:

```text
IQueryable<WorkTask>
        ↓ Select
IQueryable<WorkTaskResponseDto>
        ↓ ToListAsync
List<WorkTaskResponseDto>
```

EF Core переводит проекцию в SQL и запрашивает необходимые свойства.

Обычный метод `FromEntity` не используется внутри запроса к базе, потому что произвольный C#-метод EF Core обычно не может перевести в SQL.

Различие:

```text
уже загруженный WorkTask
→ FromEntity

IQueryable к базе
→ Select с выражением
```

## Актуальные маршруты

- `GET /hello`;
- `GET /tasks`;
- `GET /tasks/first`;
- `GET /tasks/{id}`;
- `POST /tasks`;
- `PATCH /tasks/{id}/complete`;
- `DELETE /tasks/{id}`.

### `GET /tasks`

Возвращает массив `WorkTaskResponseDto` через `Select`.

### `GET /tasks/first`

Получает одну сущность, проверяет `null`, преобразует через `FromEntity` и возвращает один JSON-объект.

Во время занятия была исправлена ошибка: повторный запрос ко всей таблице создавал массив вместо одной задачи.

### `GET /tasks/{id}`

Получает одну сущность через `FindAsync`, возвращает `404`, если задача не найдена, иначе возвращает DTO.

### `POST /tasks`

```csharp
var newTask = new WorkTask
{
    Title = request.Title,
    Description = request.Description,
    IsCompleted = false
};

db.Tasks.Add(newTask);
await db.SaveChangesAsync();

var response = WorkTaskResponseDto.FromEntity(newTask);

return Results.Created($"/tasks/{newTask.Id}", response);
```

DTO создаётся после `SaveChangesAsync`, потому что после сохранения SQLite назначает `Id`.

Проверенный ответ:

```json
{
  "id": 1009,
  "title": "Проверить response DTO",
  "description": "Убедиться, что POST возвращает DTO",
  "isCompleted": false
}
```

Идентификатор относится только к локальной учебной базе.

### `PATCH /tasks/{id}/complete`

После изменения и сохранения возвращает DTO. Проверено значение:

```json
"isCompleted": true
```

### `DELETE /tasks/{id}`

DTO не нужен, потому что endpoint возвращает:

```text
204 No Content
```

## Миграции: уточнённая модель

Создание миграции:

```bash
dotnet ef migrations add MigrationName
```

Применение:

```bash
dotnet ef database update
```

Имя миграции выбирает разработчик. Оно должно описывать изменение.

### `ModelSnapshot`

`WorkPulseDbContextModelSnapshot` хранит последнее состояние модели, известное механизму миграций.

Главное назначение:

```text
текущая C#-модель
        ↕ сравнение
ModelSnapshot
        ↓
следующая миграция
```

Snapshot **не является механизмом отката**.

Откат описывает:

```csharp
Down(...)
```

Применённые миграции хранятся в:

```text
__EFMigrationsHistory
```

Кратко:

```text
ModelSnapshot          — основа следующей миграции
Down                    — обратная операция
__EFMigrationsHistory  — применённые миграции базы
```

Сгенерированную миграцию нужно читать. Её можно осознанно редактировать до применения, если разработчик понимает последствия. Особенно рискованно менять миграцию, уже применённую к общей или production-базе.

## Проверенное понимание

Ученик может объяснить:

```text
CreateWorkTaskDto
— что клиент может прислать

WorkTask
— как задача представлена внутри приложения и EF Core

WorkTaskResponseDto
— что сервер возвращает клиенту
```

Также закреплено:

- DTO — объект передачи данных;
- одинаковые свойства не означают одинаковую ответственность классов;
- внутреннее поле можно добавить только в сущность;
- публичное поле ответа нужно добавить в response DTO;
- изменение сущности может потребовать миграцию;
- изменение только DTO миграции не требует;
- дублирование mapping-кода становится проблемой при росте проекта;
- `Select` сначала был объяснён, затем применён;
- одинаковые механические замены после понимания можно выполнять сразу во всех местах.

## Уже изучено на практике

### C#

- классы, объекты и свойства;
- `null`, `string?`;
- создание объектов;
- статический метод;
- передача и возврат объекта;
- DRY на практическом уровне;
- лямбды;
- LINQ: `Where`, `OrderBy`, `Select`;
- `FirstOrDefaultAsync`, `ToListAsync`;
- `Task`, `async`, `await`.

### ASP.NET Core

- Minimal API;
- входной и выходной JSON;
- входной и выходной DTO;
- сущность как внутренняя модель;
- overposting;
- контролируемый HTTP-контракт;
- коды `200`, `201`, `204`, `400`, `404`.

### EF Core и SQL

- `DbContext`, `DbSet<T>`;
- асинхронный CRUD;
- Change Tracker;
- миграции;
- `Up`, `Down`;
- `ModelSnapshot`;
- `__EFMigrationsHistory`;
- проекция через `Select`;
- различие LINQ-выражения и обычного C#-метода.

## Введено, но пока не считать уверенно изученным

- методы и `static` системно;
- DRY глубже текущего примера;
- expression trees;
- ограничения перевода C# в SQL;
- DTO изменения;
- `record`;
- AutoMapper и компромиссы;
- unit-тесты;
- интеграционные тесты;
- тестовая база;
- `WebApplicationFactory`;
- Arrange, Act, Assert.

## Следующий конкретный шаг

### Первый интеграционный тест

Первый сценарий:

```text
POST /tasks
        ↓
201 Created
        ↓
WorkTaskResponseDto
        ↓
проверка Id, Title, Description, IsCompleted
```

Перед кодом разобрать:

1. автоматический тест;
2. unit-тест и интеграционный тест;
3. Arrange, Act, Assert;
4. отдельную тестовую базу;
5. запуск настоящего приложения через тестовый хост.

Не начинать с полного покрытия CRUD. Первая цель — один надёжный тест успешного создания задачи.
