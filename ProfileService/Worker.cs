
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;

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
				save = true;

				if (sess.TryGetValue(login, out usr)) {
					if (!KillRunning) {
						if ((usr.Locked ?? false) || usr.Remain < Cfg.LockDelay) {
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


	private static CancellationTokenSource? _killToken;
	public static bool KillRunning => _killToken != null;
	public static void KillStart(SessionUser user) {
		_killToken?.Cancel();		_killToken = new CancellationTokenSource();
		_ = Task.Run(async () => {
			try {
				var dly = Cfg.LockDelay > 5 ? Cfg.LockDelay : 5;
				for (int i = dly; i > 10; i -= 10) {
					user.Msg("Laikas baigėsi", $"Sistema atsijungs po {i} sekundžių.", ToastIcon.TimeRem, true);
					await Task.Delay(TimeSpan.FromSeconds(10), _killToken.Token);
					if (KillExtended(user.Name ?? "")) { KillStop(); return; }
				}
				var sess = Sessions.Get();
				if (!sess.TryGetValue(user.Name ?? "", out var tmp) || (tmp.Locked ?? true)) {
					user.Disable(); await Task.Delay(TimeSpan.FromSeconds(5), _killToken.Token); user.Lock();
				}
			}
			catch (OperationCanceledException) { }
			_killToken = null;
		}, _killToken.Token);
	}
	public static void KillStop() { _killToken?.Cancel(); _killToken = null; }
	private static bool KillExtended(string user) {
		var sess = Sessions.Get();
		return sess.TryGetValue(user, out var tmp) && tmp.Remain > Cfg.LockDelay;
	}


	//private static CancellationTokenSource? _lockToken;
	//public static bool LockedRunning => _lockToken != null;
	//public static void LockStart(string user) {
	//	SessionManager.DisableUser(user, false);
	//	_lockToken?.Cancel(); _lockToken = new CancellationTokenSource();
	//	_ = Task.Run(async () => {
	//		try {
	//			await Task.Delay(TimeSpan.FromSeconds(Cfg.LockDelay));
	//			SessionManager.DisableUser(user, false);


	//			var dly = Cfg.LockUnlock > 5 ? Cfg.LockUnlock : 5;

	//			var sess = Sessions.Get();
	//			if (!sess.TryGetValue(user.Name ?? "", out var tmp) || (tmp.Locked ?? true)) {
	//				user.Disable(); await Task.Delay(TimeSpan.FromSeconds(5), _killToken.Token); user.Lock();
	//			}
	//		}
	//		catch (OperationCanceledException) { }
	//		_killToken = null;
	//	}, _killToken.Token);
	//}


	//public static void LockStop() { _lockToken?.Cancel(); _lockToken = null;
	//	SessionManager.DisableUser(user, false);
	//}


	//TODO: Worker to lock and auto unlock user after 30 seconds
	//TODO: If user is locked, lock + lock + logoff



}




