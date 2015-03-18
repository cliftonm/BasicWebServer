using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;

using Clifton.Extensions;
using Clifton.WebServer;

namespace ConsoleWebServer
{
	class Program
	{
		public static Server server;

		static void Main(string[] args)
		{
			string websitePath = GetWebsitePath();
			server = new Server();
			server.OnError = ErrorHandler;
			server.OnRequest = (session, context) =>
			{
				session.Authenticated = true;
				session.UpdateLastConnectionTime();
			};

			server.AddRoute(new Route() { Verb = Router.POST, Path = "/demo/redirect", Handler=new AuthenticatedExpirableRouteHandler(server, RedirectMe) });
			server.AddRoute(new Route() { Verb = Router.PUT, Path = "/demo/ajax", Handler = new AnonymousRouteHandler(server, AjaxResponder) });
			server.AddRoute(new Route() { Verb = Router.GET, Path = "/demo/ajax", Handler = new AnonymousRouteHandler(server, AjaxGetResponder) });

			server.Start(websitePath);
			Console.ReadLine();
		}

		public static ResponsePacket RedirectMe(Session session, Dictionary<string, object> parms)
		{
			return server.Redirect("/demo/clicked");
		}

		public static ResponsePacket AjaxResponder(Session session, Dictionary<string, object> parms)
		{
			string data = "You said " + parms["number"].ToString();
			ResponsePacket ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(data), ContentType = "text" };

			return ret;
		}

		public static ResponsePacket AjaxGetResponder(Session session, Dictionary<string, object> parms)
		{
			ResponsePacket ret = null;

			if (parms.Count != 0)
			{
				string data = "You said " + parms["number"].ToString();
				ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(data), ContentType = "text" };
			}

			return ret;
		}

		public static string GetWebsitePath()
		{
			// Path of our exe.
			string websitePath = Assembly.GetExecutingAssembly().Location;
			websitePath = websitePath.LeftOfRightmostOf("\\").LeftOfRightmostOf("\\").LeftOfRightmostOf("\\") + "\\Website";

			return websitePath;
		}

		public static string ErrorHandler(Server.ServerError error)
		{
			string ret = null;

			switch (error)
			{
				case Server.ServerError.ExpiredSession:
					ret= "/ErrorPages/expiredSession.html";
					break;
				case Server.ServerError.FileNotFound:
					ret = "/ErrorPages/fileNotFound.html";
					break;
				case Server.ServerError.NotAuthorized:
					ret = "/ErrorPages/notAuthorized.html";
					break;
				case Server.ServerError.PageNotFound:
					ret = "/ErrorPages/pageNotFound.html";
					break;
				case Server.ServerError.ServerError:
					ret = "/ErrorPages/serverError.html";
					break;
				case Server.ServerError.UnknownType:
					ret = "/ErrorPages/unknownType.html";
					break;
				case Server.ServerError.ValidationError:
					ret = "/ErrorPages/validationError.html";
					break;
			}

			return ret;
		}
	}
}
