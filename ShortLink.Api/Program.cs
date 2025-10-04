using ShortLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton<IUrlService, InMemoryUrlService>();

var app = builder.Build();

app.Run();
