using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace NetServices
{
    /// <summary>
    /// Substitutes null values in serializatin/deserialization process
    /// </summary>
    [Serializable]
    public class Null { }
    /// <summary>
    /// Marks ISerializationSurrogate implementations
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SerializationSurrogateAttribute : Attribute
    {
        /// <summary>
        /// Target type
        /// </summary>
        public readonly Type Type;
        /// <summary>
        /// Creates attribute instance
        /// </summary>
        /// <param name="type">Target type</param>
        public SerializationSurrogateAttribute(Type type) => Type = type;
    }
    /// <summary>
    /// Handly extensions for serialization/deserialization
    /// </summary>
    public static class SvcFormatter
    {
        /// <summary>
        /// Deserialize method
        /// </summary>
        /// <param name="value">Serialized data</param>
        /// <returns>Deserialized object</returns>
        public static object Deserialize(this byte[] value)
        {
            return value == null ? null : new Func<object>(() =>
            {
                using (var stream = new MemoryStream(value))
                {
                    var result = Formatter.Deserialize(stream);
                    return result is Null ? null : result;
                }
            })();
        }
        /// <summary>
        /// Deserialize and cast
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <param name="value">Serialized data</param>
        /// <returns>Deserialized instance of type T</returns>
        public static T Deserialize<T>(this byte[] value) where T : class
            => value.Deserialize() as T;

        /// <summary>
        /// Serialize object
        /// </summary>
        /// <param name="value">Source serializable object</param>
        /// <returns>Serialized data</returns>
        public static byte[] Serialize(this object value)
        {
            using (var ms = new MemoryStream())
                return new Func<byte[]>(() =>
                {
                    Formatter.Serialize(ms, value == null ? new Null() : value);
                    return ms.ToArray();
                })();
        }
        
        /// <summary>
        /// The BinaryFormatter instance initialized with surrogate imlementations
        /// </summary>
        public static BinaryFormatter Formatter => _Formatter.Value;
        /// <summary>
        /// Search for ISerializationSurrogate implementations
        /// </summary>
        static readonly Lazy<BinaryFormatter> _Formatter = new Lazy<BinaryFormatter>(() =>
        {
            var selector = new SurrogateSelector();

            foreach (var surrogate in AppDomain.CurrentDomain.GetTypes(x => typeof(ISerializationSurrogate).IsAssignableFrom(x))) try
                {
                    var type = surrogate.GetCustomAttribute<SerializationSurrogateAttribute>()?.Type;

                    if (type != null) selector.AddSurrogate(type, new StreamingContext(StreamingContextStates.All),
                        surrogate.GetConstructor(Type.EmptyTypes).Invoke(new object[0]) as ISerializationSurrogate);
                }
                catch { }

            return new BinaryFormatter() { SurrogateSelector = selector };
        });
        /// <summary>
        /// Shortens GetValue method call in surrogate implementation
        /// </summary>
        /// <typeparam name="T">Type to cast deserialized value</typeparam>
        /// <param name="info">SerializationInfo parameter value</param>
        /// <param name="name">The name associated with the value to retrieve</param>
        /// <returns>Value of stored type</returns>
        public static T GetValue<T>(this SerializationInfo info, string name)
            => (T)info.GetValue(name, typeof(T));

        /// <summary>
        /// Adds additional surrogate implementation to <see cref="Formatter"/>
        /// </summary>
        /// <typeparam name="T">A nonSerializable type</typeparam>
        /// <param name="surrogate">The ISerializationSurrogate implementation instance</param>
        public static void AddSurrogate<T>(ISerializationSurrogate surrogate)
            => (Formatter.SurrogateSelector as SurrogateSelector).AddSurrogate(typeof(T)
                , new StreamingContext(StreamingContextStates.All), surrogate);
    }
}
