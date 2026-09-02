using System.Text;

namespace ProfileServer;


public static class Extensions {
	public static RouteHandlerBuilder RequireApiKey(this RouteHandlerBuilder endpoint) =>
		endpoint.AddEndpointFilter(async (context, next) => {
			var httpContext = context.HttpContext;
			if (!httpContext.Request.Headers.TryGetValue("x-api-key", out var extractedKey) ||
				extractedKey != Config.Data.Client.ServerSecret) {
				return Results.Unauthorized();
			}
			return await next(context);
		});


	public static RouteHandlerBuilder RequireBasicAuth(this RouteHandlerBuilder endpoint) =>
	endpoint.AddEndpointFilter(async (context, next) => {
		var httpContext = context.HttpContext;

		if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) { }
		else {
			try {
				var token = authHeader.ToString()["Basic ".Length..].Trim();
				var credentialBytes = Convert.FromBase64String(token);
				var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

				if (credentials.Length == 2 && credentials[0] == Config.Data.UserName && credentials[1] == Config.Data.UserPass) {
					return await next(context);
				}
			}
			catch {
				// Malformed base64 or encoding issues
			}
		}
		httpContext.Response.Headers["WWW-Authenticate"] = "Basic";
		return Results.Unauthorized();
	});
}