using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProfileService;

namespace ProfileService;

public class SessionUser {
	public int Id { get; set; }
	public string? Name { get; set; }
	public bool Locked { get; set; }
	public string? Station { get; set; }
	public UserState State { get; set; }

	public void Logoff() { Process.Start(new ProcessStartInfo("logoff", Id.ToString()) { CreateNoWindow = true }); }
	public void Lock() { Process.Start(new ProcessStartInfo("tsdiscon", Id.ToString()) { CreateNoWindow = true }); }

	public void Msg(string title, string text, ToastIcon? icon) => new ToastMessage() { Title = title, Message = text, Icon = icon }.Send(Name ?? "");

	public void Disable(bool @lock = true) => SessionManager.DisableUser(Name ?? "null", @lock);
	public enum UserState { Other, Active, Disconnected, Idle }

}



public static class SessionManager {
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);
	[DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr pMemory);
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSQuerySessionInformation(IntPtr hServer, int SessionId, int WTSInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

	private const int WTS_CURRENT_SERVER_HANDLE = 0;
	private const int WTSUserName = 5;
	private const int WTSSessionInfoEx = 25;

	private const uint WTS_SESSIONSTATE_LOCK = 0x0;

	public enum WTS_CONNECT_STATE_CLASS {
		WTSActive, WTSConnected, WTSConnectQuery, WTSShadow,
		WTSDisconnected, WTSIdle, WTSListen, WTSReset, WTSDown, WTSInit
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct WTS_SESSION_INFO {
		public int SessionId;
		[MarshalAs(UnmanagedType.LPStr)]
		public string pWinStationName;
		public WTS_CONNECT_STATE_CLASS State;
	}



	private static Dictionary<string, bool> DisabledUsers { get; set; } = [];
	public static void DisableUser(string username, bool @lock = true, bool silent = false) {
		var du = DisabledUsers.TryGetValue(username, out var lk);
		if (!string.IsNullOrEmpty(username) && (!du || lk != @lock)) {
			if (du && !silent) {
				new ToastMessage() { Icon = @lock ? ToastIcon.Lock : ToastIcon.Unlock, Title = $"Vartotojas {(@lock ? "užrakintas" : "atrakintas")}", Priority = true }.Send(username);
			}

			DisabledUsers[username] = @lock;
			var disablePsi = new ProcessStartInfo("net", $"user {username} /active:{(@lock ? "no" : "yes")}") {
				CreateNoWindow = true, UseShellExecute = false, Verb = "runas"
			};
			Process.Start(disablePsi);
		}
	}

	private static SessionUser.UserState MapState(WTS_CONNECT_STATE_CLASS state) => state switch {
		WTS_CONNECT_STATE_CLASS.WTSActive => SessionUser.UserState.Active,
		WTS_CONNECT_STATE_CLASS.WTSDisconnected => SessionUser.UserState.Disconnected,
		WTS_CONNECT_STATE_CLASS.WTSIdle => SessionUser.UserState.Idle,
		_ => SessionUser.UserState.Other
	};

	public static SessionUser? GetActive() => GetAllSessions(true).FirstOrDefault();

	public static List<SessionUser> GetAllSessions(bool unlocked = false) {
		var ret = new List<SessionUser>();
		IntPtr ppSessionInfo = IntPtr.Zero;
		int count = 0;
		if (!WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref count)) { return ret; }
		int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
		IntPtr current = ppSessionInfo;
		for (int i = 0; i < count; i++) {
			var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(current);
			current = IntPtr.Add(current, dataSize);
			var locked = IsSessionLocked(si.SessionId);
			if (unlocked && (si.State != WTS_CONNECT_STATE_CLASS.WTSActive || locked)) { continue; } //Ignore inactive;
			if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, si.SessionId, WTSUserName, out IntPtr buffer, out int bytes) && bytes > 1) {
				string username = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
				ret.Add(new SessionUser() { Id = si.SessionId, Name = username.ToLower(), State = MapState(si.State), Station = si.pWinStationName, Locked = locked });
			}
		}
		WTSFreeMemory(ppSessionInfo);
		return ret;
	}

	[StructLayout(LayoutKind.Sequential)] private struct WTSINFOEX { public uint Level; public uint Reserved; public WTSINFOEX_LEVEL Data; }
	[StructLayout(LayoutKind.Sequential)] private struct WTSINFOEX_LEVEL { public WTSINFOEX_LEVEL1 WTSInfoExLevel1; }
	[StructLayout(LayoutKind.Sequential)] private struct WTSINFOEX_LEVEL1 { public uint SessionId; public uint SessionState; public int SessionFlags; }

	private static bool IsSessionLocked(int sessionId) {
		if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, WTSSessionInfoEx, out IntPtr buffer, out int bytes)) {
			try {
				if (bytes >= Marshal.SizeOf<WTSINFOEX>()) {
					var info = Marshal.PtrToStructure<WTSINFOEX>(buffer);
					if (info.Level == 1) {
						return info.Data.WTSInfoExLevel1.SessionFlags == WTS_SESSIONSTATE_LOCK;
					}
				}
			}
			finally { WTSFreeMemory(buffer); }
		}
		return false;
	}







	public static void Send(this ToastMessage msg, string username) {
		try {
			using var client = new NamedPipeClientStream(".", $"local\\ToastPipe_{username}", PipeDirection.Out);
			client.Connect(5000);
			JsonSerializer.Serialize(client, msg);
		}
		catch { }
	}
}
