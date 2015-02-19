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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clifton.Extensions;

namespace Clifton.WebServer
{
	/// <summary>
	/// The base class for route handlers.  If not for being abstract, this would be the equivalent of an anonymous handler,
	/// but we want to enforce an explicit declaration of that so the developer doesn't accidentally use RouteHandler without
	/// realizing that it's an anonymous, unauthenticated, no session timeout check, handler.  Defensive Programming!
	/// </summary>
	public abstract class RouteHandler
	{
		protected Func<Session, Dictionary<string, object>, ResponsePacket> handler;

		public RouteHandler(Func<Session, Dictionary<string, object>, ResponsePacket> handler)
		{
			this.handler = handler;
		}

		public virtual ResponsePacket Handle(Session session, Dictionary<string, object> parms)
		{
			return InvokeHandler(session, parms);
		}

		/// <summary>
		/// CanHandle is used only for determining which handler, in a multiple handler for a single route, can actually handle to session and params for that route.
		/// </summary>
		public virtual bool CanHandle(Session session, Dictionary<string, object> parms) { return true; }

		protected ResponsePacket InvokeHandler(Session session, Dictionary<string, object> parms)
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
		public AnonymousRouteHandler(Func<Session, Dictionary<string, object>, ResponsePacket> handler = null)
			: base(handler)
		{
		}
	}

	/// <summary>
	/// Page is visible only to authorized users.
	/// </summary>
	public class AuthenticatedRouteHandler : RouteHandler
	{
		public AuthenticatedRouteHandler(Func<Session, Dictionary<string, object>, ResponsePacket> handler = null)
			: base(handler)
		{
		}

		public override ResponsePacket Handle(Session session, Dictionary<string, object> parms)
		{
			ResponsePacket ret;

			if (session.Authenticated)
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
		public AuthenticatedExpirableRouteHandler(Func<Session, Dictionary<string, object>, ResponsePacket> handler = null)
			: base(handler)
		{
		}

		public override ResponsePacket Handle(Session session, Dictionary<string, object> parms)
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
