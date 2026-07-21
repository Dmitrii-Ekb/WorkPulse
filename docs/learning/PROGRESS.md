# Прогресс обучения WorkPulse

Обновлено: **2026-07-22**

## Текущая точка

WorkPulse — работающий Minimal API на .NET 9 с хранением задач в SQLite через Entity Framework Core.

Завершены два первых интеграционных сценария для `POST /tasks`:

```text
корректный запрос
→ 201 Created
→ корректный WorkTaskResponseDto
→ задача действительно сохранена в тестовой базе
```

```text
пустой Title
→ 400 Bad Request
→ тестовая база остаётся пустой
```

Следующая крупная тема — **редактирование заголовка и описания задачи**.

Перед реализацией нужно разобрать:

- различие `PUT` и `PATCH`;
- какой контракт запроса нужен для редактирования;
- как валидировать новый заголовок;
- как проверить изменение интеграционным тестом.

## Состояние Git

Репозиторий:

```text
Dmitrii-Ekb/WorkPulse
```

Основная ветка:

```text
master
```

Последние подтверждённые коммиты:

```text
902444834f4cca7aa9fdbd00dad24eb374d98aef
test: add invalid task creation scenario

a3d748d3166b3e310d720394daaffbfe7ccf3748
test: add integration test for task creation

ca52eeec32bd4034d6bd86d5c8e6acdc069020b0
docs: synchronize learning documents after response DTO step

551acc95023d5e2d3c255484ab52ce0da7ac0055
refactor: add response DTO for task endpoints
```

## Текущий стек

- .NET 9;
- C#;
- ASP.NET Core Minimal API;
- Entity Framework Core 9.0.17;
- SQLite;
- LINQ;
- xUnit;
- `Microsoft.AspNetCore.Mvc.Testing` 9.0.18;
- `WebApplicationFactory`;
- Rider;
- Git и GitHub.

## Структура решения

В solution находятся два проекта:

```text
WorkPulse
WorkPulse.IntegrationTests
```

Основной проект содержит API и работу с SQLite.

Тестовый проект содержит интеграционные тесты и ссылается на основной проект через `ProjectReference`.

## Актуальная модель

```text
CreateWorkTaskDto
клиент → API

WorkTask
API ↔ EF Core ↔ SQLite

WorkTaskResponseDto
API → клиент
```

Сущность `WorkTask` больше не возвращается клиенту напрямую.

## Актуальные маршруты

- `GET /hello`;
- `GET /tasks`;
- `GET /tasks/first`;
- `GET /tasks/{id}`;
- `POST /tasks`;
- `PATCH /tasks/{id}/complete`;
- `DELETE /tasks/{id}`.

## Создание задачи

Текущая цепочка:

```text
JSON
↓
CreateWorkTaskDto
↓
валидация Title
↓
WorkTask
↓
SaveChangesAsync
↓
SQLite назначает Id
↓
WorkTaskResponseDto
↓
201 Created
```

Если `Title` пустой или состоит только из пробелов, endpoint возвращает `400 Bad Request` до сохранения в базу.

## База данных и миграции

Используются миграции:

```text
InitialCreate
AddTaskDescription
```

Закреплено:

```text
ModelSnapshot
— последнее состояние модели, известное механизму миграций

Up
— применение изменения

Down
— обратная операция миграции

__EFMigrationsHistory
— список миграций, применённых к конкретной базе
```

Изменение свойства сущности может потребовать миграцию. Изменение только DTO миграции не требует.

## SQL и LINQ

Разобрана цепочка:

```text
LINQ
→ EF Core
→ SQL
→ SQLite
→ C#-объекты
```

Закреплено:

- `Where` добавляет фильтрацию;
- `Select` преобразует результат;
- `OrderBy` задаёт сортировку;
- конечные async-операции выполняют запрос;
- LINQ-запрос обычно выполняется отложенно;
- `SaveChangesAsync` формирует команды изменения;
- без `SaveChangesAsync` объект может существовать в памяти, но строки в базе не будет.

## Интеграционное тестирование

Создан проект:

```text
WorkPulse.IntegrationTests
```

Используются:

- xUnit;
- `Microsoft.AspNetCore.Mvc.Testing`;
- `WebApplicationFactory<Program>`;
- SQLite in-memory.

В `Program.cs` добавлен доступный тестовому хосту `public partial class Program`.

### Тестовая фабрика

`CustomWebApplicationFactory`:

- запускает настоящее приложение внутри тестового процесса;
- создаёт тестовый `HttpClient`;
- заменяет рабочую регистрацию `WorkPulseDbContext`;
- регистрирует отдельное SQLite-соединение `Data Source=:memory:`;
- держит соединение открытым во время теста.

Ключевая модель:

```text
new CustomWebApplicationFactory()
→ новое SqliteConnection
→ новая отдельная SQLite-база в памяти
```

`scope` не создаёт отдельную базу. Он задаёт область жизни `WorkPulseDbContext` и позволяет получить контекст из DI.

### Структура теста

```text
Arrange
→ подготовка приложения, базы и входных данных

Act
→ выполнение HTTP-запроса

Assert
→ проверка результата
```

### Текущие тесты

#### `Hello_ReturnsExpectedResponse`

Проверяет запуск приложения, `GET /hello`, `200 OK`, ожидаемый текст и доступность таблицы `Tasks`.

#### `CreateTask_ReturnsCreatedTask`

Проверяет:

- `POST /tasks`;
- `201 Created`;
- чтение JSON как `WorkTaskResponseDto`;
- `Id > 0`;
- правильные `Title` и `Description`;
- `IsCompleted == false`;
- фактическое сохранение строки в тестовой базе.

Различие:

```text
createdTask
→ данные, возвращённые API

savedTask
→ данные, повторно прочитанные из SQLite
```

Экспериментально подтверждено:

```text
убрали SaveChangesAsync
→ Id остался 0
→ интеграционный тест упал
```

#### `CreateTask_WithEmptyTitle_ReturnsBadRequest`

Проверяет:

- запрос с `Title = ""`;
- `400 Bad Request`;
- `Tasks.CountAsync() == 0`;
- некорректная задача не сохраняется.

### Изоляция тестов

Весь набор тестов дважды подряд прошёл успешно.

Причина изоляции:

```text
каждый тест создаёт собственную фабрику
→ фабрика создаёт собственное соединение
→ соединение относится к собственной базе в памяти
```

Отдельный `scope` управляет жизнью `WorkPulseDbContext`, но не является причиной создания отдельной базы.

## Введённые элементы ООП

Конструкторы и ООП ещё не проходились как отдельная полноценная тема.

На практическом примере введён конструктор:

```csharp
public CustomWebApplicationFactory()
{
    _connection.Open();
}
```

Текущая модель понимания:

```text
new CustomWebApplicationFactory()
→ создаётся объект
→ автоматически вызывается конструктор
→ открывается SQLite-соединение
```

Также введено поле класса с `private readonly`. Эти конструкции пока считаются инфраструктурным шаблоном. Полный блок по ООП должен быть пройден позже системно.

## Проверенное понимание

Ученик может объяснить:

- роли request DTO, entity и response DTO;
- почему изменение DTO не требует миграции;
- почему сущность не нужно возвращать напрямую;
- цепочку LINQ → EF Core → SQL → SQLite;
- роль `SaveChangesAsync`;
- различие unit- и интеграционного теста;
- структуру Arrange, Act, Assert;
- роль `WebApplicationFactory` и тестового `HttpClient`;
- зачем нужна отдельная тестовая база;
- различие ответа API и состояния базы;
- почему отдельная фабрика даёт отдельную базу;
- почему `scope` не равен отдельной базе.

## Технические замечания

При ближайшем рефакторинге можно:

- удалить неиспользуемый `using Xunit.Abstractions`, если диагностический вывод не используется;
- добавить завершающий перевод строки в новые файлы;
- позже оценить повторение Arrange-кода;
- привести `WorkPulse.http` к повторяемым запросам без жёстко заданных идентификаторов.

Не создавать сложную тестовую архитектуру преждевременно.

## Следующий этап

### Редактирование задачи

Нужно реализовать изменение:

- заголовка;
- описания.

Перед кодом разобрать:

1. Семантику `PUT`.
2. Семантику `PATCH`.
3. Полная это замена или частичное изменение.
4. Как будет выглядеть DTO запроса.
5. Как валидировать заголовок.
6. Какой статус вернуть для отсутствующей задачи.
7. Как подтвердить изменение интеграционным тестом.

Первый практический шаг:

```text
сравнить PUT и PATCH
→ выбрать контракт редактирования WorkPulse
→ только затем писать endpoint и DTO
```
