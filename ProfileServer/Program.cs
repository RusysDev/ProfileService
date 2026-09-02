using Microsoft.AspNetCore.Mvc;
using ProfileServer;


Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.WebHost.ConfigureKestrel(serverOptions => {
	serverOptions.ListenAnyIP(5404); //todo: prom konpig
});

var app = builder.Build();



app.MapGet("/api/config", () => Config.Data.Client).RequireApiKey();
app.MapGet("/api/session", () => Sessions.Get()).RequireApiKey();

app.MapPost("/api/tick", ([FromBody] Tick tick) => {
	var ret = Sessions.Get();
	if (tick.User is not null && ret.TryGetValue(tick.User, out var dt)) {
		dt.Remain -= tick.Time;
		if (dt.Remain <= 0) dt.Locked = true;
		ret.Save();
	}
	return ret;
}).RequireApiKey();



app.MapGet("/admin/session", () => Sessions.Get()).RequireBasicAuth();
app.MapGet("/admin/config", () => Config.Data.Client.Users).RequireBasicAuth();
app.MapPost("/admin/config", (Dictionary<string, UserConfig> users) => {
	foreach (var i in users) {
		Config.Data.Client.Users[i.Key] = i.Value;
		Config.Save();
	}
}).RequireBasicAuth();

app.MapPost("/admin/limits/{user}", (string user, Day limits) => {
	var ret = Sessions.Get();
	if (ret.TryGetValue(user, out var dt)) {
		dt.SetLimit(limits);
		ret.Save();
	}
	return ret;
}).RequireBasicAuth();



app.MapGet("/admin", async () => {
	var filePath = Path.Combine(AppContext.BaseDirectory, "html", "admin.html");
	if (!File.Exists(filePath)) return Results.NotFound();
	var html = await File.ReadAllTextAsync(filePath);
	return Results.Content(html, "text/html");
}).RequireBasicAuth();



app.MapGet("/admin/limits", async () => {
	var filePath = Path.Combine(AppContext.BaseDirectory, "html", "limits.html");
	if (!File.Exists(filePath)) return Results.NotFound();
	var html = await File.ReadAllTextAsync(filePath);
	return Results.Content(html, "text/html");
}).RequireBasicAuth();


app.MapGet("/admin/style.css", async () => {
	var filePath = Path.Combine(AppContext.BaseDirectory, "html","style.css");
	if (!File.Exists(filePath)) return Results.NotFound();
	var html = await File.ReadAllTextAsync(filePath);
	return Results.Content(html, "text/css");
});

app.Run();


