
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ProfileService;

public class ProfileWorkerService : BackgroundService {

	private static ClientConfig Cfg => Config.Data;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		Directory.CreateDirectory(".\\log\\");

		while (!stoppingToken.IsCancellationRequested) {
			try { ProcessActiveTime(); }
			catch { }
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
				onl.Msg("Nežinomas vartotojas", $"{onl.Name} sesija išjungiama.", ToastIcon.Info);
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


			if (login == onl?.Name && !onl.Locked) {
				var diff = (int)(now - Diff).TotalSeconds;
				usr.Remain -= diff;
				sess = new Tick() { Time = diff, User = login }.Send();

				usr.Message?.Send(login);
				//				if (usr.Incr > 0) onl.Msg("Pridėta laiko", $"{(int)(usr.Incr / 60)} minutės.", ToastIcon.TimeAdd);

				save = true;

				if (sess.TryGetValue(login, out usr) ) {
					if (!KillRunning) {
						if ((usr.Locked ?? true) || usr.Remain < Cfg.LockDelay) {
							KillStart(onl);
						}
					}
					else if ((!usr.Locked ?? false) && usr.Remain < Cfg.LockDelay) { KillStop(); }
				}
			}
			else SessionManager.DisableUser(login, usr.Locked ?? false);
		}
		Diff = now;
		if (save) sess.Save();
	}


	private static CancellationTokenSource? _cts;
	public static bool KillRunning => _cts != null;
	public static void KillStart(SessionUser user) {
		_cts?.Cancel();
		_cts = new CancellationTokenSource();
		_ = Task.Run(async () => {
			try {
				user.Msg("Laikas baigėsi", $"Sistema atsijungs po {Cfg.LockDelay} sekundžių.", ToastIcon.TimeRem);
				await Task.Delay(TimeSpan.FromSeconds(Cfg.LockDelay), _cts.Token);
				var sess = Sessions.Get();
				if (!sess.TryGetValue(user.Name ?? "", out var tmp) || (tmp.Locked ?? true)) {
					user.Disable(); Thread.Sleep(1000); user.Lock();
				}
			}
			catch (OperationCanceledException) { }
			_cts = null;
		}, _cts.Token);
	}

	public static void KillStop() { _cts?.Cancel(); }

}




