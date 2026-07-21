using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace WorkPulse.IntegrationTests;

public class WorkPulseApiTests
{
    [Fact]
    public async Task Hello_ReturnsExpectedResponse()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WorkPulseDbContext>();

        await db.Database.EnsureCreatedAsync();
        
        // Act
        var response = await client.GetAsync("/hello");
        var content = await response.Content.ReadAsStringAsync();
        var taskCount = await db.Tasks.CountAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Привет, это мой АПИ", content);
        Assert.Equal(0, taskCount);
    }
    
    [Fact]
    public async Task CreateTask_ReturnsCreatedTask()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WorkPulseDbContext>();

        await db.Database.EnsureCreatedAsync();

        var request = new CreateWorkTaskDto
        {
            Title = "Изучить интеграционные тесты",
            Description = "Создать первый тест POST /tasks"
        };
        
        // Act
        var response = await client.PostAsJsonAsync("/tasks", request);
        var createdTask = await response.Content.ReadFromJsonAsync<WorkTaskResponseDto>();
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(createdTask);
        Assert.True(createdTask.Id > 0);
        Assert.Equal(request.Title, createdTask.Title);
        Assert.Equal(request.Description, createdTask.Description);
        Assert.False(createdTask.IsCompleted);
        
        var savedTask = await db.Tasks
            .SingleAsync(task => task.Id == createdTask.Id);

        Assert.Equal(request.Title, savedTask.Title);
        Assert.Equal(request.Description, savedTask.Description);
        Assert.False(savedTask.IsCompleted);
    }
}