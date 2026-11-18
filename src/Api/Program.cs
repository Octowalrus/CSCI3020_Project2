using Api.Models;
using Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<MongoOptions>(
    builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.AddSingleton<MongoItemStore>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("MauiApp", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("MauiApp");
app.MapControllers();

app.Run();
