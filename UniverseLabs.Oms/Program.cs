using Dapper;
using FluentValidation;
using UniverseLabs.Oms.BLL.Services;
using UniverseLabs.Oms.DAL;
using UniverseLabs.Oms.DAL.Interfaces;
using UniverseLabs.Oms.DAL.Repositories;
using UniverseLabs.Oms.Validators;


var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;
builder.Services.Configure<DbSettings>(builder.Configuration.GetSection(nameof(DbSettings)));
builder.Services.AddValidatorsFromAssemblyContaining(typeof(Program));
builder.Services.AddScoped<ValidatorFactory>();
builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

UniverseLabs.Oms.Migrations.Program.Main([]);
app.Run();