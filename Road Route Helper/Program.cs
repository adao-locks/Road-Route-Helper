var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// DI registrations
builder.Services.AddSingleton<Road_Route_Helper.Application.Interfaces.ITripRepository>(sp =>
{
    var relativePath = sp.GetRequiredService<IConfiguration>().GetRequiredSection("Storage:TripsFilePath").Value!;
    var contentRoot = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
    return new Road_Route_Helper.Infrastructure.JsonTripRepository(Path.Combine(contentRoot, relativePath));
});

var app = builder.Build();

// Load the trips now: an unusable file must stop the app at startup, not on the first request.
app.Services.GetRequiredService<Road_Route_Helper.Application.Interfaces.ITripRepository>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
