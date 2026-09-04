using ProfileServer;
using ProfileService;


Directory.SetCurrentDirectory(AppContext.BaseDirectory);


var cfg = Config.Data;


var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Default", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(serverOptions => {
	serverOptions.ListenAnyIP(cfg.Port);
});

var app = builder.Build();



app.MapGet("/api/config", () => Config.Data.Client).RequireApiKey();
app.MapGet("/api/session", () => Sessions.Get()).RequireApiKey();
app.MapPost("/api/tick", (Tick tick) => Sessions.Tick(tick)).RequireApiKey();



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
	if (ret.TryGetValue(user, out var dt)) { dt.SetLimit(limits); ret.Save(); }
	return ret;
}).RequireBasicAuth();

app.MapPost("/admin/message/{user}", (string user, ToastMessage msg) => {
	var ret = Sessions.Get();
	if (ret.TryGetValue(user, out var dt)) { dt.Message = msg; ret.Save(); }
	return ret;
}).RequireBasicAuth();

app.MapGet("/admin", () => Extensions.GetFile("admin.html")).RequireBasicAuth();
app.MapGet("/admin/limits", () => Extensions.GetFile("limits.html")).RequireBasicAuth();
app.MapGet("/admin/message", () => Extensions.GetFile("message.html")).RequireBasicAuth();
app.MapGet("/admin/style.css", () => Extensions.GetFile("style.css"));

app.Run();


