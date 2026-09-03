
namespace ProfileService;

public class ProfileWorkerService : BackgroundService {

	private static ClientConfig Cfg => Config.Data;


	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		Directory.CreateDirectory(".\\log\\");

		while (!stoppingToken.IsCancellationRequested) {
			try {
				ProcessActiveTime();
			}
			catch {
				// Suppress background errors
			}

			await Task.Delay(TimeSpan.FromSeconds(Cfg.WorkerWait), stoppingToken);
		}
	}

	private static DateTime Diff { get; set; } = DateTime.Now;
	private static DateTime ReloadConfig { get; set; }
	private static void ProcessActiveTime() {
		var now = DateTime.Now;
		var save = false;
		var sess = Sessions.Get();
		if (now > ReloadConfig) {
			ReloadConfig = now.AddSeconds(Cfg.ConfigReload);
			Config.Reload(); Config.Save();
		}

		var onl = SessionManager.GetActive();

		if (!string.IsNullOrEmpty(onl?.Name)) {
			if (!Cfg.Users.ContainsKey(onl.Name) && !Cfg.IgnoreUsers.Contains(onl.Name)) {
				Thread.Sleep(Cfg.LogoutDelay * 1000); onl.Logoff();
			}
		}

		foreach (var i in Cfg.Users) {
			var login = i.Key;
			if (!sess.TryGetValue(login, out var usr)) sess[i.Key] = usr = new(i.Value.GetDay());

			var tme = TimeOnly.FromDateTime(DateTime.Now);

			if (usr.Remain > Cfg.LockDelay && tme >= usr.Start && tme <= usr.End) {
				if (usr.Locked ?? true) {
					usr.Locked = false; save = true; //Uptime - Unlock
				}
			}
			else if (!usr.Locked ?? true) {
				usr.Locked = true; save = true;  //Downtime - Unlock
			}

			SessionManager.DisableUser(login, usr.Locked ?? false);

			if (onl?.Id > 0) {
				var diff = (int)(now - Diff).TotalSeconds;
				usr.Remain -= diff;
				sess = new Tick() { Time = diff, User = login }.Send();
				save = true;
				if (!sess.TryGetValue(login, out var tmp) || (tmp.Locked ?? true)) {
					onl.Msg("Time limit.", $"Daily time limit has been reached.\nYou will be logged out in {Cfg.LockDelay} seconds.");
					Thread.Sleep(Cfg.LockDelay * 1000);
					sess = Sessions.Get();
					if (!sess.TryGetValue(login, out tmp) || (tmp.Locked ?? true)) {
						onl.Disable();
						onl.Lock();
					}
				}
			}
		}
		Diff = now;
		if (save) sess.Save();
	}


}

