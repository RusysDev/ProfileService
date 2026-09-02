using System.Text.Json;

namespace ProfileServer;



public static class Sessions {
	public static Dictionary<string, SessionUsers> Data { get; set; } = [];

	public static SessionUsers Get() {
		var date = DateTime.Now.ToString("yyyy-MM-dd");
		if (!Data.TryGetValue(date, out var ret)) {
			ret = SessionUsers.GetFromFile(date);
			foreach (var i in Config.Data.Client.Users) {
				if (!ret.TryGetValue(i.Key, out var j)) {
					ret[i.Key] = new(i.Value.GetDay());
				}
			}
			ret.Save();
		}
		return ret;
	}
}


public class SessionUsers : Dictionary<string, Daily> {
	public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
	public void Save() {
		var f = Path.Combine(AppContext.BaseDirectory, "log", $"{Date}.json");
		File.WriteAllText(f, JsonSerializer.Serialize(this, Config.JsonOpts));
	}

	public static SessionUsers GetFromFile(string date) {
		var f = Path.Combine(AppContext.BaseDirectory, "log", $"{date}.json");
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


public class Daily : Day {
	public bool? Locked { get; set; }
	public int Limit { get; set; }
	public int Remain { get; set; }
	public Daily() { }
	public Daily(Day day) { Time = day.Time; Remain = Limit = day.Time * 60; Start = day.Start; End = day.End; }

	public void SetLimit(Day day) {
		var sec = day.Time * 60; var incr = sec - Limit;
		Limit = sec; Remain += incr; Start = day.Start; End = day.End;

		var m = TimeOnly.FromDateTime(DateTime.Now);
		if (m >= Start && m <= End) {
			if (Remain < 1) {
				Locked = true;
				Limit += -Remain;
				Remain = 0;
			}
			else Locked = false;
		} else { Locked = true; }
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


public class ServerConfig {
	public string UserName { get; set; } = "test";
	public string UserPass { get; set; } = "test";
	public ClientConfig Client { get; set; } = new();
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






public class Tick {
	public string? User { get; set; }
	public int Time { get; set; }
}