

using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProfileService;

public static class Sessions {
	public static SessionUsers Users { get; set; } = GetSessions();

	public static SessionUsers Get(bool force=false) {
		var now = DateTime.Now.ToString("yyyy-MM-dd");
		if (force || now != Users.Date) {
			Users = GetSessions();
		}
		return Users;
	}

	private static SessionUsers GetSessions() {
		SessionUsers? ret = null;
		try { ret = Config.HClient.GetFromJsonAsync<SessionUsers>("/api/session").GetAwaiter().GetResult(); } catch { }
		ret ??= SessionUsers.GetFromFile(DateTime.Now.ToString("yyyy-MM-dd"));
		ret.Save(); return ret;
	}
}

public class Tick {
	public string? User { get; set; }
	public int Time { get; set; }

	public SessionUsers Send() {
		using var rsp = Config.HClient.PostAsJsonAsync("/api/tick", this).GetAwaiter().GetResult();
		return (rsp.IsSuccessStatusCode ? rsp.Content.ReadFromJsonAsync<SessionUsers>().GetAwaiter().GetResult() : Sessions.Users) ?? Sessions.Users;
	}
}


public class Daily : Day {
	public bool? Locked { get; set; }
	public int Limit { get; set; }
	public int Remain { get; set; }
	public Daily() { }
	public Daily(Day day) { Time = day.Time; Remain = Limit = day.Time * 60; Start = day.Start; End = day.End; }

	public void SetLimit(Day day) {
		var sec = day.Time * 60; var incr = sec - Limit;
		Limit = sec; Remain += incr; Start = day.Start; End = day.End;
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
		Directory.CreateDirectory(".\\log\\");

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
		catch { }
	}
}


public class SessionUsers : Dictionary<string, Daily> {
	public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
	public void Save() {
		var f = $".\\log\\{Date}.json";
		File.WriteAllText(f, JsonSerializer.Serialize(this, Config.JsonOpts));
	}

	public static SessionUsers GetFromFile(string date) {
		var f = $".\\log\\{date}.json";
		SessionUsers? ret = null;
		if (File.Exists(f)) {
			try { ret = JsonSerializer.Deserialize<SessionUsers>(File.ReadAllText(f)); }
			catch { }
		}
		ret ??= [];
		ret.Date = date;
		return ret;
	}
}


public class ClientConfig {
	public string ServerHost { get; set; } = "http://localhost:5001";
	public string ServerSecret { get; set; } = "5ufPJehQpQCc";
	public List<string> IgnoreUsers { get; set; } = ["Administrator"];
	public int WorkerWait { get; set; } = 10;
	public int ConfigReload { get; set; } = 60;
	public int LogoutDelay { get; set; } = 15;
	public Dictionary<string, UserConfig> Users { get; set; } = [];
}

public class UserConfig {
	public Day Mon { get; set; } = new();
	public Day Tue { get; set; } = new();
	public Day Wed { get; set; } = new();
	public Day Thu { get; set; } = new();
	public Day Fri { get; set; } = new();
	public Day Sat { get; set; } = new();
	public Day Sun { get; set; } = new();

	public Day GetDay() => DateTime.Now.DayOfWeek switch {
		DayOfWeek.Monday => Mon,
		DayOfWeek.Tuesday => Tue,
		DayOfWeek.Wednesday => Wed,
		DayOfWeek.Thursday => Thu,
		DayOfWeek.Friday => Fri,
		DayOfWeek.Saturday => Sat,
		DayOfWeek.Sunday => Sun,
		_ => new(),
	};
}

public class Day {
	public TimeOnly Start { get; set; } = new(8, 0);
	public TimeOnly End { get; set; } = new(21, 0);
	public int Time { get; set; } = 60;
	public Day() { }
	public Day(string start, string end, int time) { Start = TimeOnly.Parse(start); End = TimeOnly.Parse(end); Time = time; }
}


