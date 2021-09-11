using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace NetServices.Surrogate
{
    /// <summary>
    /// A sample SerializationSurrogate for XmlDocument class
    /// </summary>
    [SerializationSurrogate(typeof(XmlDocument))]
    public class XmlDocumentSurrogate : ISerializationSurrogate
    {
        void ISerializationSurrogate.GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            var sb = new StringBuilder(); using (var xw = XmlWriter.Create(sb))
                (obj as XmlDocument).WriteTo(xw);

            info.AddValue("Xml", Encoding.UTF8.GetBytes(sb.ToString()));
        }

        object ISerializationSurrogate.SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            var xdoc = new XmlDocument();

            xdoc.LoadXml(Encoding.UTF8.GetString(info.GetValue<byte[]>("Xml")));

            return xdoc;
        }
    }



}