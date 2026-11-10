using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using RaceDay.Api.Data;

namespace RaceDay_Tests;

// Starts the real API for the tests, but swaps SQL Server for a temporary in-memory database.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // A unique name per factory, so different test classes never share data
    private readonly string _databaseName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server settings that Program.cs registered...
            var existing = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (existing != null)
                services.Remove(existing);

            // ...and use the in-memory database instead
            services.AddDbContext<RaceDayDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}