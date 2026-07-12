var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var tasks = new List<WorkTask>();

app.MapGet("/hello", () =>
{
    return "Привет, это мой АПИ";
});

app.MapGet("/tasks", () =>
{
    return tasks;
});

app.MapGet("/tasks/first", () =>
{
    return tasks[0];
});

app.MapGet("/tasks/{id}", (int id) =>
{
    var task = tasks.FirstOrDefault(task => task.Id == id);

    if (task is null)
    {
        return Results.NotFound();
    }
    
    return Results.Ok(task);
});

app.MapPost("/tasks", (WorkTask task) =>
{
    if (string.IsNullOrWhiteSpace(task.Title))
    {
        return Results.BadRequest("Название задачи обязательно");
    }
    
    if (tasks.Count == 0)
    {
        task.Id = 1;
    }
    else
    {
        task.Id = tasks.Max(existingTask => existingTask.Id) + 1;
    }
    
    tasks.Add(task);
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapPatch("/tasks/{id}/complete", (int id) =>
{
    var task = tasks.FirstOrDefault(task => task.Id == id);

    if (task is null)
    {
        return Results.NotFound();
    }

    task.IsCompleted = true;

    return Results.Ok(task);
});

app.MapDelete("/tasks/{id}", (int id) =>
{
    var task = tasks.FirstOrDefault(task => task.Id == id);
    
    if (task is null)
    {
        return Results.NotFound();
    }
    
    tasks.Remove(task);
    
    return Results.NoContent();
});

app.Run();


class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
}