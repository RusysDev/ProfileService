using System.IO.Pipes;
using Microsoft.Toolkit.Uwp.Notifications;

string username = Environment.UserName;
string pipeName = $"ToastPipe_{username.ToLower()}";

while (true) {
	using var server = new NamedPipeServerStream(pipeName, PipeDirection.In);
	await server.WaitForConnectionAsync();

	using var reader = new StreamReader(server);
	string payload = await reader.ReadToEndAsync();

	var parts = payload.Split(['|'], 3);
	if (parts.Length == 3) {
		var type = (ToastScenario)(int.TryParse(parts[2], out var tp) ? tp : 0);
		new ToastContentBuilder().AddText(parts[0]).SetToastScenario(type).AddAttributionText(parts[1]).Show();
	}
}
