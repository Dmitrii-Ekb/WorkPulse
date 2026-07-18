using Microsoft.EntityFrameworkCore;
using WorkPulse;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("WorkPulseDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Не найдена строка подключения " +
        "'ConnectionStrings:WorkPulseDatabase'. " +
        "Проверь файл appsettings.json.");
}

builder.Services.AddDbContext<WorkPulseDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

app.MapGet("/hello", () =>
{
    return "Привет, это мой АПИ";
});

app.MapGet("/tasks", (WorkPulseDbContext db) =>
{
    return db.Tasks.ToList();
});

app.MapGet("/tasks/first", (WorkPulseDbContext db) =>
{
    var task = db.Tasks
        .OrderBy(task => task.Id)
        .FirstOrDefault();

    if (task is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(task);
});

app.MapGet("/tasks/{id}", (int id, WorkPulseDbContext db) =>
{
    var task = db.Tasks.Find(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(task);
});

app.MapPost("/tasks", (CreateWorkTaskDto request, WorkPulseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Название задачи обязательно");
    }
    
    var newTask = new WorkTask
    {
        Title = request.Title,
        IsCompleted = false
    };

    db.Tasks.Add(newTask);
    db.SaveChanges();

    return Results.Created($"/tasks/{newTask.Id}", newTask);
});

app.MapPatch("/tasks/{id}/complete", (int id, WorkPulseDbContext db) =>
{
    var task = db.Tasks.Find(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    task.IsCompleted = true;
    db.SaveChanges();

    return Results.Ok(task);
});

app.MapDelete("/tasks/{id}", (int id, WorkPulseDbContext db) =>
{
    var task = db.Tasks.Find(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    db.Tasks.Remove(task);
    db.SaveChanges();

    return Results.NoContent();
});

app.Run();