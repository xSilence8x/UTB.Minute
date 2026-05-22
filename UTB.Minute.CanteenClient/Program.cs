using UTB.Minute.CanteenClient.Components;
using UTB.Minute.CanteenClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddScoped<RoleState>();
builder.Services.AddScoped<KeycloakTokenProvider>();
builder.Services.AddHttpClient<CanteenApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["services:webapi:https:0"]
        ?? builder.Configuration["services:webapi:http:0"]
        ?? builder.Configuration["WebApi:BaseAddress"]
        ?? "https://localhost:7133");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
