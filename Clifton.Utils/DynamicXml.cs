using System;
using System.Collections.Generic;
using System.Linq;
using System.Dynamic;
using System.Xml.Linq;
using System.Collections;

// From here: https://www.captechconsulting.com/blog/kevin-hazzard/fluent-xml-parsing-using-cs-dynamic-type-part-1

namespace Clifton.Utils
{
	public class DynamicXml : DynamicObject, IEnumerable
	{
		private readonly List<XElement> _elements;

		public DynamicXml(string text)
		{
			var doc = XDocument.Parse(text);
			_elements = new List<XElement> { doc.Root };
		}

		protected DynamicXml(XElement element)
		{
			_elements = new List<XElement> { element };
		}

		protected DynamicXml(IEnumerable<XElement> elements)
		{
			_elements = new List<XElement>(elements);
		}



		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			result = null;

			/* handle the Value and Count special cases */
			if (binder.Name == "Value")
				result = _elements[0].Value;
			else if (binder.Name == "Count")
				result = _elements.Count;
			else
			{
				/* try to find a named attribute first */
				var attr = _elements[0].Attribute(XName.Get(binder.Name));
				if (attr != null)
				{
					/* if a named attribute was found, return that NON-dynamic object */
					result = attr;
				}
				else
				{
					/* find the named descendants */
					var items = _elements.Descendants(XName.Get(binder.Name));
					if (items != null && items.Count() > 0)
					{
						/* prepare a new dynamic object with the list of found descendants */
						result = new DynamicXml(items);
					}
				}
			}
			if (result == null)
			{
				/* not found, create a new element here */
				_elements[0].AddFirst(new XElement(binder.Name));
				result = new DynamicXml(_elements[0].Descendants().First());
			}
			return true;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (binder.Name == "Value")
			{
				/* the Value property is the only one that may be modified.
				TryGetMember actually creates new XML elements in this implementation */
				_elements[0].Value = value.ToString();
				return true;
			}
			return false;
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			int ndx = (int)indexes[0];
			result = new DynamicXml(_elements[ndx]);
			return true;
		}

		public IEnumerator GetEnumerator()
		{
			foreach (var element in _elements)
				yield return new DynamicXml(element);
		}
	}
}