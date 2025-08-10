using Server;
using Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureWebhost();
builder.RegisterServices();

var app = builder.Build();
app.AddRequestLoggingMiddleware();
app.ConfigureWebApp();
app.RegisterEndpoints();
app.Run();