
using System.Text.Json;


namespace ProfileService;






public class Test {
	//Get config
	//Get sessions


	//Tick - add session info

	//Messages
}






public class Tick {
	public string? User { get; set; }
	public int Time { get; set; }

}


public class Daily : Day {
	public bool? Locked { get; set; }
	public int Limit { get; set; }
	public int Remain { get; set; }
	public int? Incr { get; set; }
	public ToastMessage? Message { get; set; }
	public Daily() { }
	public Daily(Day day) {
		Time = day.Time; Remain = Limit = day.Time * 60; Start = day.Start; End = day.End;
		var m = TimeOnly.FromDateTime(DateTime.Now);
		Locked = m >= Start && m <= End && Remain > 0;
	}

	public void SetLimit(Day day) {
		var sec = day.Time * 60; Incr = sec - Limit;
		Limit = sec; Remain += Incr.Value; Start = day.Start; End = day.End;

		if (Incr > 0 && Remain > 0) { Message = new("Pridėta laiko", (int)(Incr / 60) + " minutės") { Icon = ToastIcon.TimeAdd }; }

		var m = TimeOnly.FromDateTime(DateTime.Now);
		if (m >= Start && m <= End) {
			if (Remain < 1) { Locked = true; Limit += -Remain; Remain = 0; }
			else Locked = false;
		}
		else { Locked = true; }
	}
}



public class SessionUsers : Dictionary<string, Daily> {
	public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");


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

public class ServerConfig {
	public string UserName { get; set; } = "test";
	public string UserPass { get; set; } = "test";
	public int Port { get; set; } = 5404;
	public ClientConfig Client { get; set; } = new();
}


public class ClientConfig {
	public string ServerHost { get; set; } = "http://localhost:5404";
	public string ServerSecret { get; set; } = "5ufPJehQpQCc";
	public List<string> IgnoreUsers { get; set; } = ["Administrator"];
	public int WorkerWait { get; set; } = 10;
	public int ConfigReload { get; set; } = 60;
	public int LockDelay { get; set; } = 30;
	public int LockUnlock { get; set; } = 60;
	public int LogoutDelay { get; set; } = 15;
	public Dictionary<string, UserConfig> Users { get; set; } = [];
}

public class UserConfig {
	public string? Title { get; set; }
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


