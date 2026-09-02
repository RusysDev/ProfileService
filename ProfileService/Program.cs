/*
 
sc.exe create WinProfService binPath= "C:\ProgramData\Microsoft\ProfileService\ProfileService.exe" start=auto displayname="Windows Profile Service"
sc.exe description WinProfService "Monitors active user session profile quality service."
sc.exe start WinProfService


 */
using Microsoft.Extensions.Hosting;

using ProfileService;



Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(); // Required for Windows Service control signals
builder.Services.AddHostedService<ProfileWorkerService>();

var host = builder.Build();
host.Run();