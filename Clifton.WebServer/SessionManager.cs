using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Clifton.Extensions;

namespace Clifton.WebServer
{
	/// <summary>
	/// Sessions are associated with the client IP.
	/// </summary>
	public class Session
	{
		public DateTime LastConnection { get; set; }
		public bool Authorized { get; set; }

		/// <summary>
		/// Can be used by controllers to add additional information that needs to persist in the session.
		/// </summary>
		public Dictionary<string, object> Objects { get; set; }

		public Session()
		{
			Objects = new Dictionary<string, object>();
			UpdateLastConnectionTime();
		}

		public void UpdateLastConnectionTime()
		{
			LastConnection = DateTime.Now;
		}

		/// <summary>
		/// Returns true if the last request exceeds the specified expiration time in seconds.
		/// </summary>
		public bool IsExpired(int expirationInSeconds)
		{
			return (DateTime.Now - LastConnection).TotalSeconds > expirationInSeconds;
		}

		/// <summary>
		/// De-authorize the session and remove the validation token.
		/// </summary>
		public void Expire()
		{
			Authorized = false;
			Objects.Remove(Server.validationTokenName);
		}
	}

	public class SessionManager
	{
		/// <summary>
		/// Track all sessions.
		/// </summary>
		protected Dictionary<IPAddress, Session> sessionMap = new Dictionary<IPAddress, Session>();

		// TODO: We need a way to remove very old sessions so that the server doesn't accumulate thousands of stale endpoints.

		public SessionManager()
		{
			sessionMap = new Dictionary<IPAddress, Session>();
		}

		/// <summary>
		/// Creates or returns the existing session for this remote endpoint.
		/// </summary>
		public Session GetSession(IPEndPoint remoteEndPoint)
		{
			Session session;

			if (!sessionMap.TryGetValue(remoteEndPoint.Address, out session))
			{
				session=new Session();
				session.Objects[Server.validationTokenName] = Guid.NewGuid().ToString();
				sessionMap[remoteEndPoint.Address] = session;
			}
			
			return session;
		}
	}
}
