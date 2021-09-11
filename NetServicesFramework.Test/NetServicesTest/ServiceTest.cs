using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Security.Cryptography;
using NetServices.SampleService;
using System.Diagnostics;

namespace NetServices.Test
{
    public static partial class Program
    {
        static void Assert(bool isTrue, string message = "") { if (!isTrue) throw new Exception(message); }

        static void ServicesTest(object obj)
        {
            var args = obj as object[];

            var client = args[0] as SvcClient;

            var thread = args.Length > 1 ? $"<Thread {args[1]}> " : "";

            // Creating an instance. Its good idea to explicitly dispose instances to free server recources
            using (var instance = client.GetProxy<ISampleInstance>().Create())
            {
                // Using ByRef parameters (see implementation)
                string refvalue = "string";

                int length; var echo = instance.Echo(ref refvalue, out length);

                Console.WriteLine($"{thread}echo: '{echo}' refvalue = '{refvalue}' length = {length}");

                // You may export delegates
                instance.Event += (ISampleInstance s, int n, string v) =>
                {
                    Assert(ReferenceEquals(s, instance));

                    Console.WriteLine($"{thread}Event: {n} '{v}'");
                };

                // Exported as IDictionary<int, string>
                var lookup = new Dictionary<int, string>();

                // And now have a look at the interface implemementation and see the result
                foreach (var item in instance.IEnumerableTest(lookup, "string #", 5))
                {
                    Console.WriteLine($"{thread}foreach: {item.Key} = '{item.Value}' lookup.Count = {lookup.Count}");
                }

                // Server implementation filled lookup with some data
                foreach (var item in lookup) Console.WriteLine($"{thread} lookup: {item.Key} = '{item.Value}'");
            }

            using (var instance = client.GetProxy<ISampleInstance>().Create()) ;

            // Multithreading test
            if (args.Length > 1 && Interlocked.Decrement(ref threadCount) == 0) allThreads.Set();
        }

        static ManualResetEvent allThreads = new ManualResetEvent(false);

        static int threadCount = 0;
    }
}
