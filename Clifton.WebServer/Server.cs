using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Clifton.ExtensionMethods;

namespace Clifton.WebServer
{
	/// <summary>
	/// A lean and mean web server.
	/// </summary>
	public static class Server
	{
		public static int maxSimultaneousConnections = 20;
		public static Func<ServerError, string> onError;
		public static Func<Session, string, string, string> postProcess = DefaultPostProcess;
		public static Action<Session, HttpListenerContext> onRequest;
		public static int expirationTimeSeconds = 60;		// default expires in 1 minute.
		public static string publicIP = String.Empty;
		public static string validationTokenScript = "@AntiForgeryToken@";
		public static string validationTokenName = "__CSRFToken__";

		public enum ServerError
		{
			OK,
			ExpiredSession,
			NotAuthorized,
			FileNotFound,
			PageNotFound,
			ServerError,
			UnknownType,
			ValidationError,
			AjaxError,
		}

		private static Semaphore sem = new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections);
		private static Router router = new Router();
		private static SessionManager sessionManager = new SessionManager();

		/// <summary>
		/// Starts the web server.
		/// </summary>
		public static void Start(string websitePath)
		{
			onError.IfNull(() => Console.WriteLine("Warning - the onError callback has not been initialized by the application."));

			// publicIP = GetExternalIP();
			Console.WriteLine("public IP: " + publicIP);
			router.WebsitePath = websitePath;
			List<IPAddress> localHostIPs = GetLocalHostIPs();
			HttpListener listener = InitializeListener(localHostIPs);
			Start(listener);
		}

		public static void AddRoute(Route route)
		{
			router.AddRoute(route);
		}

		/// <summary>
		/// Return a ResponsePacket with the specified URL and an optional (singular) parameter.
		/// </summary>
		public static ResponsePacket Redirect(string url, string parm = null)
		{
			ResponsePacket ret = new ResponsePacket() { Redirect = url };
			parm.IfNotNull((p) => ret.Redirect += "?" + p);

			return ret;
		}

		private static HttpListener InitializeListener(List<IPAddress> localhostIPs)
		{
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add("http://localhost/");

			// Listen to IP address as well.
			localhostIPs.ForEach(ip =>
			{
				Console.WriteLine("Listening on IP " + "http://" + ip.ToString() + "/");
				listener.Prefixes.Add("http://" + ip.ToString() + "/");
				
				// For testing on a different port:
				// listener.Prefixes.Add("https://"+ip.ToString()+":8443/");
			});

			// https:
			listener.Prefixes.Add("https://*:443/");

			return listener;
		}

		/// <summary>
		/// Returns list of IP addresses assigned to localhost network devices, such as hardwired ethernet, wireless, etc.
		/// </summary>
		private static List<IPAddress> GetLocalHostIPs()
		{
			IPHostEntry host;
			host = Dns.GetHostEntry(Dns.GetHostName());
			List<IPAddress> ret = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

			return ret;
		}

		/// <summary>
		/// Begin listening to connections on a separate worker thread.
		/// </summary>
		private static void Start(HttpListener listener)
		{
			listener.Start();
			Task.Run(() => RunServer(listener));
		}

		/// <summary>
		/// Start awaiting for connections, up to the "maxSimultaneousConnections" value.
		/// This code runs in a separate thread.
		/// </summary>
		private static void RunServer(HttpListener listener)
		{
			while (true)
			{
				sem.WaitOne();
				StartConnectionListener(listener);
			}
		}

		/// <summary>
		/// Await connections.
		/// </summary>
		private static async void StartConnectionListener(HttpListener listener)
		{
			ResponsePacket resp = null;

			// Wait for a connection.  Return to caller while we wait.
			HttpListenerContext context = await listener.GetContextAsync();
			Session session = sessionManager.GetSession(context.Request.RemoteEndPoint);
			onRequest.IfNotNull(r => r(session, context));

			// Release the semaphore so that another listener can be immediately started up.
			sem.Release();
			Log(context.Request);

			HttpListenerRequest request = context.Request;

			try
			{
				string path = request.RawUrl.LeftOf("?");			// Only the path, not any of the parameters
				string verb = request.HttpMethod;					// get, post, delete, etc.
				string parms = request.RawUrl.RightOf("?");			// Params on the URL itself follow the URL and are separated by a ?
				Dictionary<string, string> kvParams = GetKeyValues(parms);	// Extract into key-value entries.
				string data = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
				GetKeyValues(data, kvParams);
				Log(kvParams);

				if (!VerifyCsrf(session, verb, kvParams))
				{
					Console.WriteLine("CSRF did not match.  Terminating connection.");
					context.Response.OutputStream.Close();
				}
				else
				{
					resp = router.Route(session, verb, path, kvParams);

					// Update session last connection after getting the response, as the router itself validates session expiration only on pages requiring authentication.
					session.UpdateLastConnectionTime();

					if (resp.Error != ServerError.OK)
					{
						resp.Redirect = onError(resp.Error);
					}

					// TODO: Nested exception: is this best?

					try
					{
						Respond(request, context.Response, resp);
					}
					catch (Exception reallyBadException)
					{
						// The response failed!
						// TODO: We need to put in some decent logging!
						Console.WriteLine(reallyBadException.Message);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
				resp = new ResponsePacket() { Redirect = onError(ServerError.ServerError) };
			}
		}

		/// <summary>
		/// If a CSRF validation token exists, verify it matches our session value.
		/// If one doesn't exist, issue a warning to the console.
		/// </summary>
		private static bool VerifyCsrf(Session session, string verb, Dictionary<string, string> kvParams)
		{
			bool ret = true;

			if (verb.ToLower() != "get")
			{
				string token;

				if (kvParams.TryGetValue(Server.validationTokenName, out token))
				{
					ret = session[Server.validationTokenName].ToString() == token;
				}
				else
				{
					Console.WriteLine("Warning - CSRF token is missing.  Consider adding it to the request.");
				}
			}

			return ret;
		}



		private static void Respond(HttpListenerRequest request, HttpListenerResponse response, ResponsePacket resp)
		{
			// Are we redirecting?
			if (String.IsNullOrEmpty(resp.Redirect))
			{
				// No redirect.
				// Do we have a response?
				if (resp.Data != null)
				{
					// Yes we do.
					response.ContentType = resp.ContentType;
					response.ContentLength64 = resp.Data.Length;
					response.OutputStream.Write(resp.Data, 0, resp.Data.Length);
					response.ContentEncoding = resp.Encoding;
				}

				// Whether we do or not, no error occurred, so the response code is OK.
				// For example, we may have just processed an AJAX callback that does not have a data response.
				// Use the status code in the response packet, so the controller has an opportunity to set the response.
				response.StatusCode = (int)resp.StatusCode;
			}
			else
			{
				response.StatusCode = (int)HttpStatusCode.Redirect;

				if (String.IsNullOrEmpty(publicIP))
				{
					string redirectUrl = request.Url.Scheme + "://" + request.Url.Host + resp.Redirect;
					response.Redirect(redirectUrl);
				}
				else
				{
					// response.Redirect("http://" + publicIP + resp.Redirect);					
					string redirectUrl = request.Url.Scheme + "://" + request.Url.Host + resp.Redirect;
					response.Redirect(redirectUrl);
				}
			}

			response.OutputStream.Close();
		}

		/// <summary>
		/// Log requests.
		/// </summary>
		private static void Log(HttpListenerRequest request)
		{
			Console.WriteLine(request.RemoteEndPoint + " " + request.HttpMethod + " /" + request.Url.AbsoluteUri.RightOf('/', 3));
		}

		/// <summary>
		/// Log parameters.
		/// </summary>
		private static void Log(Dictionary<string, string> kv)
		{
			kv.ForEach(kvp => Console.WriteLine(kvp.Key + " : " + Uri.UnescapeDataString(kvp.Value)));
		}

		/// <summary>
		/// Separate out key-value pairs, delimited by & and into individual key-value instances, separated by =
		/// Ex input: username=abc&password=123
		/// </summary>
		private static Dictionary<string, string> GetKeyValues(string data, Dictionary<string, string> kv = null)
		{
			kv.IfNull(() => kv = new Dictionary<string, string>());
			data.If(d => d.Length > 0, (d) => d.Split('&').ForEach(keyValue => kv[keyValue.LeftOf('=')] = System.Uri.UnescapeDataString(keyValue.RightOf('='))));

			return kv;
		}

		private static string GetExternalIP()
		{
			string externalIP;
			externalIP = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
			externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(externalIP)[0].ToString();

			return externalIP;
		}

		/// <summary>
		/// Callable by the application for default handling, therefore must be public.
		/// </summary>
		// TODO: Implement this as interface with a base class so the app can call the base class default behavior.
		public static string DefaultPostProcess(Session session, string fileName, string html)
		{
			string ret = html.Replace(validationTokenScript, "<input name=" + validationTokenName.SingleQuote() +
				" type='hidden' value=" + session[validationTokenName].ToString().SingleQuote() +
				" id='__csrf__'/>");

			// For when the CSRF is in a knockout model or other JSON that is being posted back to the server.
			ret = ret.Replace("@CSRF@", session[validationTokenName].ToString().SingleQuote());

			ret = ret.Replace("@CSRFValue@", session[validationTokenName].ToString());

			return ret;
		}
	}
}
