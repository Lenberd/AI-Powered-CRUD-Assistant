using TaskAssistant.Mvc.Assistant;
using TaskAssistant.Mvc.Configuration;
using TaskAssistant.Mvc.Data;
using TaskAssistant.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<TaskAssistantOptions>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TaskAssistantDb");
});
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));

// Data layer (ADO.NET only - no ORM).
builder.Services.AddScoped<ITaskRepository, SqlTaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

// AI assistant layer. The Gemini client gets its own HttpClient with a hard timeout so a hung
// AI call can never hang the request indefinitely.
builder.Services.AddHttpClient<IAiAssistantClient, GeminiAiClient>(client =>
{
    // Kept comfortably above Gemini:TimeoutSeconds so GeminiAiClient's own linked
    // CancellationTokenSource always fires first and produces the friendly
    // AiUnavailableException, rather than racing an ambiguous HttpClient timeout.
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddScoped<AssistantOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var connectionString = builder.Configuration.GetConnectionString("TaskAssistantDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:TaskAssistantDb.");
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.InitializeAsync(connectionString, logger);
}

app.Run();
