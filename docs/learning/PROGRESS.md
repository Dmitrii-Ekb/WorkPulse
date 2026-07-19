# Прогресс обучения WorkPulse

Обновлено: **2026-07-19**

## Текущая точка

WorkPulse — работающий Minimal API на .NET 9 с постоянным хранением задач в SQLite через Entity Framework Core 9.0.17.

Текущий CRUD полностью работает и использует асинхронные операции EF Core. Ученик увидел и разобрал SQL, который EF Core создаёт для чтения, создания, изменения и удаления задач.

Завершена практическая тема:

```text
LINQ на C#
    ↓
EF Core строит SQL-команду
    ↓
SQLite выполняет SQL
    ↓
EF Core создаёт C#-объекты
    ↓
Minimal API формирует HTTP-ответ
```

Следующая учебная точка — **короткая практика написания базового SQL вручную на текущей SQLite-базе**, после чего перейти ко второй миграции на полезном изменении модели.

`LEARNING.md` менять не требуется: правила обучения не изменились.

## Актуальное состояние репозитория

Репозиторий:

```text
Dmitrii-Ekb/WorkPulse
```

Основная ветка:

```text
master
```

Последние завершённые коммиты перед текущим занятием:

```text
9bfd87dca77bbc03121e65769695a472138cbf47
refactor: make EF Core operations asynchronous

df5fae7357da420f179197bdebd480aefa524ef1
docs: synchronize learning documents after asynchronous step
```

## Текущее состояние проекта

### Стек

- .NET 9;
- C#;
- ASP.NET Core Minimal API;
- Entity Framework Core 9.0.17;
- SQLite;
- `dotnet-ef` 9.0.17;
- HTTP-запросы через `WorkPulse.http`.

### Модель базы данных

Сущность находится в отдельном файле `WorkTask.cs`:

```csharp
namespace WorkPulse;

public class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
}
```

Соответствие модели таблице:

```text
WorkTask.Id          → Tasks.Id
WorkTask.Title       → Tasks.Title
WorkTask.IsCompleted → Tasks.IsCompleted
```

### Входной DTO

Для создания задачи используется `CreateWorkTaskDto`:

```csharp
namespace WorkPulse;

public class CreateWorkTaskDto
{
    public string Title { get; set; } = "";
}
```

Цепочка создания:

```text
JSON
  ↓
CreateWorkTaskDto
  ↓ валидация
new WorkTask
  ↓
EF Core
  ↓
SQLite
```

Клиент управляет только `Title`.

- `Id` назначает SQLite;
- `IsCompleted` при создании устанавливает сервер;
- DTO не входит в модель EF Core и не требует миграции.

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
- проверяется на `null`, пустое значение и пробелы;
- передаётся в `UseSqlite`;
- при неправильной настройке приложение завершается с понятной ошибкой.

### Миграции

Создана и применена миграция:

```text
InitialCreate
```

Она создаёт таблицу `Tasks`:

- `Id` — `INTEGER`, первичный ключ, `AUTOINCREMENT`;
- `Title` — `TEXT NOT NULL`;
- `IsCompleted` — `INTEGER NOT NULL`.

Локальные файлы SQLite исключены через `.gitignore`, а папка `Migrations` хранится в Git.

## Реализованные маршруты

- `GET /hello`;
- `GET /tasks`;
- `GET /tasks/first`;
- `GET /tasks/{id}`;
- `POST /tasks`;
- `PATCH /tasks/{id}/complete`;
- `DELETE /tasks/{id}`.

### `GET /tasks`

```csharp
app.MapGet("/tasks", async (WorkPulseDbContext db) =>
{
    return await db.Tasks.ToListAsync();
});
```

Пример SQL:

```sql
SELECT "t"."Id", "t"."IsCompleted", "t"."Title"
FROM "Tasks" AS "t";
```

### `GET /tasks/first`

```csharp
var task = await db.Tasks
    .OrderBy(task => task.Id)
    .FirstOrDefaultAsync();
```

`OrderBy` строит запрос, а `FirstOrDefaultAsync` выполняет его.

Пример SQL:

```sql
SELECT "t"."Id", "t"."IsCompleted", "t"."Title"
FROM "Tasks" AS "t"
ORDER BY "t"."Id"
LIMIT 1;
```

### `GET /tasks/{id}`

```csharp
var task = await db.Tasks.FindAsync(id);
```

Поиск выполняется по первичному ключу. При отсутствии задачи возвращается `null`, после чего endpoint отвечает `404 Not Found`.

### `POST /tasks`

```csharp
db.Tasks.Add(newTask);
await db.SaveChangesAsync();
```

- `Add` переводит сущность в состояние `Added`;
- `SaveChangesAsync` отправляет `INSERT`;
- SQLite возвращает созданный `Id`;
- EF Core записывает его в `newTask.Id`.

Пример SQL:

```sql
INSERT INTO "Tasks" ("IsCompleted", "Title")
VALUES (@p0, @p1)
RETURNING "Id";
```

### `PATCH /tasks/{id}/complete`

```csharp
var task = await db.Tasks.FindAsync(id);
task.IsCompleted = true;
await db.SaveChangesAsync();
```

Последовательность:

1. `FindAsync` выполняет `SELECT`;
2. свойство меняется в C#-объекте;
3. EF Core замечает изменение;
4. `SaveChangesAsync` выполняет `UPDATE`.

Пример SQL:

```sql
UPDATE "Tasks"
SET "IsCompleted" = @p0
WHERE "Id" = @p1
RETURNING 1;
```

`Title` не попадает в `SET`, потому что он не изменялся.

### `DELETE /tasks/{id}`

```csharp
var task = await db.Tasks.FindAsync(id);
db.Tasks.Remove(task);
await db.SaveChangesAsync();
```

- `FindAsync` загружает сущность с её `Id`;
- `Remove` переводит сущность в состояние `Deleted`;
- `SaveChangesAsync` отправляет `DELETE`;
- первичный ключ берётся из отслеживаемого объекта.

Пример SQL:

```sql
DELETE FROM "Tasks"
WHERE "Id" = @p0
RETURNING 1;
```

## Завершённая тема: SQL, создаваемый EF Core

### Цепочка выполнения

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

### Построение запроса и выполнение

Не выполняют запрос немедленно:

```csharp
db.Tasks
.Where(...)
.OrderBy(...)
```

Они строят описание будущего запроса.

Выполняют запрос:

```csharp
ToListAsync()
FirstOrDefaultAsync()
FindAsync()
SaveChangesAsync()
```

### Эксперимент с отложенным выполнением

Проверено на практике:

```csharp
var query = db.Tasks
    .OrderBy(task => task.Id);

Console.WriteLine("Запрос построен");

var tasks = await query.ToListAsync();

Console.WriteLine("Запрос выполнен");
```

Порядок сообщений:

```text
Запрос построен
SQL-команда EF Core
Запрос выполнен
```

Следовательно, переменная `query` содержала не список задач, а описание будущего запроса.

### `Where`

Пример LINQ:

```csharp
.Where(task => !task.IsCompleted)
```

Пример SQL:

```sql
WHERE NOT ("t"."IsCompleted")
```

`Where` добавляет условие фильтрации, но сам не отправляет запрос в SQLite.

### `OrderBy`

Пример LINQ:

```csharp
.OrderBy(task => task.Id)
```

Пример SQL:

```sql
ORDER BY "t"."Id"
```

Сортировку выполняет SQLite. C# получает уже отсортированный результат.

### `FirstOrDefaultAsync`

Метод:

- возвращает первый найденный объект;
- ограничивает результат через `LIMIT 1`;
- возвращает `null`, если объект не найден;
- не бросает исключение только из-за отсутствия строк.

### SQL-параметры

Пример:

```sql
VALUES (@p0, @p1)
```

EF Core создаёт в оперативной памяти объект команды, в котором находятся:

```text
CommandText
Parameters
```

Упрощённо:

```text
SQL: INSERT ... VALUES (@p0, @p1)

Parameters:
@p0 = false
@p1 = "Название задачи"
```

Связку определяет EF Core:

```sql
("IsCompleted", "Title")
VALUES (@p0, @p1)
```

Имена `@p0`, `@p1`, `@__p_0` временные и сами по себе не имеют предметного смысла.

Значения скрываются в обычных логах знаком `?`, потому что параметры могут содержать конфиденциальные данные.

### Сопоставление CRUD и SQL

```text
ToListAsync / FirstOrDefaultAsync / FindAsync → SELECT
Add + SaveChangesAsync                       → INSERT
изменение свойства + SaveChangesAsync        → UPDATE
Remove + SaveChangesAsync                    → DELETE
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
- `PATCH` сохраняет `IsCompleted = true`;
- `DELETE` возвращает `204 No Content`;
- повторный запрос удалённой задачи возвращает `404`;
- данные сохраняются после перезапуска приложения;
- асинхронность не изменила HTTP-контракт;
- после временных SQL-экспериментов `GET /tasks` возвращён в исходное состояние.

## Уже изучено на практике

### C#

- переменные и `var`;
- массивы и `List<T>`;
- классы, объекты и свойства;
- ссылочная природа объектов на базовом уровне;
- `null`;
- строки и `string.IsNullOrWhiteSpace`;
- условия и ранний `return`;
- интерполяция строк;
- лямбда-выражения;
- базовый LINQ;
- `Where`;
- `OrderBy`;
- `FirstOrDefault` и `FirstOrDefaultAsync`;
- `ToList` и `ToListAsync`;
- создание объекта одного типа из данных другого типа;
- разные классы остаются разными типами;
- организация классов по отдельным файлам;
- асинхронные лямбды;
- `Task`, `async`, `await` на базовом практическом уровне.

### Асинхронность

Ученик может объяснить:

- обработчик выполняет код HTTP-запроса;
- поток является исполнителем кода;
- незавершённый `await` приостанавливает продолжение обработчика;
- поток возвращается в пул;
- продолжение может выполнить другой свободный поток;
- `async` не создаёт новый поток автоматически;
- асинхронность полезна при ожидании базы, сети и файлов;
- обычные вычисления не становятся автоматически быстрее;
- зависимые операции выполняются последовательно.

### HTTP и ASP.NET Core

- Minimal API;
- маршруты и обработчики;
- параметры маршрута;
- GET, POST, PATCH, DELETE;
- JSON-запрос и JSON-ответ;
- коды `200`, `201`, `204`, `400`, `404`, базовое понимание `500`;
- DTO входного запроса;
- валидация;
- ограничение полей клиента;
- базовая проблема overposting;
- получение `DbContext` через dependency injection.

### Базы данных и SQL

- база данных и СУБД;
- реляционная таблица;
- строка, столбец, тип данных;
- схема;
- первичный ключ;
- автоинкремент;
- SQLite;
- `SELECT`, `INSERT`, `UPDATE`, `DELETE`;
- `WHERE`, `ORDER BY`, `LIMIT` на уровне чтения сгенерированного SQL;
- SQL-параметры;
- опасность `UPDATE` и `DELETE` без `WHERE`;
- постоянное хранение данных.

### Entity Framework Core

- EF Core как ORM, а не база данных;
- SQLite-провайдер;
- `DbContext`;
- `DbSet<T>`;
- регистрация контекста;
- миграции;
- `Up`, `Down`, `__EFMigrationsHistory`;
- `Add`, `Remove`, изменение свойства;
- состояния сущностей на базовом уровне;
- `SaveChanges` и `SaveChangesAsync`;
- отложенное выполнение LINQ;
- перевод LINQ в SQL;
- материализация результата в C#-объекты;
- полный асинхронный CRUD.

### Git

- `git status`;
- `git add`;
- `git commit`;
- `git push`;
- локальный коммит и отправка на GitHub;
- миграции хранятся в Git;
- локальная SQLite-база не хранится в Git;
- после завершённого этапа ученик самостоятельно выполняет commit и push.

## Проведённые эксперименты и найденные ошибки

1. Индекс массива был ошибочно принят за `Id` сущности.
2. Несуществующий индекс вызвал `IndexOutOfRangeException`.
3. Поиск по свойству `Id` заменил зависимость от позиции элемента.
4. `FirstOrDefault` вернул объект или `null`.
5. Анонимный объект нельзя изменять как обычную сущность.
6. Массив не поддерживает `Add`, поэтому использован `List<T>`.
7. Неверное тело HTTP-запроса не заполнило `Title`.
8. Валидация должна выполняться до добавления сущности.
9. Клиентский `Id` оказался небезопасным при хранении в списке.
10. `Max` на пустом списке потребовал отдельной обработки.
11. Изменение объекта через другую переменную изменило тот же объект.
12. Хранение в памяти потеряло данные после перезапуска.
13. `Microsoft.EntityFrameworkCore.Design` не устанавливает `dotnet ef` автоматически.
14. Создана и разобрана первая миграция.
15. После `database update` появилась реальная таблица SQLite.
16. Без `SaveChanges` изменения не записываются в базу.
17. Небезопасный `/tasks/first` заменён вариантом с `404`.
18. Неверная строка подключения дала раннюю понятную ошибку.
19. Перенос `WorkTask` в отдельный файл не потребовал миграции.
20. `CreateWorkTaskDto` нельзя передать в `DbSet<WorkTask>`.
21. Явное создание `WorkTask` отделило DTO от сущности.
22. Дополнительные JSON-поля не управляют сущностью через DTO.
23. DTO не потребовал миграции.
24. Асинхронные методы сохранили прежнее HTTP-поведение.
25. `Add` и `Remove` не выполняют SQL немедленно.
26. `SaveChangesAsync` выполняет реальные команды изменения.
27. `OrderBy` добавил `ORDER BY`, но не выполнил запрос.
28. SQL появился только при `ToListAsync`.
29. `Where` добавил условие `WHERE`.
30. `FirstOrDefaultAsync` добавил `LIMIT 1`.
31. Отсутствие совпадений вернуло `null`.
32. `INSERT` использовал параметры и `RETURNING "Id"`.
33. `UPDATE` изменил только `IsCompleted`.
34. `DELETE` использовал первичный ключ отслеживаемой сущности.
35. Значения параметров в обычном логе были скрыты знаком `?`.

## Введено, но пока не считать уверенно изученным

- методы класса как отдельная тема;
- конструкторы глубже текущего использования;
- значимые и ссылочные типы системно;
- nullable reference types;
- исключения глубже `InvalidOperationException`;
- делегаты;
- обобщения системно;
- жизненный цикл `DbContext` глубже текущей практики;
- состояния EF Core глубже `Added`, `Modified`, `Deleted`;
- ручное написание SQL;
- внешние ключи;
- связи таблиц;
- `JOIN`;
- индексы;
- уникальные ограничения;
- транзакции;
- DTO ответа;
- middleware;
- контроллеры;
- автоматическое тестирование;
- авторизация;
- Docker и развёртывание;
- архитектурные паттерны.

## Уровень и стиль обучения

Исходный уровень:

- основы синтаксиса C# знакомы;
- ООП почти с нуля;
- Web API и ASP.NET Core с нуля;
- Git на начальном уровне;
- архитектура почти с нуля.

Подход:

- обучение через один реальный проект WorkPulse;
- один небольшой шаг за раз;
- сначала изменение и эксперимент, затем объяснение;
- ученик регулярно формулирует происходящее своими словами;
- тема не считается изученной после копирования кода;
- теория вводится только при практической необходимости;
- не вводить сложную архитектуру раньше времени;
- после завершённого этапа предложить название коммита;
- commit и push ученик выполняет самостоятельно;
- Git подробно разбирать только при ошибке или новой теме.

## Следующий конкретный шаг

### Короткая ручная практика SQL

На текущей базе `workpulse.db` выполнить несколько запросов вручную вне production-кода:

1. простой `SELECT` всех задач;
2. `SELECT` с `WHERE`;
3. `SELECT` с `ORDER BY`;
4. один безопасный эксперимент с `INSERT` на тестовой задаче;
5. `UPDATE` этой тестовой задачи с обязательным `WHERE`;
6. `DELETE` этой тестовой задачи с обязательным `WHERE`.

Цель — не глубокий курс SQL, а подтверждение понимания уже увиденных команд без посредничества EF Core.

После этого:

### Вторая миграция

Выбрать одно полезное поле задачи, вероятнее всего `CreatedAt`, `Description`, `DueDate` или `Priority`, и пройти полный цикл:

1. изменить `WorkTask`;
2. определить, нужно ли менять DTO;
3. создать миграцию;
4. прочитать `Up` и `Down`;
5. применить миграцию;
6. проверить старые и новые данные;
7. объяснить переход схемы.

### Небольшая техническая уборка

При подходящем коротком шаге привести `WorkPulse.http` к повторяемому набору запросов без случайных экспериментальных `Id` и временных некорректных тел.
