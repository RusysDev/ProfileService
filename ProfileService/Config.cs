using System.Net.Http.Json;
using System.Text.Json;

namespace ProfileService;

public static class Sessions {
	public static SessionUsers Users { get; set; } = GetSessions();
	public static SessionUsers Get() => Users = GetSessions();

	private static SessionUsers GetSessions() {
		SessionUsers? ret = null;
		try { ret = Config.HClient.GetFromJsonAsync<SessionUsers>("/api/session").GetAwaiter().GetResult(); } catch { }
		ret ??= SessionUsers.GetFromFile(DateTime.Now.ToString("yyyy-MM-dd"));
		ret.Save(); return ret;
	}

	public static SessionUsers Send(this Tick tick) {
		using var rsp = Config.HClient.PostAsJsonAsync("/api/tick", tick).GetAwaiter().GetResult();
		return (rsp.IsSuccessStatusCode ? rsp.Content.ReadFromJsonAsync<SessionUsers>().GetAwaiter().GetResult() : Users) ?? Users;
	}

	public static void Save(this SessionUsers sess) {
		var f = Path.Combine(AppContext.BaseDirectory, "log", $"{sess.Date}.json");
		File.WriteAllText(f, JsonSerializer.Serialize(sess, Config.JsonOpts));
	}
}

public static class Config {
	public static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
	private static readonly string _cfg = "config.json";
	public static ClientConfig Data { get; set; } = new();

	public static HttpClient HClient { get; set; }


	public static HttpClient GetClient {
		get {
			var ret = new HttpClient() { BaseAddress = new(Data.ServerHost) };
			ret.DefaultRequestHeaders.Add("X-API-Key", Data.ServerSecret);
			return ret;
		}
	}

	static Config() {
		Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log"));

		ClientConfig? ret = null;
		if (File.Exists(_cfg)) {
			try { ret = JsonSerializer.Deserialize<ClientConfig>(File.ReadAllText(_cfg)); }
			catch { throw; }
		}
		Data = ret ?? new();
		HClient = GetClient;
		Reload();
		Save();
	}


	public static void Save() {
		File.WriteAllText(_cfg, JsonSerializer.Serialize(Data, JsonOpts));
	}

	public static void Reload() {
		try {
			var ret = HClient.GetFromJsonAsync<ClientConfig>("/api/config").GetAwaiter().GetResult();
			if (ret != null) {
				if (ret.ServerSecret == Data.ServerSecret && ret.ServerHost == Data.ServerHost) { HClient = GetClient; }
				Data = ret;
			}
		}
		catch (Exception ex) { Console.WriteLine($"Error getting config: {ex.Message}"); }
	}
}
