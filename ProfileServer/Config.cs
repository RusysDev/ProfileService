using ProfileService;
using System.Text.Json;

namespace ProfileServer;


public static class Sessions {
	public static Dictionary<string, SessionUsers> Data { get; set; } = [];

	public static SessionUsers Get() {
		var date = DateTime.Now.ToString("yyyy-MM-dd");
		if (!Data.TryGetValue(date, out var ret)) {
			ret = SessionUsers.GetFromFile(date);
			foreach (var i in Config.Data.Client.Users) {
				if (!ret.TryGetValue(i.Key, out _)) {
					ret[i.Key] = new(i.Value.GetDay());
				}
			}
			ret.Save();
		}
		return ret;
	}

	public static SessionUsers Tick(Tick tick) {
		var ret = Get();
		if (tick.User is not null && ret.TryGetValue(tick.User, out var dt)) {
			dt.Remain -= tick.Time; dt.Incr = null; dt.Message = null;
			if (dt.Remain <= 0) dt.Locked = true;
			ret.Save();
		}
		return ret;
	}


	public static void Save(this SessionUsers sess) {
		var f = Path.Combine(AppContext.BaseDirectory, "log", $"{sess.Date}.json");
		File.WriteAllText(f, JsonSerializer.Serialize(sess, Config.JsonOpts));
	}
}




public class Config {
	public static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
	private static readonly string _cfg = "config.json";
	public static HttpClient HClient { get; set; } = new HttpClient() { };
	public static ServerConfig Data { get; set; }

	static Config() {
		Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log"));
		ServerConfig? ret = null;
		var f = Path.Combine(AppContext.BaseDirectory, _cfg);
		if (File.Exists(f)) {
			try { ret = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(f)); }
			catch { throw; }
		}
		Data = ret ?? new();
		Save();
	}
	public static void Save() {
		File.WriteAllText(_cfg, JsonSerializer.Serialize(Data, JsonOpts));
	}
}
