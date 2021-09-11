using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetServices.SampleService
{
    /// <summary>
    /// A sample interface
    /// </summary>
    public interface ISampleSingleton : INetService
    {
        /// <summary>
        /// Property
        /// </summary>
        DateTime Time { get; }
        /// <summary>
        /// Method returns delegate
        /// </summary>
        /// <param name="index">Parameter</param>
        /// <returns></returns>
        Func<string> this[int index] { get; }
    }

    public interface ISampleInstance : INetService
    {
        /// <summary>
        /// Sample using ByRef parameters
        /// </summary>
        /// <param name="value">Returns string in reverse order</param>
        /// <param name="length">Returns string length</param>
        /// <returns>Source string</returns>
        string Echo(ref string value, out int length);
        /// <summary>
        /// Acts as constructor call
        /// </summary>
        /// <returns>An instance of interface implementation</returns>
        ISampleInstance Create();
        /// <summary>
        /// Obtaining of interface as a return value
        /// </summary>
        /// <param name="clientInterface">Client exports its interface</param>
        /// <param name="value">Prameter</param>
        /// <param name="count">Parameter</param>
        /// <returns>Remote interface reference</returns>
        IEnumerable<KeyValuePair<int, string>> IEnumerableTest(IDictionary<int,string> clientInterface, string value, int count);
        /// <summary>
        /// Sample of passing client delegate
        /// </summary>
        event Action<ISampleInstance, int, string> Event;
    }
    /// <summary>
    /// Sample generic service
    /// </summary>
    /// <typeparam name="T">Generic parameter</typeparam>
    [SvcAccess(AuthType.StrongAuth | AuthType.Password)]
    public interface ISampleGeneric<T> : INetService 
    {
        /// <summary>
        /// No need to implement
        /// </summary>
        /// <returns>The implementation class type</returns>
        string ToString();
    }
}
