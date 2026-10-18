using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
var builder = WebApplication.CreateBuilder(args);

// Enables controller classes (our endpoints)
builder.Services.AddControllers();

// Swagger: the browser page that lists and tests every endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Load the XML comments file so Swagger shows each endpoint's description
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

// Connect Entity Framework Core to SQL Server using the connection string in appsettings.json
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session support: keeps the logged-in user's ID and role on the server between requests
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // the session ends after 30 minutes of no activity
    options.Cookie.HttpOnly = true;                  // JavaScript cannot read the session cookie
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Show Swagger only while developing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSession();          // must come BEFORE anything that reads the session
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the test project start the whole API later (needed for unit tests)
public partial class Program { }