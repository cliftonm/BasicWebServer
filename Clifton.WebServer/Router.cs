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

	internal class ExtensionInfo
	{
		public string ContentType { get; set; }
		public Func<string, string, ExtensionInfo, ResponsePacket> Loader { get; set; }
	}

	public class Router
	{
		public string WebsitePath { get; set; }

		private  const string POST = "post";
		private const string GET = "get";

		private Dictionary<string, ExtensionInfo> extFolderMap;

		public Router()
		{
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

		public ResponsePacket Route(string verb, string path, Dictionary<string, string> kvParams)
		{
			string ext = path.RightOfRightmostOf('.');
			ExtensionInfo extInfo;
			ResponsePacket ret = null;

			if (extFolderMap.TryGetValue(ext, out extInfo))
			{
				path = path.Substring(1).Replace('/', '\\');			// Strip off leading '/' and reformat as with windows path separator.
				string fullPath = Path.Combine(WebsitePath, path);
				ret = extInfo.Loader(fullPath, ext, extInfo);
			}
			else
			{
				ret = new ResponsePacket() { Error = Server.ServerError.UnknownType };
			}
	
			return ret;
		}

		/// <summary>
		/// Read in what is basically a text file and return a ResponsePacket with the text UTF8 encoded.
		/// </summary>
		private ResponsePacket FileLoader(string fullPath, string ext, ExtensionInfo extInfo)
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
		private ResponsePacket ImageLoader(string fullPath, string ext, ExtensionInfo extInfo)
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
		private ResponsePacket PageLoader(string fullPath, string ext, ExtensionInfo extInfo)
		{
			ResponsePacket ret;

			if (fullPath == WebsitePath)		// If nothing follows the domain name or IP, then default to loading index.html.
			{
				ret = Route(GET, "/index.html", null);
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
					ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(text), ContentType = extInfo.ContentType, Encoding = Encoding.UTF8 };
				}
			}

			return ret;
		}
	}
}
