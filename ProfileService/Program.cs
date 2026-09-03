
using ProfileService;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var cfg = Config.Data;
var usr = SessionManager.GetActive();

if (!string.IsNullOrEmpty(usr?.Name)) {
	if (!cfg.Users.ContainsKey(usr.Name) && !cfg.IgnoreUsers.Contains(usr.Name)) { 
		Console.WriteLine($"Warning: Active user is not in config ({usr.Name})");
		Thread.Sleep(cfg.LogoutDelay*1000);
	}
} else {
	Console.WriteLine("No logged in users found.");
}


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(); // Required for Windows Service control signals
builder.Services.AddHostedService<ProfileWorkerService>();

var host = builder.Build();
host.Run();