using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Security.Cryptography;
using NetServices.SampleService;
using System.Diagnostics;
using System.IO;

namespace NetServices.Test
{
    public static partial class Program
    {
        static readonly string[] toLoad = new string[] { "SampleService.dll" };

        static readonly IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 1305);

        /// <summary>
        /// "User1" makes use of top secury level
        /// </summary>
        static RSACryptoServiceProvider User1Rsa => _User1Rsa.Value;
        static readonly Lazy<RSACryptoServiceProvider> _User1Rsa = new Lazy<RSACryptoServiceProvider>(() =>
        {
            //The way of storing the private key is determined by the developer
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                var rsa = new RSACryptoServiceProvider(); rsa.ImportCspBlob(Resources.User1Rsa); return rsa;
            }
            else return new RSACryptoServiceProvider(new CspParameters(1) { KeyContainerName = "SvcUser1" });
        });

        /// <summary>
        /// The public part is stored on server
        /// <see cref="ServerLaunch(int)"/>
        /// </summary>
        static byte[] User1Public => User1Rsa.ExportCspBlob(false);

        /// <summary>
        /// "User2" uses password authentication. 
        /// Password hash (not password string!) is used by <see cref="SvcClient"/> and <see cref="SvcServer"/>.
        /// Password hash is never transmitted over network
        /// </summary>
        static readonly byte[] User2PasswordHash = "password".HashBytes();

        /// <summary>
        /// NetServicesTest
        /// </summary>
        /// <param name="args">Used for server launch</param>
        public static void Main(string[] args)
        {
            // Produces a lot of output
            //Trace.Listeners.Add(new ConsoleTraceListener());

            // Do not modify this line!
            if (args.Length == 1) ServerLaunch(int.Parse(args[0]));

            Console.WriteLine($"Client ProcessID = {Process.GetCurrentProcess().Id}");

            // Here we select the way of server hosting.
            // We may start server in current process (0)
            // for simultaneous debugging server and client code
            // or in a separate one (~0)
            ServerLaunch(0);

            // Servers are referenced with nominal names
            SvcClient.Servers["SvcServer"] = new SvcServerRef(serverPublic, serverEndPoint.Address, serverEndPoint.Port);

            // Anonimous user
            using (var client = new SvcClient("SvcServer"))
            {
                // Constructor does not throw exceptions
                // Examine ConnectionException property
                // to be sure the connection and authentication are successful.
                // Otherwise the exception is thrown at the first call.
                if (client.ConnectionException != null) throw client.ConnectionException;

                // Get proxy to remote implementation
                var proxy = client.GetProxy<ISampleSingleton>();

                // Sample property
                Console.WriteLine($"Anonymous: Time = {proxy.Time}");

                // Implementation returns delegate 
                Func<string> delegate1 = proxy[1], delegate2 = proxy[2];

                // We may explicitly dispose Delegate objects
                // Otherwise they are disposed with parent ISampleSingleton object
                using (delegate1.AsIDisposabe()) using (delegate2.AsIDisposabe())
                    Console.WriteLine($"delegate1()->'{delegate1()}' delegate2()->'{delegate2()}'");

                // An anonimous user has no permition to use ISampleGeneric
                try { client.GetProxy<ISampleGeneric<int>>().ToString(); }
                catch (Exception ex) { Console.WriteLine($"Anonymous: {ex.GetType().Name}"); }
            }

            // "User2" uses password authentication.
            using (var client = new SvcClient("SvcServer", "User2", User2PasswordHash))
            {
                if (client.ConnectionException != null) throw client.ConnectionException;

                // User2 with password authentication has permitions
                Console.WriteLine($"User2: {client.GetProxy<ISampleGeneric<int>>().ToString()}");
            }

            /// "User1" makes use of top secury level
            using (var client = new SvcClient("SvcServer", "User1", User1Rsa))
            {
                if (client.ConnectionException != null) { Console.WriteLine(client.ConnectionException); return; }

                // First let’s have a look at framework functionality
                ServicesTest(new object[] { client });

                // And now let’s try multithreading (over single connection)
                for (int threadno = 1; threadno <= 1000; threadno++)
                {
                    Interlocked.Increment(ref threadCount);

                    new Thread(ServicesTest).Start(new object[] { client, threadno });
                }
                allThreads.WaitOne();
            }
        }
    }
}

