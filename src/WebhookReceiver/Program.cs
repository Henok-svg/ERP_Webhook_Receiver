using WebhookReceiver.Endpoints;
using WebhookReceiver.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SignatureVerifier>();
builder.Services.AddSingleton<EventStore>();

var app = builder.Build();

app.MapWebhookEndpoints();

app.Run();
