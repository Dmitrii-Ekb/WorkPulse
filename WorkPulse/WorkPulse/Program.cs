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

app.MapGet("/tasks", async (WorkPulseDbContext db) =>
{
    return await db.Tasks.ToListAsync();
});

app.MapGet("/tasks/first", async (WorkPulseDbContext db) =>
{
    var task = await db.Tasks
        .OrderBy(task => task.Id)
        .FirstOrDefaultAsync();

    if (task is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(task);
});

app.MapGet("/tasks/{id}", async (int id, WorkPulseDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(task);
});

app.MapPost("/tasks", async (CreateWorkTaskDto request, WorkPulseDbContext db) =>
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
    await db.SaveChangesAsync();

    return Results.Created($"/tasks/{newTask.Id}", newTask);
});

app.MapPatch("/tasks/{id}/complete", async (int id, WorkPulseDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    task.IsCompleted = true;
    await db.SaveChangesAsync();

    return Results.Ok(task);
});

app.MapDelete("/tasks/{id}", async (int id, WorkPulseDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();