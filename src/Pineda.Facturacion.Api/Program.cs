using Pineda.Facturacion.Application.DependencyInjection;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Infrastructure.DependencyInjection;
using Pineda.Facturacion.Infrastructure.BillingWrite.DependencyInjection;
using Pineda.Facturacion.Infrastructure.LegacyRead.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddLegacyReadInfrastructure(builder.Configuration);
builder.Services.AddBillingWriteInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapOrdersEndpoints();
app.MapSalesOrdersEndpoints();

app.Run();
