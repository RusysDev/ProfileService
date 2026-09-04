using ProfileService;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;


string username = Environment.UserName;
string pipeName = $"local\\ToastPipe_{username.ToLower()}";

Console.WriteLine(pipeName);

var assembly = Assembly.GetExecutingAssembly();
string title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title?? "Laiko Limitas";
string aumid = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product?? "RusysDev.LaikoLimitas.0.3";

var toast = new Toast.ToastService(title, aumid);

while (true) {
	using var server = new NamedPipeServerStream(pipeName, PipeDirection.In);
	await server.WaitForConnectionAsync();
	try {
		var msg = await JsonSerializer.DeserializeAsync<ToastMessage>(server);
		if (msg is not null) toast.Show(msg);
	}
	catch { }
}
