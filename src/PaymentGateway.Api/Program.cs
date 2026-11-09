using System.Text.Json.Serialization;

using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddHealthChecks();

var bankBaseUrl = builder.Configuration["Bank:BaseUrl"]
    ?? throw new InvalidOperationException("Bank:BaseUrl is not configured.");

builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri(bankBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
