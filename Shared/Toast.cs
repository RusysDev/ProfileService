
namespace ProfileService;


public enum ToastIcon { Info, TimeRem, TimeAdd, Lock, Unlock }

public class ToastMessage {
	public string Title { get; set; }
	public string Message { get; set; }
	public ToastIcon? Icon { get; set; }
	public string? Sound { get; set; }
	public bool Silent { get; set; } = false;
	public bool Priority { get; set; } = true;

	public ToastMessage() { Title = ""; Message = ""; }
	public ToastMessage(string title, string? msg = "") {
		Title = title; Message = msg ?? "";
	}
}