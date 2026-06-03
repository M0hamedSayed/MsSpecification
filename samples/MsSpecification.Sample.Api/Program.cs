using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF;
using MsSpecification.Infra.EF.Extensions;
using MsSpecification.Sample.Api.Data;
using MsSpecification.Sample.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite("Data Source=sample.db"));

builder.Services.AddMsSpecification<SampleDbContext>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SeedData.InitializeAsync(db);
}

app.MapOpenApi();

app.MapProductEndpoints();
app.MapOrderEndpoints();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests in the sample test project.</summary>
public partial class Program;
