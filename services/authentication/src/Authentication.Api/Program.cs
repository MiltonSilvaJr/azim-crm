// Authentication.Api — ponto de entrada do módulo BC-12.
// Middlewares e endpoints serão registrados nas ondas seguintes (TASK-15..TASK-20).
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
