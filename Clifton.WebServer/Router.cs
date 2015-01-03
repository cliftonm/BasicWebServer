using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clifton.ExtensionMethods;

namespace Clifton.WebServer
{
	public class ResponsePacket
	{
		public string Redirect { get; set; }
		public byte[] Data { get; set; }
		public string ContentType { get; set; }
		public Encoding Encoding { get; set; }
		public Server.ServerError Error { get; set; }
	}

	public class Route
	{
		public string Verb { get; set; }
		public string Path { get; set; }
		public RouteHandler Handler { get; set; }
	}

	internal class ExtensionInfo
	{
		public string ContentType { get; set; }
		public Func<Session, string, string, ExtensionInfo, ResponsePacket> Loader { get; set; }
	}

	public class Router
	{
		public string WebsitePath { get; set; }

		public const string POST = "post";
		public const string GET = "get";
		public const string PUT = "put";
		public const string DELETE = "delete";

		private Dictionary<string, ExtensionInfo> extFolderMap;
		private List<Route> routes;

		public Router()
		{
			routes = new List<WebServer.Route>();

			extFolderMap = new Dictionary<string, ExtensionInfo>() 
			{
				{"ico", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/ico"}},
				{"png", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/png"}},
				{"jpg", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/jpg"}},
				{"gif", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/gif"}},
				{"bmp", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/bmp"}},
				{"html", new ExtensionInfo() {Loader=PageLoader, ContentType="text/html"}},
				{"css", new ExtensionInfo() {Loader=FileLoader, ContentType="text/css"}},
				{"js", new ExtensionInfo() {Loader=FileLoader, ContentType="text/javascript"}},
				{"", new ExtensionInfo() {Loader=PageLoader, ContentType="text/html"}},	  // no extension is assumed to be .html
			};
		}

		public void AddRoute(Route route)
		{
			routes.Add(route);
		}

		public ResponsePacket Route(Session session, string verb, string path, Dictionary<string, string> kvParams)
		{
			string ext = path.RightOfRightmostOf('.');
			ExtensionInfo extInfo;
			ResponsePacket ret = null;
			verb = verb.ToLower();

			if (verb != GET)
			{
				if (!VerifyCSRF(session, kvParams))
				{
					// Don't like multiple return points, but it's so convenient here!
					return Server.Redirect(Server.onError(Server.ServerError.ValidationError));
				}
			}

			if (extFolderMap.TryGetValue(ext, out extInfo))
			{
				string wpath = path.Substring(1).Replace('/', '\\');			// Strip off leading '/' and reformat as with windows path separator.
				string fullPath = Path.Combine(WebsitePath, wpath);

				Route handler = routes.SingleOrDefault(r => verb == r.Verb.ToLower() && path == r.Path);

				if (handler != null)
				{
					// Application has a handler for this route.
					ResponsePacket handlerResponse = handler.Handler.Handle(session, kvParams);

					if (handlerResponse == null)
					{
						// Respond with default content loader.
						ret = extInfo.Loader(session, fullPath, ext, extInfo);
					}
					else
					{
						// Respond with redirect.
						ret = handlerResponse;
					}
				}
				else
				{
					// Attempt default behavior
					ret = extInfo.Loader(session, fullPath, ext, extInfo);
				}
			}
			else
			{
				ret = new ResponsePacket() { Error = Server.ServerError.UnknownType };
			}
	
			return ret;
		}

		/// <summary>
		/// If a CSRF validation token exists, verify it matches our session value.
		/// If one doesn't exist, issue a warning to the console.
		/// </summary>
		private bool VerifyCSRF(Session session, Dictionary<string,string> kvParams)
		{
			bool ret = true;
			string token;

			if (kvParams.TryGetValue(Server.validationTokenName, out token))
			{
				ret = session.Objects[Server.validationTokenName].ToString() == token;
			}
			else
			{
				Console.WriteLine("Warning - CSRF token is missing.  Consider adding it to the request.");
			}

			return ret;
		}

		/// <summary>
		/// Read in what is basically a text file and return a ResponsePacket with the text UTF8 encoded.
		/// </summary>
		private ResponsePacket FileLoader(Session session, string fullPath, string ext, ExtensionInfo extInfo)
		{
			ResponsePacket ret;

			if (!File.Exists(fullPath))
			{
				ret = new ResponsePacket() { Error = Server.ServerError.FileNotFound };
			}
			else
			{
				string text = File.ReadAllText(fullPath);
				ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(text), ContentType = extInfo.ContentType, Encoding = Encoding.UTF8 };
			}

			return ret;
		}

		/// <summary>
		/// Read in an image file and returns a ResponsePacket with the raw data.
		/// </summary>
		private ResponsePacket ImageLoader(Session session, string fullPath, string ext, ExtensionInfo extInfo)
		{
			ResponsePacket ret;

			if (!File.Exists(fullPath))
			{
				ret = new ResponsePacket() { Error = Server.ServerError.FileNotFound };
			}
			else
			{
				FileStream fStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
				BinaryReader br = new BinaryReader(fStream);
				ret = new ResponsePacket() { Data = br.ReadBytes((int)fStream.Length), ContentType = extInfo.ContentType };
				br.Close();
				fStream.Close();
			}

			return ret;
		}

		/// <summary>
		/// Load an HTML file, taking into account missing extensions and a file-less IP/domain, which should default to index.html.
		/// </summary>
		private ResponsePacket PageLoader(Session session, string fullPath, string ext, ExtensionInfo extInfo)
		{
			ResponsePacket ret;

			if (fullPath == WebsitePath)		// If nothing follows the domain name or IP, then default to loading index.html.
			{
				ret = Route(session, GET, "/index.html", null);
			}
			else
			{
				if (String.IsNullOrEmpty(ext))
				{
					// No extension, so we make it ".html"
					fullPath = fullPath + ".html";
				}

				// Inject the "Pages" folder into the path
				fullPath = WebsitePath + "\\Pages" + fullPath.RightOf(WebsitePath);

				// Custom, for page not found error.
				if (!File.Exists(fullPath))
				{
					ret = new ResponsePacket() { Error = Server.ServerError.PageNotFound };
				}
				else
				{
					string text = File.ReadAllText(fullPath);
					text = Server.postProcess(session, text);			// post processing option, such as adding a validation token.
					ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(text), ContentType = extInfo.ContentType, Encoding = Encoding.UTF8 };
				}
			}

			return ret;
		}
	}
}
