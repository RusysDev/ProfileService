using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;


namespace ProfileService;

public class ProfileWorkerService : BackgroundService {

	private static ClientConfig _cfg => Config.Data;


	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		Directory.CreateDirectory(".\\log\\");


		while (!stoppingToken.IsCancellationRequested) {
			try {
				ProcessActiveTime();
			}
			catch {
				// Suppress background errors
			}

			await Task.Delay(TimeSpan.FromSeconds(_cfg.WorkerWait), stoppingToken);
		}
	}

	private static DateTime Diff { get; set; } = DateTime.Now;
	private static DateTime ReloadConfig { get; set; }
	private void ProcessActiveTime() {
		var now = DateTime.Now;
		var dt = now.ToString("yyyy-MM-dd");
		var save = false;
		var sess = Sessions.Get(true);
		if (now > ReloadConfig) {
			ReloadConfig = now.AddSeconds(_cfg.ConfigReload);
			Config.Reload(); Config.Save();
		}


		var onl = SessionManager.GetActiveSessions().ToList();


		foreach(var i in onl) {
			if(!_cfg.Users.ContainsKey(i.Username) && !_cfg.IgnoreUsers.Contains(i.Username)) {
				SessionManager.Logoff(i.SessionId);
			}
		}

		foreach (var i in _cfg.Users) {
			var login = i.Key;
			if (!sess.TryGetValue(login, out var usr)) sess[i.Key] = usr = new(i.Value.GetDay());

			var tme = TimeOnly.FromDateTime(DateTime.Now);
			var (SessionId, Username) = onl.Find(x => x.Username == login);

			if (usr.Remain > _cfg.LogoutDelay && tme >= usr.Start && tme <= usr.End) {
				if (usr.Locked ?? true) {
					Console.WriteLine("UPTIME - UNLOCK");
					usr.Locked = false; save = true;
				}
			}
			else if (!usr.Locked ?? true) {
				if (SessionId > 0) {
					Console.WriteLine("DOWNTIME - LOCK");
					usr.Locked = true; save = true;
				}
			}

			UserSession.Lock(login, usr.Locked ?? false);

			if (SessionId > 0) {
				var diff = (int)(now - Diff).TotalSeconds;
				usr.Remain -= diff;
				sess = new Tick() { Time = diff, User = login }.Send();
				save = true;
				if (!sess.TryGetValue(login, out var tmp) || (tmp.Locked ?? true)) {
					SessionManager.SendMessageToUser(SessionId, "Your daily time limit has been reached.", $"You will be logged out in {_cfg.LogoutDelay} seconds.");
					Thread.Sleep(_cfg.LogoutDelay * 1000);
					sess = Sessions.Get(true);
					if (!sess.TryGetValue(login, out tmp) || (tmp.Locked ?? true)) {
						UserSession.Lock(login);
						SessionManager.LockUser(SessionId);
					}
				}
			}
		}
		Diff = now;
		if (save)  sess.Save(); 
	}









}

