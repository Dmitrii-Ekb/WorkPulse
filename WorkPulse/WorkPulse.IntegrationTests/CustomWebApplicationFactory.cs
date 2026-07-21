using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace WorkPulse.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = 
        new("Data Source=:memory:");
    
    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WorkPulseDbContext>>();
            
            services.AddDbContext<WorkPulseDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }
}