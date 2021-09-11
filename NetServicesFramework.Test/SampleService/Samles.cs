using System;
using System.Collections.Generic;
using System.Linq;

namespace NetServices.SampleService
{
    /// <summary>
    /// Samle implementation
    /// </summary>
    public class SampleSingleton : NetService, ISampleSingleton
    {
        public Func<string> this[int index] => () => $"index = {index}";

        /// <summary>
        /// Property sample
        /// </summary>
        public DateTime Time => DateTime.Now;
    }

    public class SampleInstance : NetService, ISampleInstance
    {
        /// <summary>
        /// Demo ByRef parameters
        /// </summary>
        /// <param name="value">ref parameter</param>
        /// <param name="instance">out parameter</param>
        public string Echo(ref string value, out int count)
        {
            var source = value;

            count = value.Length;

            value = new string(value.Reverse().ToArray());

            return source;
        }
        /// <summary>
        /// See documentation
        /// </summary>
        /// <returns></returns>
        public ISampleInstance Create() => throw new NotImplementedException();
        /// <summary>
        /// A not public constructor is invoked on Create method call
        /// </summary>
        SampleInstance() { }
        /// <summary>
        /// Callback delegate
        /// </summary>
        public event Action<ISampleInstance, int, string> Event;
        /// <summary>
        /// Method return type is interface
        /// </summary>
        /// <param name="value"></param>
        /// <param name="count">Iteration count</param>
        /// <returns>Interface</returns>
        public IEnumerable<KeyValuePair<int, string>> IEnumerableTest(IDictionary<int, string> clientInterface, string value, int count)
        {
            // We may free resources after usage
            using (clientInterface as IDisposable)
                for (int n = 0; n < count; n++)
                {
                    var nvalue = $"{value} {n}";

                    clientInterface[n] = nvalue;

                    yield return new KeyValuePair<int, string>(n, nvalue);

                    Event?.Invoke(this, n, nvalue);
                }
        }
    }

    public class SampleGeneric<T> : NetService, ISampleGeneric<T>
    {

    }


}
