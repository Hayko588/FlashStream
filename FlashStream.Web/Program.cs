using FlashStream.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient("catalog", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:CatalogBaseUrl"]
        ?? throw new InvalidOperationException("Services:CatalogBaseUrl is required.")));
builder.Services.AddHttpClient("ordering", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderingBaseUrl"]
        ?? throw new InvalidOperationException("Services:OrderingBaseUrl is required.")));
builder.Services.AddHttpClient("inventory", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:InventoryBaseUrl"]
        ?? throw new InvalidOperationException("Services:InventoryBaseUrl is required.")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
