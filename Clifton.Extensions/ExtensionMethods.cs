using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Clifton.ExtensionMethods
{
	public static class ExtensionMethods
	{
		public static bool If<T>(this T v, Func<T, bool> predicate, Action<T> action)
		{
			bool ret = predicate(v);

			if (ret)
			{
				action(v);
			}

			return ret;
		}

		// Type is...
		public static bool Is<T>(this object obj, Action<T> action)
		{
			bool ret = obj is T;

			if (ret)
			{
				action((T)obj);
			}

			return ret;
		}

		// ---------- if-then-else as lambda expressions --------------

		// If the test returns true, execute the action.
		// Works with objects, not value types.
		public static void IfTrue<T>(this T obj, Func<T, bool> test, Action action)
		{
			if (test(obj))
			{
				action();
			}
		}

		/// <summary>
		/// Returns true if the object is null.
		/// </summary>
		public static bool IfNull<T>(this T obj)
		{
			return obj == null;
		}

		/// <summary>
		/// If the object is null, performs the action and returns true.
		/// </summary>
		public static bool IfNull<T>(this T obj, Action action)
		{
			bool ret = obj == null;

			if (ret) { action(); }

			return ret;
		}

		/// <summary>
		/// Returns true if the object is not null.
		/// </summary>
		public static bool IfNotNull<T>(this T obj)
		{
			return obj != null;
		}

		/// <summary>
		/// Return the result of the func if 'T is not null, passing 'T to func.
		/// </summary>
		public static R IfNotNullReturn<T, R>(this T obj, Func<T, R> func)
		{
			if (obj != null)
			{
				return func(obj);
			}
			else
			{
				return default(R);
			}
		}

		/// <summary>
		/// Return the result of func if 'T is null.
		/// </summary>
		public static R ElseIfNullReturn<T, R>(this T obj, Func<R> func)
		{
			if (obj == null)
			{
				return func();
			}
			else
			{
				return default(R);
			}
		}

		/// <summary>
		/// If the object is not null, performs the action and returns true.
		/// </summary>
		public static bool IfNotNull<T>(this T obj, Action<T> action)
		{
			bool ret = obj != null;

			if (ret) { action(obj); }

			return ret;
		}

		/// <summary>
		/// If the boolean is true, performs the specified action.
		/// </summary>
		public static bool Then(this bool b, Action f)
		{
			if (b) { f(); }

			return b;
		}

		/// <summary>
		/// If the boolean is false, performs the specified action and returns the complement of the original state.
		/// </summary>
		public static void Else(this bool b, Action f)
		{
			if (!b) { f(); }
		}

		// ---------- Dictionary --------------

		/// <summary>
		/// Return the key for the dictionary value or throws an exception if more than one value matches.
		/// </summary>
		public static TKey KeyFromValue<TKey, TValue>(this Dictionary<TKey, TValue> dict, TValue val)
		{
			// from: http://stackoverflow.com/questions/390900/cant-operator-be-applied-to-generic-types-in-c
			// "Instead of calling Equals, it's better to use an IComparer<T> - and if you have no more information, EqualityComparer<T>.Default is a good choice: Aside from anything else, this avoids boxing/casting."
			return dict.Single(t => EqualityComparer<TValue>.Default.Equals(t.Value, val)).Key;
		}

		// ---------- DBNull value --------------

		// Note the "where" constraint, only value types can be used as Nullable<T> types.
		// Otherwise, we get a bizzare error that doesn't really make it clear that T needs to be restricted as a value type.
		public static object AsDBNull<T>(this Nullable<T> item) where T : struct
		{
			// If the item is null, return DBNull.Value, otherwise return the item.
			return item as object ?? DBNull.Value;
		}

		// ---------- ForEach iterators --------------

		/// <summary>
		/// For collections that can change as the entries are being processed, use this method,
		/// as it uses an indexer to iterate through the collection, avoiding the "Collection has been modified"
		/// exception.
		/// </summary>
		public static void IndexerForEach<T>(this IList<T> collection, Action<T> action)
		{
			for (int i = 0; i < collection.Count(); i++)
			{
				action(collection[i]);
			}
		}

		/// <summary>
		/// Implements a ForEach for generic enumerators.
		/// </summary>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var item in collection)
			{
				action(item);
			}
		}

		/// <summary>
		/// ForEach with an index.
		/// </summary>
		public static void ForEachWithIndex<T>(this IEnumerable<T> collection, Action<T, int> action)
		{
			int n = 0;

			foreach (var item in collection)
			{
				action(item, n++);
			}
		}

		public static void ForEachWithIndexOrUntil<T>(this IEnumerable<T> collection, Action<T, int> action, Func<T, int, bool> until)
		{
			int n = 0;

			foreach (var item in collection)
			{
				if (until(item, n))
				{
					break;
				}

				action(item, n++);
			}
		}

		/// <summary>
		/// Executes the "elseAction" if the collection is empty.
		/// </summary>
		public static void ForEachElse<T>(this IEnumerable<T> collection, Action<T> action, Action elseAction)
		{
			if (collection.Count() > 0)
			{
				foreach (var item in collection)
				{
					action(item);
				}
			}
			else
			{
				elseAction();
			}
		}

		/// <summary>
		/// Implements ForEach for non-generic enumerators.
		/// </summary>
		// Usage: Controls.ForEach<Control>(t=>t.DoSomething());
		public static void ForEach<T>(this IEnumerable collection, Action<T> action)
		{
			foreach (T item in collection)
			{
				action(item);
			}
		}

		public static void ForEach(this DataTable dt, Action<DataRow> action)
		{
			foreach (DataRow dtr in dt.Rows)
			{
				action(dtr);
			}
		}

		public static void ForEach(this DataView dv, Action<DataRowView> action)
		{
			foreach (DataRowView drv in dv)
			{
				action(drv);
			}
		}

		/// <summary>
		/// Returns a new dictionary having merged the two source dictionaries.
		/// </summary>
		public static Dictionary<T, U> Merge<T, U>(this Dictionary<T, U> dict1, Dictionary<T, U> dict2)
		{
			return (new[] { dict1, dict2 }).SelectMany(dict => dict).ToDictionary(pair => pair.Key, pair => pair.Value);
		}

		/// <summary>
		/// Merges only the unique elements found in both dictionaries.
		/// </summary>
		public static Dictionary<T, U> MergeNonDuplicates<T, U>(this Dictionary<T, U> dict1, Dictionary<T, U> dict2)
		{
			Dictionary<T, U> dict = dict1.ToDictionary(pair => pair.Key, pair => pair.Value);

			foreach (KeyValuePair<T, U> kvp in dict2)
			{
				if (!dict1.ContainsKey(kvp.Key))
				{
					dict[kvp.Key] = kvp.Value;
				}
			}

			return dict;
		}

		// ---------- collection management --------------

		// From the comments of the blog entry http://blog.jordanterrell.com/post/LINQ-Distinct()-does-not-work-as-expected.aspx regarding why Distinct doesn't work right.
		public static IEnumerable<T> RemoveDuplicates<T>(this IEnumerable<T> source)
		{
			return RemoveDuplicates(source, (t1, t2) => t1.Equals(t2));
		}

		public static IEnumerable<T> RemoveDuplicates<T>(this IEnumerable<T> source, Func<T, T, bool> equater)
		{
			// copy the source array 
			List<T> result = new List<T>();

			foreach (T item in source)
			{
				if (result.All(t => !equater(item, t)))
				{
					// Doesn't exist already: Add it 
					result.Add(item);
				}
			}

			return result;
		}

		public static IEnumerable<T> Replace<T>(this IEnumerable<T> source, T newItem, Func<T, T, bool> equater)
		{
			List<T> result = new List<T>();

			foreach (T item in source)
			{
				if (!equater(item, newItem))
				{
					result.Add(item);
				}
			}

			result.Add(newItem);

			return result;
		}

		public static bool AddIfUnique<T>(this IList<T> list, T item)
		{
			bool ret = false;

			if (!list.Contains(item))
			{
				list.Add(item);
				ret = true;
			}

			return ret;
		}

		public static void RemoveLast<T>(this IList<T> list)
		{
			list.RemoveAt(list.Count - 1);
		}

		/// <summary>
		/// Returns items [idx...end] of a list.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="idx"></param>
		public static List<T> Sublist<T>(this List<T> list, int idx)
		{
			return list.GetRange(idx, list.Count - idx);
		}

		// ---------- events --------------

		/// <summary>
		/// Encapsulates testing for whether the event has been wired up.
		/// </summary>
		public static void Fire<TEventArgs>(this EventHandler<TEventArgs> theEvent, object sender, TEventArgs e) where TEventArgs : EventArgs
		{
			if (theEvent != null)
			{
				theEvent(sender, e);
			}
		}

		// ---------- List to DataTable --------------

		// From http://stackoverflow.com/questions/564366/generic-list-to-datatable
		// which also suggests, for better performance, HyperDescriptor: http://www.codeproject.com/Articles/18450/HyperDescriptor-Accelerated-dynamic-property-acces
		public static DataTable AsDataTable<T>(this IList<T> data)
		{
			PropertyDescriptorCollection props = TypeDescriptor.GetProperties(typeof(T));
			DataTable table = new DataTable();

			for (int i = 0; i < props.Count; i++)
			{
				PropertyDescriptor prop = props[i];
				table.Columns.Add(prop.Name, prop.PropertyType);
			}

			object[] values = new object[props.Count];

			foreach (T item in data)
			{
				for (int i = 0; i < values.Length; i++)
				{
					values[i] = props[i].GetValue(item);
				}
				table.Rows.Add(values);
			}

			return table;
		}

		public static bool IsEmpty(this string s)
		{
			return s == String.Empty;
		}

		// From : http://stackoverflow.com/questions/1945461/how-do-i-sort-an-observable-collection
		public static void Sort<T>(this ObservableCollection<T> observable) where T : IComparable<T>, IEquatable<T>
		{
			List<T> sorted = observable.OrderBy(x => x).ToList();

			int ptr = 0;
			while (ptr < sorted.Count)
			{
				if (!observable[ptr].Equals(sorted[ptr]))
				{
					T t = observable[ptr];
					observable.RemoveAt(ptr);
					observable.Insert(sorted.IndexOf(t), t);
				}
				else
				{
					ptr++;
				}
			}
		}

		/// <summary>
		/// Creates the entry in the dictionary if the key is not found and returns the new entry, otherwise returns the existing entry.
		/// </summary>
		public static U CreateOrGet<T, U>(this Dictionary<T, U> dict, T key) where U : class, new()
		{
			U item = null;

			if (!dict.TryGetValue(key, out item))
			{
				item = new U();
				dict[key] = item;
			}

			return item;
		}
	}

	public static class StringHelpersExtensions
	{
		public static bool IsInt32(this String src)
		{
			int result;
			bool ret = Int32.TryParse(src, out result);

			return ret;
		}

		/// <summary>
		/// Replaces quote with single quote.
		/// </summary>
		public static string ParseQuote(this String src)
		{
			return src.Replace("\"", "'");
		}

		/// <summary>
		/// Replaces single quote with two single quotes.
		/// </summary>
		public static string ParseSingleQuote(this String src)
		{
			return src.Replace("'", "''");
		}

		/// <summary>
		/// Returns a new string surrounded by single quotes.
		/// </summary>
		public static string SingleQuote(this String src)
		{
			return "'" + src + "'";
		}

		public static string Spaced(this String src)
		{
			return " " + src + " ";
		}

		/// <summary>
		/// Returns a new string surrounded by quotes.
		/// </summary>
		public static string Quote(this String src)
		{
			return "\"" + src + "\"";
		}

		/// <summary>
		/// Returns a new string surrounded by brackets.
		/// </summary>
		public static string Brackets(this String src)
		{
			return "[" + src + "]";
		}

		/// <summary>
		/// Returns a new string surrounded by brackets.
		/// </summary>
		public static string CurlyBraces(this String src)
		{
			return "{" + src + "}";
		}

		public static string Between(this String src, char c1, char c2)
		{
			return StringHelpers.Between(src, c1, c2);
		}

		public static string Between(this String src, string s1, string s2)
		{
			return src.RightOf(s1).LeftOf(s2);
		}

		/// <summary>
		/// Return a new string that is "around" (left of and right of) the specified string.
		/// Only the first occurance is processed.
		/// </summary>
		public static string Surrounding(this String src, string s)
		{
			return src.LeftOf(s) + src.RightOf(s);
		}

		public static string RightOf(this String src, char c)
		{
			return StringHelpers.RightOf(src, c);
		}

		public static string RightOf(this String src, char c, int occurance)
		{
			string ret = src;

			while (--occurance >= 0)
			{
				ret = ret.RightOf(c);
			}

			return ret;
		}

		public static bool BeginsWith(this String src, string s)
		{
			return src.StartsWith(s);
		}

		public static string RightOf(this String src, string s)
		{
			string ret = String.Empty;
			int idx = src.IndexOf(s);

			if (idx != -1)
			{
				ret = src.Substring(idx + s.Length);
			}

			return ret;
		}

		public static string RightOfRightmostOf(this String src, char c)
		{
			return StringHelpers.RightOfRightmostOf(src, c);
		}

		public static string LeftOf(this String src, char c)
		{
			return StringHelpers.LeftOf(src, c);
		}

		public static string LeftOf(this String src, string s)
		{
			string ret = src;
			int idx = src.IndexOf(s);

			if (idx != -1)
			{
				ret = src.Substring(0, idx);
			}

			return ret;
		}

		public static string LeftOfRightmostOf(this String src, char c)
		{
			return StringHelpers.LeftOfRightmostOf(src, c);
		}

		public static string LeftOfRightmostOf(this String src, string s)
		{
			string ret = src;
			int idx = src.IndexOf(s);
			int idx2 = idx;

			while (idx2 != -1)
			{
				idx2 = src.IndexOf(s, idx + s.Length);

				if (idx2 != -1)
				{
					idx = idx2;
				}
			}

			if (idx != -1)
			{
				ret = src.Substring(0, idx);
			}

			return ret;
		}

		public static string RightOfRightmostOf(this String src, string s)
		{
			string ret = src;
			int idx = src.IndexOf(s);
			int idx2 = idx;

			while (idx2 != -1)
			{
				idx2 = src.IndexOf(s, idx + s.Length);

				if (idx2 != -1)
				{
					idx = idx2;
				}
			}

			if (idx != -1)
			{
				ret = src.Substring(idx + s.Length, src.Length - (idx + s.Length));
			}

			return ret;
		}

		public static char Rightmost(this String src)
		{
			return StringHelpers.Rightmost(src);
		}

		public static string TrimLastChar(this String src)
		{
			string ret = String.Empty;
			int len = src.Length;

			if (len > 1)
			{
				ret = src.Substring(0, len - 1);
			}

			return ret;
		}

		public static bool IsBlank(this string src)
		{
			return String.IsNullOrEmpty(src) || (src.Trim() == String.Empty);
		}

		/// <summary>
		/// Returns the first occurance of any token given the list of tokens.
		/// </summary>
		public static string Contains(this String src, string[] tokens)
		{
			string ret = String.Empty;
			int firstIndex = 9999;

			// Find the index of the first index encountered.
			foreach (string token in tokens)
			{
				int idx = src.IndexOf(token);

				if ((idx != -1) && (idx < firstIndex))
				{
					ret = token;
					firstIndex = idx;
				}
			}

			return ret;
		}

		public static int to_i(this string src)
		{
			return Convert.ToInt32(src);
		}

		public static bool to_b(this string src)
		{
			return Convert.ToBoolean(src);
		}

		public static T ToEnum<T>(this string src)
		{
			T enumVal = (T)Enum.Parse(typeof(T), src);

			return enumVal;
		}

		public static string SafeToString(this Object src)
		{
			string ret = String.Empty;

			if (src != null)
			{
				ret = src.ToString();
			}

			return ret;
		}

		/// <summary>
		/// Returns a list of substrings separated by the specified delimiter,
		/// ignoring delimiters inside quotes.
		/// </summary>
		public static List<string> DelimitedSplit(this string src, char delimeter, char quote = '\"')
		{
			List<string> ret = new List<string>();
			int idx = 0;
			int start = 0;
			bool inQuote = false;

			while (idx < src.Length)
			{
				if ((!inQuote) && (src[idx] == delimeter))
				{
					ret.Add(src.Substring(start, idx - start).Trim());
					start = idx + 1;		// Ignore the comma.
				}

				if (src[idx] == quote)
				{
					inQuote = !inQuote;
				}

				++idx;
			}

			// The last part.
			if (!inQuote)
			{
				ret.Add(src.Substring(start, idx - start).Trim());
			}

			return ret;
		}

		public static string SplitCamelCase(this string input)
		{
			return Regex.Replace(input, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
		}

		/// <summary>
		/// Searches for all occurances of < > and removes everything between them.
		/// </summary>
		public static string StripHtml(this string s)
		{
			string ret = s;

			int idx1 = s.IndexOf('<');
			int idx2 = s.IndexOf('>');

			while (idx1 < idx2)
			{
				s = s.LeftOf('<') + s.RightOf('>');
				idx1 = s.IndexOf('<');
				idx2 = s.IndexOf('>');
			}

			return s;
		}

		public static string LimitLength(this string s, int len)
		{
			string ret = s;

			if (s.Length > len)
			{
				ret = s.Substring(0, len - 3) + "...";
			}

			return ret;
		}
	}

	/// <summary>
	/// Helpers for string manipulation.
	/// </summary>
	public static class StringHelpers
	{
		/// <summary>
		/// Left of the first occurance of c
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">Return everything to the left of this character.</param>
		/// <returns>String to the left of c, or the entire string.</returns>
		public static string LeftOf(string src, char c)
		{
			string ret = src;

			int idx = src.IndexOf(c);

			if (idx != -1)
			{
				ret = src.Substring(0, idx);
			}

			return ret;
		}

		/// <summary>
		/// Left of the n'th occurance of c.
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">Return everything to the left n'th occurance of this character.</param>
		/// <param name="n">The occurance.</param>
		/// <returns>String to the left of c, or the entire string if not found or n is 0.</returns>
		public static string LeftOf(string src, char c, int n)
		{
			string ret = src;
			int idx = -1;

			while (n > 0)
			{
				idx = src.IndexOf(c, idx + 1);

				if (idx == -1)
				{
					break;
				}

				--n;
			}

			if (idx != -1)
			{
				ret = src.Substring(0, idx);
			}

			return ret;
		}

		/// <summary>
		/// Right of the first occurance of c
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">The search char.</param>
		/// <returns>Returns everything to the right of c, or an empty string if c is not found.</returns>
		public static string RightOf(string src, char c)
		{
			string ret = String.Empty;
			int idx = src.IndexOf(c);

			if (idx != -1)
			{
				ret = src.Substring(idx + 1);
			}

			return ret;
		}

		/// <summary>
		/// Returns all the text to the right of the specified string.
		/// Returns an empty string if the substring is not found.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="substr"></param>
		/// <returns></returns>
		public static string RightOf(string src, string substr)
		{
			string ret = String.Empty;
			int idx = src.IndexOf(substr);

			if (idx != -1)
			{
				ret = src.Substring(idx + substr.Length);
			}

			return ret;
		}

		/// <summary>
		/// Returns the last character in the string.
		/// </summary>
		/// <param name="src"></param>
		/// <returns></returns>
		public static char LastChar(string src)
		{
			return src[src.Length - 1];
		}

		/// <summary>
		/// Returns all but the last character of the source.
		/// </summary>
		/// <param name="src"></param>
		/// <returns></returns>
		public static string RemoveLastChar(string src)
		{
			return src.Substring(0, src.Length - 1);
		}

		/// <summary>
		/// Right of the n'th occurance of c
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">The search char.</param>
		/// <param name="n">The occurance.</param>
		/// <returns>Returns everything to the right of c, or an empty string if c is not found.</returns>
		public static string RightOf(string src, char c, int n)
		{
			string ret = String.Empty;
			int idx = -1;

			while (n > 0)
			{
				idx = src.IndexOf(c, idx + 1);

				if (idx == -1)
				{
					break;
				}

				--n;
			}

			if (idx != -1)
			{
				ret = src.Substring(idx + 1);
			}

			return ret;
		}

		/// <summary>
		/// Returns everything to the left of the righmost char c.
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">The search char.</param>
		/// <returns>Everything to the left of the rightmost char c, or the entire string.</returns>
		public static string LeftOfRightmostOf(string src, char c)
		{
			string ret = src;
			int idx = src.LastIndexOf(c);

			if (idx != -1)
			{
				ret = src.Substring(0, idx);
			}

			return ret;
		}

		/// <summary>
		/// Returns everything to the right of the rightmost char c.
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="c">The seach char.</param>
		/// <returns>Returns everything to the right of the rightmost search char, or an empty string.</returns>
		public static string RightOfRightmostOf(string src, char c)
		{
			string ret = String.Empty;
			int idx = src.LastIndexOf(c);

			if (idx != -1)
			{
				ret = src.Substring(idx + 1);
			}

			return ret;
		}

		/// <summary>
		/// Returns everything between the start and end chars, exclusive.
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="start">The first char to find.</param>
		/// <param name="end">The end char to find.</param>
		/// <returns>The string between the start and stop chars, or an empty string if not found.</returns>
		public static string Between(string src, char start, char end)
		{
			string ret = String.Empty;
			int idxStart = src.IndexOf(start);

			if (idxStart != -1)
			{
				++idxStart;
				int idxEnd = src.IndexOf(end, idxStart);

				if (idxEnd != -1)
				{
					ret = src.Substring(idxStart, idxEnd - idxStart);
				}
			}

			return ret;
		}

		public static string Between(string src, string start, string end)
		{
			string ret = String.Empty;
			int idxStart = src.IndexOf(start);

			if (idxStart != -1)
			{
				idxStart += start.Length;
				int idxEnd = src.IndexOf(end, idxStart);

				if (idxEnd != -1)
				{
					ret = src.Substring(idxStart, idxEnd - idxStart);
				}
			}

			return ret;
		}

		public static string BetweenEnds(string src, char start, char end)
		{
			string ret = String.Empty;
			int idxStart = src.IndexOf(start);

			if (idxStart != -1)
			{
				++idxStart;
				int idxEnd = src.LastIndexOf(end);

				if (idxEnd != -1)
				{
					ret = src.Substring(idxStart, idxEnd - idxStart);
				}
			}

			return ret;
		}

		/// <summary>
		/// Returns the number of occurances of "find".
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <param name="find">The search char.</param>
		/// <returns>The # of times the char occurs in the search string.</returns>
		public static int Count(string src, char find)
		{
			int ret = 0;

			foreach (char s in src)
			{
				if (s == find)
				{
					++ret;
				}
			}

			return ret;
		}

		/// <summary>
		/// Returns the rightmost char in src.
		/// </summary>
		/// <param name="src">The source string.</param>
		/// <returns>The rightmost char, or '\0' if the source has zero length.</returns>
		public static char Rightmost(string src)
		{
			char c = '\0';

			if (src.Length > 0)
			{
				c = src[src.Length - 1];
			}

			return c;
		}

		public static bool BeginsWith(string src, char c)
		{
			bool ret = false;

			if (src.Length > 0)
			{
				ret = src[0] == c;
			}

			return ret;
		}

		public static bool EndsWith(string src, char c)
		{
			bool ret = false;

			if (src.Length > 0)
			{
				ret = src[src.Length - 1] == c;
			}

			return ret;
		}

		public static string EmptyStringAsNull(string src)
		{
			string ret = src;

			if (ret == String.Empty)
			{
				ret = null;
			}

			return ret;
		}

		public static string NullAsEmptyString(string src)
		{
			string ret = src;

			if (ret == null)
			{
				ret = String.Empty;
			}

			return ret;
		}

		public static bool IsNullOrEmpty(string src)
		{
			return ((src == null) || (src == String.Empty));
		}

		// Read about MD5 here: http://en.wikipedia.org/wiki/MD5
		public static string Hash(string src)
		{
			HashAlgorithm hashProvider = new MD5CryptoServiceProvider();
			byte[] bytes = Encoding.UTF8.GetBytes(src);
			byte[] encoded = hashProvider.ComputeHash(bytes);
			return Convert.ToBase64String(encoded);
		}

		/// <summary>
		/// Returns a camelcase string, where the first character is lowercase.
		/// </summary>
		/// <param name="src"></param>
		/// <returns></returns>
		public static string CamelCase(string src)
		{
			return src[0].ToString().ToLower() + src.Substring(1).ToLower();
		}

		/// <summary>
		/// Returns a Pascalcase string, where the first character is uppercase.
		/// </summary>
		/// <param name="src"></param>
		/// <returns></returns>
		public static string PascalCase(string src)
		{
			string ret = String.Empty;

			if (!String.IsNullOrEmpty(src))
			{
				ret = src[0].ToString().ToUpper() + src.Substring(1).ToLower();
			}

			return ret;
		}

		/// <summary>
		/// Returns a Pascal-cased string, given a string with words separated by spaces.
		/// </summary>
		/// <param name="src"></param>
		/// <returns></returns>
		public static string PascalCaseWords(string src)
		{
			StringBuilder sb = new StringBuilder();
			string[] s = src.Split(' ');
			string more = String.Empty;

			foreach (string s1 in s)
			{
				sb.Append(more);
				sb.Append(PascalCase(s1));
				more = " ";
			}

			return sb.ToString();
		}

		public static string SeparateCamelCase(string src)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Char.ToUpper(src[0]));

			for (int i = 1; i < src.Length; i++)
			{
				char c = src[i];

				if (Char.IsUpper(c))
				{
					sb.Append(' ');
				}

				sb.Append(c);
			}

			return sb.ToString();
		}

		public static string[] Split(string source, char delimeter, char quoteChar)
		{
			List<string> retArray = new List<string>();
			int start = 0, end = 0;
			bool insideField = false;

			for (end = 0; end < source.Length; end++)
			{
				if (source[end] == quoteChar)
				{
					insideField = !insideField;
				}
				else if (!insideField && source[end] == delimeter)
				{
					retArray.Add(source.Substring(start, end - start));
					start = end + 1;
				}
			}

			retArray.Add(source.Substring(start));

			return retArray.ToArray();
		}
	}
}

