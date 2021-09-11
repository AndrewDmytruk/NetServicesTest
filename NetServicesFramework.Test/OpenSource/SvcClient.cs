using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NetServices
{
    /// <summary>
    /// Implements the client side functionality
    /// </summary>
    public sealed partial class SvcClient : IDisposable
    {
        /// <summary>
        /// Creates connection for anonimous user
        /// </summary>
        /// <param name="serverName"></param>
        public SvcClient(string serverName) : this(serverName, null, (object)null) { }

        /// <summary>
        /// Creates connection with password authentication
        /// </summary>
        /// <param name="serverName">Server name in Servers dictionary</param>
        /// <param name="userName">User's account name known to server</param>
        /// <param name="password">Password hash</param>
        public SvcClient(string serverName, string userName, byte[] password)
            : this(serverName, userName, password as object)
        { }

        /// <summary>
        /// Creates connection with RSA authentication
        /// </summary>
        /// <param name="serverName">The server name in Servers dictionary</param>
        /// <param name="userName">Users's name known by server</param>
        /// <param name="userPrivate">Users's secret</param>
        public SvcClient(string serverName, string userName, RSACryptoServiceProvider userPrivate)
            : this(serverName, userName, userPrivate as object)
        { }

        /// <summary>
        /// An exception (if any) thrown during server connection process
        /// </summary>
        public Exception ConnectionException => ChanelOrException as Exception;

        /// <summary>
        /// Creates client-server TCP connection and implements the authentication and key agreement protocol. 
        /// The constructor code is included in open source for verification  
        /// </summary>
        /// <param name="serverName">The server name in Servers dictionary</param>
        /// <param name="userName">Users's name known by server</param>
        /// <param name="rsaOrPassword"></param>
        SvcClient(string serverName, string userName, object rsaOrPassword)
        {
            TcpClient tcp = null;

            using (var serverRSA = new RSACryptoServiceProvider()) try
                {
                    if (((rsaOrPassword as byte[])?.Length ?? 16) != 16)
                        throw new ArgumentException("Password hash length", "password");

                    if (!string.IsNullOrEmpty(userName) && rsaOrPassword == null
                        || string.IsNullOrEmpty(userName) && rsaOrPassword != null)
                        throw new ArgumentException("The proper constructor must be used for anonimous user");

                    var userID = string.IsNullOrEmpty(userName) ? new byte[16] : userName.HashBytes();

                    // Connecting to server
                    tcp = Connect(serverName, out SvcServerRef server);

                    serverRSA.ImportCspBlob(server.CspBlob);

                    var userRsa = rsaOrPassword as RSACryptoServiceProvider;

                    if (userRsa != null && serverRSA.KeySize != userRsa.KeySize)
                        throw new ArgumentException("Server and user KeySize must be the same", "userPrivate");

                    // In authentication process the password or PublicCspBlob hash
                    // is used as a key for RC4 algorithm.
                    // So the hash value itself in never transferred over network. 
                    RC4 password = rsaOrPassword == null ? null
                        : new RC4(Memory.Write(userID,
                        userRsa?.ExportCspBlob(false) ?? rsaOrPassword as byte[]).HashBytes(), false);

                    // Generate a random sequence
                    var message = new byte[serverRSA.KeySize / 8 - 48].Random();

                    // Add it's hash 
                    message = Memory.Write(message, message.HashBytes());

                    // Encrypt the resulting message with password/publicCspBlob hash
                    // Successful check of decrypted message proofs that both parties use the same hash value
                    message = password?.code(message) ?? message;

                    // The message for RSA encryption
                    message = Memory.Write(userID, message);

                    // See forward for usage
                    RC4 rc4 = new RC4(message.HashBytes()), encrypt, decrypt;

                    // Encrypt an send message to server
                    using (var writer = new BinaryWriter(tcp.GetStream(), Encoding.Default, true))
                    {
                        writer.Write(serverRSA.Encrypt(message, RSAEncryptionPadding.Pkcs1));

                        writer.BaseStream.Flush();
                    }

                    // Read and decrypt response. 
                    using (var reader = new BinaryReader(tcp.GetStream(), Encoding.Default, true))
                    {
                        // Input message is ecnrypted with hash of sent message
                        // So we proof that server had decrypted it
                        message = rc4.code(userRsa?.Decrypt(reader.ReadBytes(userRsa.KeySize / 8), RSAEncryptionPadding.Pkcs1)
                            ?? reader.ReadBytes(serverRSA.KeySize / 8));

                        // 
                        var keySequence = message.ToArray(0, message.Length - 16);

                        // The response message is encrypted with hash of clients request.
                        // So the server side proofs it's decryption
                        if (keySequence.HashGuid() != new Guid(message.ToArray(message.Length - 16)))
                            throw new AuthenticationException("Server authentication");

                        // Decrypt keySequence with password hash.
                        if (userRsa == null) password?.code(keySequence);

                        // Stream decryption (two keys are used as input and output are asynchronous)
                        decrypt = new RC4(keySequence.ToArray(0, keySequence.Length / 2).HashBytes());

                        // Stream encryption
                        encrypt = new RC4(keySequence.ToArray(keySequence.Length / 2).HashBytes());
                    }

                    // The implementation of SvcStream class is internal and not open source.
                    ChanelOrException = new SvcStream(tcp, encrypt, decrypt);
                }
                catch (Exception ex) { ChanelOrException = (tcp?.Connected ?? false) ? new AuthenticationException() : ex; }
        }

        static TcpClient Connect(string serverName, out SvcServerRef server)
        {
            lock (Servers) if (!Servers.TryGetValue(serverName, out server))
                    throw new ArgumentException($"'{serverName}' not found", "serverName");

            var tcp = new TcpClient(); try
            {
                tcp.Connect(server.First);
            }
            catch
            {
                if (server.Second != null)
                {
                    tcp.Connect(server.Second); server.SwapPorts();
                }
                else throw;
            }
            return tcp;
        }

        readonly object ChanelOrException;

        SvcStream Chanel => ChanelOrException as SvcStream;

        /// <summary>
        /// IDisposable intrface implementation
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, ~0) == 0)
                using (ChanelOrException as IDisposable)
                    GC.SuppressFinalize(this);
        }
        int disposed;

        /// <summary>
        /// Stores server parameters to enable the particular server referencing with nominal name
        /// </summary>
        public static ConcurrentDictionary<string, SvcServerRef> Servers = new ConcurrentDictionary<string, SvcServerRef>();
    }
}


