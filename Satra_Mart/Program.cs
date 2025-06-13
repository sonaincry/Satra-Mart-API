var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.WebHost.UseUrls("http://0.0.0.0:8080");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>

{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
//IF (app.Environment.IsDevelopment()) BLOCK
// app.UseSwagger() and app.UseSwaggerUI() should be directly here
app.UseSwagger(); // <--- REMOVE THE IF CONDITION AROUND THIS
app.UseSwaggerUI(); // <--- REMOVE THE IF CONDITION AROUND THIS


app.UseCors("AllowAll"); // This looks correct for enabling CORS across your application

app.UseAuthorization();

app.MapControllers();

app.Run();