using Microsoft.EntityFrameworkCore;
using WorkPulse;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkPulseDbContext>(options =>
    options.UseSqlite("Data Source=workpulse.db"));

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

app.MapPost("/tasks", (WorkTask task, WorkPulseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(task.Title))
    {
        return Results.BadRequest("Название задачи обязательно");
    }

    db.Tasks.Add(task);
    db.SaveChanges();

    return Results.Created($"/tasks/{task.Id}", task);
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


public class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
}