using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;

using Clifton.ExtensionMethods;
using Clifton.WebServer;

namespace ConsoleWebServer
{
	class Program
	{
		static void Main(string[] args)
		{
			string websitePath = GetWebsitePath();
			Server.onError = ErrorHandler;
			Server.onRequest = (session, context) =>
			{
				session.Authorized = true;
				session.UpdateLastConnectionTime();
			};

			Server.AddRoute(new Route() { Verb = Router.POST, Path = "/demo/redirect", Handler=new AuthenticatedExpirableRouteHandler(RedirectMe) });
			Server.AddRoute(new Route() { Verb = Router.PUT, Path = "/demo/ajax", Handler = new AnonymousRouteHandler(AjaxResponder) });
			Server.AddRoute(new Route() { Verb = Router.GET, Path = "/demo/ajax", Handler = new AnonymousRouteHandler(AjaxGetResponder) });

			Server.Start(websitePath);
			Console.ReadLine();
		}

		public static ResponsePacket RedirectMe(Session session, Dictionary<string, string> parms)
		{
			return Server.Redirect("/demo/clicked");
		}

		public static ResponsePacket AjaxResponder(Session session, Dictionary<string, string> parms)
		{
			string data = "You said " + parms["number"];
			ResponsePacket ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(data), ContentType = "text" };

			return ret;
		}

		public static ResponsePacket AjaxGetResponder(Session session, Dictionary<string, string> parms)
		{
			ResponsePacket ret = null;

			if (parms.Count != 0)
			{
				string data = "You said " + parms["number"];
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
