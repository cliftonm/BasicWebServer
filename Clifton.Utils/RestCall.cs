using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Clifton.Extensions;

namespace Clifton.Utils
{
	/// <summary>
	/// Implmenets a simple RESTful HTTP POST call.
	/// </summary>
	public class RestCall
	{
		protected string url;

		public RestCall(string url)
		{
			this.url = url;
		}

		/// <summary>
		/// Issues an HTTP POST with the specified url and payload parameters.
		/// </summary>
		/// <returns>The string "Error" if a local error occurred, otherwise the server response.</returns>
		public string Post(Dictionary<string, object> urlParams, Dictionary<string, object> payloadParams)
		{
			string ret = "Error";
			string payload = String.Empty;

			urlParams.IfNotNull(p => p.ForEach(parm => url = AddUrlParam(url, parm.Key, parm.Value)));
			payloadParams.IfNotNull(p => p.ForEach(parm => payload = AddPayloadParam(payload, parm.Key, parm.Value)));

			byte[] data = Encoding.ASCII.GetBytes(payload);

			try
			{
				HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.ContentLength = data.Length;

				Stream s = request.GetRequestStream();
				s.Write(data, 0, data.Length);
				s.Flush();			// Make sure we don't close the stream until all data has been written.
				s.Close();

				using (WebResponse response = request.GetResponse())
				{
					using (StreamReader reader = new StreamReader(response.GetResponseStream()))
					{
						ret = reader.ReadToEnd();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			return ret;
		}

		protected string AddUrlParam(string url, string tag, object val)
		{
			string appendChar = (url.Contains("?") ? "&" : "?");
			url = url + appendChar + tag + "=" + val.ToString();

			return url;
		}

		protected string AddPayloadParam(string src, string item, object value)
		{
			if (src.Length > 0)
			{
				src += "&";
			}

			return src + item + "=" + value.ToString();
		}
	}
}
