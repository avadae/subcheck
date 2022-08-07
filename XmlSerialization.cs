using System.IO;
using System.Xml;
using System.Text;

namespace SubCheck
{
	public static class XmlSerializer
	{
	    public static void Serialize<T>(string filename, T item)
	    {
			using (var textWriter = new XmlTextWriter(filename, Encoding.ASCII))
			{
				textWriter.Formatting = Formatting.Indented;
				var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
				serializer.Serialize(textWriter, item);
			}
		}
	
	    public static T Deserialize<T>(string filename)
	    {
			using (var sr = new StreamReader(filename))
			{
				var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
				return (T)serializer.Deserialize(sr);
			}
		}
		
	    public static T DeserializeXml<T>(string xml)
	    {
			T result;
			using (var sr = new StreamReader(new MemoryStream(Encoding.Default.GetBytes(xml))))
			{
				var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
				result = (T)serializer.Deserialize(sr);
			}
			return result;
	    }

		public static string SerializeToXml<T>(T item)
		{
			string result;
			using (var textWriter = new StringWriter())
			{
				var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
				serializer.Serialize(textWriter, item);
				result = textWriter.ToString();
			}
			return result;
		}
	}
}