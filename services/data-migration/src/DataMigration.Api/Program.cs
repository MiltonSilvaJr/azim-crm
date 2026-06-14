// DataMigration.Api — entry point mínimo do módulo (Onda 1 Bootstrap).
// Controllers e DI completos serão adicionados nas Ondas 3 e 5.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
