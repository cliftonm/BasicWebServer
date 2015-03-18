/*
Copyright (c) 2015, Marc Clifton
All rights reserved.

Article: http://www.codeproject.com/Articles/859108/Writing-a-Web-Server-from-Scratch
Git: https://github.com/cliftonm/BasicWebServer.git

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list
  of conditions and the following disclaimer. 

* Redistributions in binary form must reproduce the above copyright notice, this 
  list of conditions and the following disclaimer in the documentation and/or other
  materials provided with the distribution. 
 
* Neither the name of MyXaml nor the names of its contributors may be
  used to endorse or promote products derived from this software without specific
  prior written permission. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

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

using Clifton.Extensions;

namespace Clifton.WebServer
{
	/// <summary>
	/// A lean and mean web server.
	/// </summary>
	public class Server
	{
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

		public Func<Session, string, string, string> PostProcess { get; set; }
		public Func<ServerError, string> OnError { get; set; }
		public int MaxSimultaneousConnections { get; set; }
		public int ExpirationTimeSeconds { get; set; }
		public string ValidationTokenName { get; set; }

		protected Action<Session, HttpListenerContext> onRequest;
		protected string protectedIP = String.Empty;
		protected string validationTokenScript = "@AntiForgeryToken@";
		protected string publicIP = null;

		protected Semaphore sem;
		protected Router router;
		protected SessionManager sessionManager;

		public Server()
		{
			MaxSimultaneousConnections = 20;			// TODO: This needs to be externally settable before initializing the semaphore.
			ExpirationTimeSeconds = 60;					// default expires in 1 minute.
			ValidationTokenName = "__CSRFToken__";


			sem = new Semaphore(MaxSimultaneousConnections, MaxSimultaneousConnections);
			router = new Router(this);
			sessionManager = new SessionManager(this);
			PostProcess = DefaultPostProcess;
		}

		/// <summary>
		/// Starts the web server.
		/// </summary>
		public void Start(string websitePath, int port = 80, bool acquirePublicIP = false)
		{
			OnError.IfNull(() => Console.WriteLine("Warning - the onError callback has not been initialized by the application."));

			if (acquirePublicIP)
			{
				publicIP = GetExternalIP();
				Console.WriteLine("public IP: " + publicIP);
			}

			router.WebsitePath = websitePath;
			List<IPAddress> localHostIPs = GetLocalHostIPs();
			HttpListener listener = InitializeListener(localHostIPs, port);
			Start(listener);
		}

		public void AddRoute(Route route)
		{
			router.AddRoute(route);
		}

		/// <summary>
		/// Return a ResponsePacket with the specified URL and an optional (singular) parameter.
		/// </summary>
		public ResponsePacket Redirect(string url, string parm = null)
		{
			ResponsePacket ret = new ResponsePacket() { Redirect = url };
			parm.IfNotNull((p) => ret.Redirect += "?" + p);

			return ret;
		}

		private HttpListener InitializeListener(List<IPAddress> localhostIPs, int port)
		{
			HttpListener listener = new HttpListener();
			string url = UrlWithPort("http://localhost", port);

			try
			{
				listener.Prefixes.Add(url);
				Console.WriteLine("Listening on " + url);
			}
			catch
			{
				// Ignore exception, which will occur on AWS servers.
			}

			// Listen to IP address as well.
			localhostIPs.ForEach(ip =>
			{
				url = UrlWithPort("http://" + ip.ToString(), port);
				Console.WriteLine("Listening on "+url);
				listener.Prefixes.Add(url);
				
				// For testing on a different port:
				// listener.Prefixes.Add("https://"+ip.ToString()+":8443/");
			});

			// TODO: What's listening on this port that is preventing me from adding an HTTPS listener???  This started all of a sudden after a reboot.
			// https:
			// listener.Prefixes.Add("https://*:443/");

			return listener;
		}

		/// <summary>
		/// Returns the url appended with a / for port 80, otherwise, the [url]:[port]/ if the port is not 80.
		/// </summary>
		private string UrlWithPort(string url, int port)
		{
			string ret = url + "/";

			if (port != 80)
			{
				ret = url + ":" + port.ToString() + "/";
			}

			return ret;
		}

		/// <summary>
		/// Returns list of IP addresses assigned to localhost network devices, such as hardwired ethernet, wireless, etc.
		/// </summary>
		private List<IPAddress> GetLocalHostIPs()
		{
			IPHostEntry host;
			host = Dns.GetHostEntry(Dns.GetHostName());
			List<IPAddress> ret = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

			return ret;
		}

		/// <summary>
		/// Begin listening to connections on a separate worker thread.
		/// </summary>
		private void Start(HttpListener listener)
		{
			listener.Start();
			Task.Run(() => RunServer(listener));
		}

		/// <summary>
		/// Start awaiting for connections, up to the "maxSimultaneousConnections" value.
		/// This code runs in a separate thread.
		/// </summary>
		private void RunServer(HttpListener listener)
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
		private async void StartConnectionListener(HttpListener listener)
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
				Dictionary<string, object> kvParams = GetKeyValues(parms);	// Extract into key-value entries.
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
						resp.Redirect = OnError(resp.Error);
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
				resp = new ResponsePacket() { Redirect = OnError(ServerError.ServerError) };
			}
		}

		/// <summary>
		/// If a CSRF validation token exists, verify it matches our session value.
		/// If one doesn't exist, issue a warning to the console.
		/// </summary>
		private bool VerifyCsrf(Session session, string verb, Dictionary<string, object> kvParams)
		{
			bool ret = true;

			if (verb.ToLower() != "get")
			{
				object token;

				if (kvParams.TryGetValue(ValidationTokenName, out token))
				{
					ret = session[ValidationTokenName].ToString() == token.ToString();
				}
				else
				{
					Console.WriteLine("Warning - CSRF token is missing.  Consider adding it to the request.");
				}
			}

			return ret;
		}



		private void Respond(HttpListenerRequest request, HttpListenerResponse response, ResponsePacket resp)
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
		private void Log(HttpListenerRequest request)
		{
			Console.WriteLine(request.RemoteEndPoint + " " + request.HttpMethod + " /" + request.Url.AbsoluteUri.RightOf('/', 3));
		}

		/// <summary>
		/// Log parameters.
		/// </summary>
		private void Log(Dictionary<string, object> kv)
		{
			kv.ForEach(kvp => Console.WriteLine(kvp.Key + " : " + Uri.UnescapeDataString(kvp.Value.ToString())));
		}

		/// <summary>
		/// Separate out key-value pairs, delimited by & and into individual key-value instances, separated by =
		/// Ex input: username=abc&password=123
		/// </summary>
		private Dictionary<string, object> GetKeyValues(string data, Dictionary<string, object> kv = null)
		{
			kv.IfNull(() => kv = new Dictionary<string, object>());
			data.If(d => d.Length > 0, (d) => d.Split('&').ForEach(keyValue => kv[keyValue.LeftOf('=')] = System.Uri.UnescapeDataString(keyValue.RightOf('='))));

			return kv;
		}

		private string GetExternalIP()
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
		public string DefaultPostProcess(Session session, string fileName, string html)
		{
			string ret = html.Replace(validationTokenScript, "<input name=" + ValidationTokenName.SingleQuote() +
				" type='hidden' value=" + session[ValidationTokenName].ToString().SingleQuote() +
				" id='__csrf__'/>");

			// For when the CSRF is in a knockout model or other JSON that is being posted back to the server.
			ret = ret.Replace("@CSRF@", session[ValidationTokenName].ToString().SingleQuote());

			ret = ret.Replace("@CSRFValue@", session[ValidationTokenName].ToString());

			return ret;
		}
	}
}
