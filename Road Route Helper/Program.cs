var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// lightweight in-memory trip repository will be registered from Infrastructure during startup
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// DI registrations
builder.Services.AddSingleton<Road_Route_Helper.Infrastructure.InMemoryTripRepository>();
builder.Services.AddSingleton<Road_Route_Helper.Application.Interfaces.ITripRepository>(sp => sp.GetRequiredService<Road_Route_Helper.Infrastructure.InMemoryTripRepository>());

var app = builder.Build();

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

app.MapRazorPages();

app.Run();
