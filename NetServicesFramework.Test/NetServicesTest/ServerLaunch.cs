using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Security.Cryptography;
using NetServices.SampleService;
using System.Diagnostics;
using System.Net.Sockets;

namespace NetServices.Test
{
    public static partial class Program
    {
        // Create RSACryptoServiceProvider instance with private parameters
        static RSACryptoServiceProvider serverRsa => _serverRsa.Value;
        static readonly Lazy<RSACryptoServiceProvider> _serverRsa = new Lazy<RSACryptoServiceProvider>(() =>
        {
            //The way of storing the private key is determined by the developer
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                var rsa = new RSACryptoServiceProvider(); rsa.ImportCspBlob(Resources.ServerRsa); return rsa;
            }
            else return new RSACryptoServiceProvider(new CspParameters(1) { KeyContainerName = "SvcServer" });
        });

        // Client application uses server's public key 
        static byte[] serverPublic => serverRsa.ExportCspBlob(false);

        static void ServerLaunch(int parent)
        {
            if (parent == ~0)
            {
                var fileName = $"\"{typeof(Program).Assembly.Location}\"";

                var currentID = Process.GetCurrentProcess().Id;

                Process process; switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Unix:
                        process = Process.Start("/usr/bin/dotnet", $"exec {fileName} {currentID}");
                        break;
                    case PlatformID.Win32NT:
                        process = Process.Start(fileName, $"{currentID}");
                        break;
                    default:
                        throw new NotSupportedException($"Not tested platform '{Environment.OSVersion.Platform}'");
                }

                Exception connection = null;
                for (int n = 0; n < 3; n++) using (var tcp = new TcpClient()) try
                        {
                            Thread.Sleep(1000); tcp.Connect(serverEndPoint);

                            Console.WriteLine($"{process.MainModule.FileName} ID = {process.Id}");

                            return;
                        }
                        catch (Exception ex) { connection = ex; }

                throw connection;
            }

            // Initializing server
            var server = new SvcServer(serverRsa)
            {
                // List of assemblies with implementations to load.
                Assemblies = toLoad,
                // Users list. We add three users
                Users = new SvcUser[]
                {
                    // Anonimous user
                    new SvcUser(),
                    // User with "strong" authentication.
                    // The additional level of authentication is just a bonus.
                    // The main purpose is usage of “shared-secret” based encryption 
                    new SvcUser("User1", User1Public),
                    // User with "password" authentication
                    new SvcUser("User2", User2PasswordHash),
                },
            };

            // Testing users export/import
            server.ImportUsers(server.ExportUsers());

            // Starting server thread
            server.Start(serverEndPoint);

            Console.WriteLine($"Server ProcessID = {Process.GetCurrentProcess().Id}");

            if (parent != 0)
            {
                Process.GetProcessById(parent).WaitForExit();

                using (server) Environment.Exit(0);
            }
        }
    }
}
