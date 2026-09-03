using Microsoft.Toolkit.Uwp.Notifications;

var len = args.Length;
var title = len >0 ? args[0] : "Laiko Limitas";
var text = len > 1 ? args[1] : "";
var type = len > 2 && int.TryParse(args[2], out var i) ? i : 0;

new ToastContentBuilder().AddText(title).SetToastScenario((ToastScenario)type).AddAttributionText(text).Show();
