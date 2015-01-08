using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clifton.Extensions;

namespace Clifton.WebServer
{
	/// <summary>
	/// The base class for route handlers.
	/// </summary>
	public abstract class RouteHandler
	{
		protected Func<Session, Dictionary<string, string>, ResponsePacket> handler;

		public RouteHandler(Func<Session, Dictionary<string, string>, ResponsePacket> handler)
		{
			this.handler = handler;
		}

		public abstract ResponsePacket Handle(Session session, Dictionary<string, string> parms);

		protected ResponsePacket InvokeHandler(Session session, Dictionary<string, string> parms)
		{
			ResponsePacket ret = null;
			handler.IfNotNull((h) => ret = h(session, parms));

			return ret;
		}
	}

	/// <summary>
	/// Page is always visible.
	/// </summary>
	public class AnonymousRouteHandler : RouteHandler
	{
		public AnonymousRouteHandler(Func<Session, Dictionary<string, string>, ResponsePacket> handler = null)
			: base(handler)
		{
		}

		public override ResponsePacket Handle(Session session, Dictionary<string, string> parms)
		{
			return InvokeHandler(session, parms);
		}
	}

	/// <summary>
	/// Page is visible only to authorized users.
	/// </summary>
	public class AuthenticatedRouteHandler : RouteHandler
	{
		public AuthenticatedRouteHandler(Func<Session, Dictionary<string, string>, ResponsePacket> handler = null)
			: base(handler)
		{
		}

		public override ResponsePacket Handle(Session session, Dictionary<string, string> parms)
		{
			ResponsePacket ret;

			if (session.Authorized)
			{
				ret = InvokeHandler(session, parms);
			}
			else
			{
				ret = Server.Redirect(Server.onError(Server.ServerError.NotAuthorized));
			}

			return ret;
		}
	}

	/// <summary>
	/// Page is visible only to authorized users whose session has not expired.
	/// </summary>
	public class AuthenticatedExpirableRouteHandler : AuthenticatedRouteHandler
	{
		public AuthenticatedExpirableRouteHandler(Func<Session, Dictionary<string, string>, ResponsePacket> handler = null)
			: base(handler)
		{
		}

		public override ResponsePacket Handle(Session session, Dictionary<string, string> parms)
		{
			ResponsePacket ret;

			if (session.IsExpired(Server.expirationTimeSeconds))
			{
				session.Expire();
				ret = Server.Redirect(Server.onError(Server.ServerError.ExpiredSession));
			}
			else
			{
				ret = base.Handle(session, parms);
			}

			return ret;
		}
	}
}
