using Microsoft.EntityFrameworkCore;
using MQTTnet;
using StateMachineDemo.Infrastructure.Persistance;
using StateMachineDemo.Presentation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure MySQL database connection
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("AppDbContext")!;
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Configure MQTT connection options
builder.Services.AddMqtt(services =>
{
    var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithClientId("StateMachineDemo")
        .WithTcpServer("localhost", 1883)
        .Build();
    return mqttClientOptions;
});

// Registreer machine state manager en handlers
builder.Services.AddMachineStateManager();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map application endpoints
app.MapChocoReworkStationEndpoints();

app.Run();
