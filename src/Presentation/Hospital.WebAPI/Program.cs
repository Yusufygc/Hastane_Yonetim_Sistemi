using Hospital.Application.Extensions;
using Hospital.Infrastructure.Extensions;
using Hospital.Persistence.Extensions;
using Hospital.WebAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Her katman kendi Extensions/ klasöründeki Add... metoduyla kaydolur (bkz. wiki: di-kurallari).
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddPersistence();
builder.Services.AddWebApi(builder.Configuration);

var app = builder.Build();

app.ValidateRequiredSettings();

await app.MigrateDatabaseIfConfiguredAsync();
await app.SeedDatabaseIfConfiguredAsync();

app.UseWebApi();

app.Run();

// WebApplicationFactory<Program> için.
public partial class Program;
