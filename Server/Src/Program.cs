using Server;
using Server.Endpoints;
using Server.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureWebhost();
builder.RegisterServices();

var app = builder.Build();
app.RegisterMiddleware();
app.ConfigureWebApp();
app.RegisterEndpoints();
app.Run();