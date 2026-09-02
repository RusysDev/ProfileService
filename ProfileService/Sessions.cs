using System.Diagnostics;
using System;
using System.Runtime.InteropServices;


namespace ProfileService;

public class UserSession {
	public string Username { get; set; } = "";
	public string SessionName { get; set; } = "";
	public int Id { get; set; }
	public string State { get; set; } = "";
	public string IdleTime { get; set; } = "";
	public DateTime LogonTime { get; set; }

	public void Warn() {
		var psi = new ProcessStartInfo("msg", $"{Id} /time:{Config.Data.LogoutDelay} Your daily time limit has been reached. You will be logged out in {Config.Data.LogoutDelay} seconds.") {
			CreateNoWindow = true, UseShellExecute = false
		};
		Process.Start(psi);
	}
	public void Logoff() {
		Process.Start(new ProcessStartInfo("logoff", Id.ToString()) { CreateNoWindow = true });
	}


	private static Dictionary<string, bool> Locks { get; set; } = [];
	public static void Lock(string user, bool @lock = true) {
		if (!Locks.TryGetValue(user, out var lk) || lk != @lock) {
			Locks[user] = @lock;
			var disablePsi = new ProcessStartInfo("net", $"user {user} /active:{(@lock ? "no" : "yes")}") {
				CreateNoWindow = true, UseShellExecute = false,
				Verb = "runas"
			};
			Process.Start(disablePsi);
		}
	}

	public static List<UserSession> GetUserSessions() {
		var sessions = new List<UserSession>();
		var psi = new ProcessStartInfo("query", "user") {
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var process = Process.Start(psi);
		string output = process?.StandardOutput.ReadToEnd() ?? "";

		var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		if (lines.Length < 2) return sessions;

		string header = lines[0];

		// Find column start indexes from header
		int unIdx = header.IndexOf("USERNAME");
		int sessIdx = header.IndexOf("SESSIONNAME");
		int idIdx = header.IndexOf("ID");
		int stateIdx = header.IndexOf("STATE");
		int idleIdx = header.IndexOf("IDLE TIME");
		int logonIdx = header.IndexOf("LOGON TIME");

		foreach (var line in lines.Skip(1)) {
			if (string.IsNullOrWhiteSpace(line)) continue;

			string username = ExtractCol(line, unIdx, sessIdx).TrimStart('>').Trim();
			string sessionName = ExtractCol(line, sessIdx, idIdx);
			string idStr = ExtractCol(line, idIdx, stateIdx);
			string state = ExtractCol(line, stateIdx, idleIdx);
			string idleTime = ExtractCol(line, idleIdx, logonIdx);

			// Logion time goes to the end of the line
			string logonTimeStr = line.Length > logonIdx ? line.Substring(logonIdx).Trim() : "";

			if (int.TryParse(idStr, out int id) && DateTime.TryParse(logonTimeStr, out DateTime logonTime)) {
				sessions.Add(new UserSession {
					Username = username,
					SessionName = sessionName,
					Id = id,
					State = state,
					IdleTime = idleTime,
					LogonTime = logonTime
				});
			}
		}
		return sessions;
	}

	private static string ExtractCol(string line, int start, int nextStart) {
		if (start >= line.Length) return "";
		int length = nextStart - start;
		if (start + length > line.Length) length = line.Length - start;
		return line.Substring(start, length).Trim();
	}


}

public static class SessionManager {
	[DllImport("wtsapi32.dll", SetLastError = true)]
	private static extern bool WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);

	[DllImport("wtsapi32.dll")]
	private static extern void WTSFreeMemory(IntPtr pMemory);

	[DllImport("wtsapi32.dll", SetLastError = true)]
	private static extern bool WTSQuerySessionInformation(IntPtr hServer, int SessionId, int WTSInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

	[DllImport("wtsapi32.dll", SetLastError = true)]
	private static extern bool WTSSendMessage(IntPtr hServer, int SessionId, string pTitle, uint TitleLength, string pMessage, uint MessageLength, uint Style, uint Timeout, out uint pResponse, bool bWait);

	[DllImport("wtsapi32.dll", SetLastError = true)]
	private static extern bool WTSLogOffSession(IntPtr hServer, int SessionId, bool bWait);

	private const int WTS_CURRENT_SERVER_HANDLE = 0;
	private const int WTSUserName = 5;
	private const int WTSSessionInfoEx = 25;

	private const uint WTS_SESSIONSTATE_LOCK = 0x0;

	private enum WTS_CONNECT_STATE_CLASS {
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
			(IntPtr)WTS_CURRENT_SERVER_HANDLE,
			sessionId,
			title, (uint)((title.Length + 1) * 2),
			message, (uint)((message.Length + 1) * 2),
			0, 0, out _, false);
	}


	public static void Logoff(int sessionId) {
		Process.Start(new ProcessStartInfo("logoff", sessionId.ToString()) { CreateNoWindow = true });
	}
	public static void LockUser(int sessionId) {
		Process.Start(new ProcessStartInfo("tsdiscon", sessionId.ToString()) { CreateNoWindow = true });
	}

	public static IEnumerable<(int SessionId, string Username)> GetActiveSessions() {
		IntPtr ppSessionInfo = IntPtr.Zero;
		int count = 0;

		if (!WTSEnumerateSessions((IntPtr)WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref count))
			yield break;

		int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
		IntPtr current = ppSessionInfo;

		for (int i = 0; i < count; i++) {
			var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(current);
			current = IntPtr.Add(current, dataSize);

			if (si.State == WTS_CONNECT_STATE_CLASS.WTSActive && !IsSessionLocked(si.SessionId)) {
				if (WTSQuerySessionInformation((IntPtr)WTS_CURRENT_SERVER_HANDLE, si.SessionId, WTSUserName, out IntPtr buffer, out int bytes) && bytes > 1) {
					string username = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
					WTSFreeMemory(buffer);

					if (!string.IsNullOrEmpty(username)) {
						yield return (si.SessionId, username);
					}
				}
			}
		}

		WTSFreeMemory(ppSessionInfo);
	}

	private static bool IsSessionLocked(int sessionId) {
		if (WTSQuerySessionInformation((IntPtr)WTS_CURRENT_SERVER_HANDLE, sessionId, WTSSessionInfoEx, out IntPtr buffer, out int bytes)) {
			try {
				if (bytes >= 32) {
					uint sessionFlags = (uint)Marshal.ReadInt32(buffer, 8);
					return sessionFlags == WTS_SESSIONSTATE_LOCK;
				}
			}
			finally {
				WTSFreeMemory(buffer);
			}
		}
		return false;
	}
}