
using ProfileService;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Toast;


public class ToastService {
	public string Name { get; }
	public string Product { get; }

	public ToastService (string name, string product) {
		Name = name;  Product = product;
		string mutexName = $"Local\\LaikoLimitas_{Environment.UserName}";
		var _mutex = new Mutex(true, mutexName, out bool isNewInstance);
		if (!isNewInstance) {
			Show("Programa paleista", "");
			Environment.Exit(0); return;
		}		Register();
		_mutex.ReleaseMutex();
	}

	public void Show(string title, string msg = "") => Show(new(title, msg));
	public void Show(ToastMessage msg) {
		var absoluteIconPath = msg.Icon is null ? null : Path.Combine(AppContext.BaseDirectory, "images", $"{msg.Icon.ToString()?.ToLowerInvariant()}.png");
		var audioXml = msg.Silent ? "<audio silent='true' />" : (!string.IsNullOrEmpty(msg.Sound) ? $"<audio src='ms-winsoundevent:Notification.{msg.Sound}' />" : null);

		string prioritySnippet = msg.Priority ? "$toast.Priority = [Windows.UI.Notifications.ToastNotificationPriority]::High; " : "";
		string scenarioSnippet = msg.Priority ? "$template.GetElementsByTagName('toast').Item(0).SetAttribute('scenario', 'alarm'); " : "";

		PsExec(
			"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null; " +
			"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastImageAndText02); " +
			scenarioSnippet +
			(audioXml is null ? "" : (
				$"$template.GetElementsByTagName('toast').Item(0).AppendChild($template.CreateElement('audio')) > $null; " +
				(msg.Silent ? "$template.GetElementsByTagName('audio').Item(0).SetAttribute('silent', 'true'); " :
					(!string.IsNullOrEmpty(msg.Sound) ? $"$template.GetElementsByTagName('audio').Item(0).SetAttribute('src', 'ms-winsoundevent:Notification.{msg.Sound}'); " : "")
				)
			)) +
			"$text = $template.GetElementsByTagName('text'); " +
			$"$text.Item(0).AppendChild($template.CreateTextNode('{msg.Title.Replace("'", "''")}')) > $null; " +
			$"$text.Item(1).AppendChild($template.CreateTextNode('{msg.Message.Replace("'", "''")}')) > $null; " +
			(absoluteIconPath is null ? "" : $"$image = $template.GetElementsByTagName('image'); $image.Item(0).SetAttribute('src', 'file:///{absoluteIconPath.Replace("\\", "/")}'); ") +

			"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " + prioritySnippet +
			$"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{Product}').Show($toast);"
		);
	}

	private static void PsExec(string cmd) {
		var psi = new ProcessStartInfo {
			FileName = "powershell.exe", CreateNoWindow = true, UseShellExecute = false,
			Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\""
		};
		Process.Start(psi);
	}


	private void Register() {
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", $"{Name}.lnk");
		string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
		var link = (IShellLinkW)new ShellLink();
		link.SetPath(exe);
		link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? "");
		link.SetDescription(Name);
		var store = (IPropertyStore)link;
		using (var pv = new PropVariant(Product)) { var val = pv; store.SetValue(ref PROPERTYKEY.AppUserModel_ID, ref val); store.Commit(); }
		((IPersistFile)link).Save(path, true);
	}

	[ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
	[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
	private interface IShellLinkW {
		void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder f, int c, IntPtr p, uint f2);
		void GetIDList(out IntPtr p); void SetIDList(IntPtr p);
		void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder n, int c);
		void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string n);
		void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder d, int c);
		void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string d);
		void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder a, int c);
		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string a);
		void GetHotkey(out short h); void SetHotkey(short h);
		void GetShowCmd(out int s); void SetShowCmd(int s);
		void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder i, int c, out int idx);
		void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string i, int idx);
		void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string p, uint r);
		void Resolve(IntPtr h, uint f);
		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string p);
	}
	[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")] private interface IPropertyStore { void GetCount(out uint c); void GetAt(uint i, out PROPERTYKEY p); void GetValue(ref PROPERTYKEY k, out PropVariant v); void SetValue(ref PROPERTYKEY k, ref PropVariant v); void Commit(); }
	[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")] private interface IPersistFile { void GetClassID(out Guid pClassID); void IsDirty(); void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode); void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember); void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName); void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName); }
	[StructLayout(LayoutKind.Sequential, Pack = 4)] private struct PROPERTYKEY { public Guid fmtid; public uint pid; public static PROPERTYKEY AppUserModel_ID = new() { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 }; }
	[StructLayout(LayoutKind.Explicit)] private struct PropVariant(string str) : IDisposable { [FieldOffset(0)] public short vt = 31; [FieldOffset(8)] public IntPtr pwszVal = Marshal.StringToCoTaskMemUni(str); public readonly void Dispose() { if (vt == 31) Marshal.FreeCoTaskMem(pwszVal); } }
}

