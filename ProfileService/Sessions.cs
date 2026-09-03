using System.Diagnostics;
using System.Runtime.InteropServices;
using static ProfileService.SessionManager;
using static ProfileService.SessionUser;


namespace ProfileService;

public class SessionUser {
	public int Id { get; set; }
	public string? Name { get; set; }
	public bool Locked { get; set; }
	public string? Station { get; set; }
	public UserState State { get; set; }

	public void Logoff() { Process.Start(new ProcessStartInfo("logoff", Id.ToString()) { CreateNoWindow = true }); }
	public void Lock() { Process.Start(new ProcessStartInfo("tsdiscon", Id.ToString()) { CreateNoWindow = true }); }

	public void Msg(string title, string text) => SendMessageToUser(Id, title, text);

	public void Disable(bool @lock = true) => DisableUser(Name ?? "null", @lock);
	public enum UserState { Other, Active, Disconnected, Idle }

}



public static class SessionManager {
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);
	[DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr pMemory);
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSQuerySessionInformation(IntPtr hServer, int SessionId, int WTSInfoClass, out IntPtr ppBuffer, out int pBytesReturned);
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSSendMessage(IntPtr hServer, int SessionId, string pTitle, uint TitleLength, string pMessage, uint MessageLength, uint Style, uint Timeout, out uint pResponse, bool bWait);
	[DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSLogOffSession(IntPtr hServer, int SessionId, bool bWait);

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


	public static bool SendMessageToUser(int sessionId, string title, string message) {
		return WTSSendMessage(
			WTS_CURRENT_SERVER_HANDLE,
			sessionId,
			title, (uint)((title.Length + 1) * 2),
			message, (uint)((message.Length + 1) * 2),
			0, 0, out _, false);
	}


	private static Dictionary<string, bool> DisabledUsers { get; set; } = [];
	public static void DisableUser(string username, bool @lock = true) {
		if (!string.IsNullOrEmpty(username) && (!DisabledUsers.TryGetValue(username, out var lk) || lk != @lock)) {
			DisabledUsers[username] = @lock;
			var disablePsi = new ProcessStartInfo("net", $"user {username} /active:{(@lock ? "no" : "yes")}") {
				CreateNoWindow = true, UseShellExecute = false, Verb = "runas"
			};
			Process.Start(disablePsi);
		}
	}

	private static UserState MapState(WTS_CONNECT_STATE_CLASS state) => state switch {
		WTS_CONNECT_STATE_CLASS.WTSActive => UserState.Active,
		WTS_CONNECT_STATE_CLASS.WTSDisconnected => UserState.Disconnected,
		WTS_CONNECT_STATE_CLASS.WTSIdle => UserState.Idle,
		_ => UserState.Other
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
				ret.Add(new SessionUser() { Id = si.SessionId, Name = username, State = MapState(si.State), Station = si.pWinStationName, Locked = locked });
			}
		}
		WTSFreeMemory(ppSessionInfo);
		return ret;
	}


	private static bool IsSessionLocked(int sessionId) {
		if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, WTSSessionInfoEx, out IntPtr buffer, out int bytes)) {
			try {
				if (bytes >= 32) {
					uint sessionFlags = (uint)Marshal.ReadInt32(buffer, 8);
					return sessionFlags == WTS_SESSIONSTATE_LOCK;
				}
			}
			finally { WTSFreeMemory(buffer); }
		}
		return false;
	}
}