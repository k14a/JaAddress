using JaAddress.Api.Endpoints;
using JaAddress.Api.Options;
using JaAddress.Core;
using JaAddress.Core.Options;

var builder = WebApplication.CreateBuilder(args);

// オプション
builder.Services.Configure<ApiOptions>(
    builder.Configuration.GetSection("Api"));

// JaAddress.Core
var dataDirectory = builder.Configuration["JaAddress:DataDirectory"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data");

builder.Services.AddJaAddress(new JaAddressOptions {
    DataDirectory = dataDirectory,
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "JaAddress API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// エンドポイント登録
PrefectureEndpoints.Map(app);
ParseEndpoints.Map(app);

app.Run();

public partial class Program { }
