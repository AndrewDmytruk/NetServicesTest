using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;
using System.Collections.Concurrent;

namespace NetServices
{
    /// <summary>
    /// Delegate type for handling server errors
    /// </summary>
    /// <param name="sender">Server instance</param>
    /// <param name="error">Exception instance</param>
    public delegate void ServerErrorHandler(SvcServer sender, Exception error);
    /// <summary>
    /// Delegate type for handling a not registered user connection events
    /// </summary>
    /// <param name="userID">The ID of connecting user</param>
    /// <returns>User information or null</returns>
    public delegate SvcUser UserResolveHandler(Guid userID);

    /// <summary>
    /// The server class
    /// </summary>
    public sealed partial class SvcServer : IDisposable
    {
        #region ==== Public ====

        /// <summary>
        /// Creates server instance
        /// </summary>
        /// <param name="rsaPrivate">Object must be initialized with RSA private key</param>
        public SvcServer(RSACryptoServiceProvider rsaPrivate)
        {
            RSA = rsaPrivate; OnDispose += RSA.Dispose;

        }
        /// <summary>
        /// List of assemlies containing implementations
        /// <para>File pathes are relative to NetServices.dll location</para>
        /// <para>If some assembly is not found the set-method throws FileNotFoundException </para>
        /// </summary>
        public string[] Assemblies
        {
            get { return _Assemblies; }
            set
            {
                var notfound = (_Assemblies = value).Select(x => Path.GetFullPath($"{codeBase}/{x}"))
                    .FirstOrDefault(path => !File.Exists(path));
                if (notfound != null) throw new FileNotFoundException("Assembly not found", notfound);
            }
        }
        string[] _Assemblies = new string[0];

        /// <summary>
        /// Currently is never raised
        /// </summary>
        public event ServerErrorHandler ServerError;

        /// <summary>
        /// List of regestered users (include anonimous user explicitly)
        /// </summary>
        public SvcUser[] Users
        {
            get { lock (UserLookup) return UserLookup.Values.ToArray(); }
            set { lock (UserLookup) { UserLookup.Clear(); AddUsers(value); } }
        }

        /// <summary>
        /// Adds users to internal dictionary. Users may be added dynamicaly
        /// </summary>
        /// <param name="users">Users list</param>
        public void AddUsers(params SvcUser[] users)
        {
            lock (UserLookup) foreach (var user in users) lock (Users) UserLookup[user.UserID] = user;
        }

        /// <summary>
        /// Dynemicaly remove users
        /// </summary>
        /// <param name="userID"></param>
        public void RemoveUsers(params Guid[] userID)
        {
            lock (UserLookup) foreach (var id in userID) UserLookup.Remove(id);
        }
        /// <summary>
        /// Invoked if connecting user ID is not found in internal dictionary
        /// </summary>
        public event UserResolveHandler UserResolve;

        /// <summary>
        /// Starts TcpListeners
        /// </summary>
        /// <param name="endPoints">Endpoints list</param>
        public void Start(params IPEndPoint[] endPoints)
        {
            if (endPoints.Length == 0) throw new ArgumentException("List cannot be empty", "endPoints");

            foreach (var ep in endPoints)
            {
                var listener = new TcpListener(ep); listener.Start();

                listener.BeginAcceptTcpClient(CallBack, listener);
            }
        }
        /// <summary>
        /// Creates encrypted container with user's information
        /// </summary>
        /// <returns>Encrypted array</returns>
        public byte[] ExportUsers()
        {
            var data = Memory.Write(bw =>
            {
                var key = new byte[16].Random();

                bw.Write(key); bw.Write(new RC4(key).code(Users.Serialize()));
            });

            var length = Math.Min(data.Length, RSA.KeySize / 8 - 16);

            var block = RSA.Encrypt(data.ToArray(0, length), RSAEncryptionPadding.Pkcs1);

            return Memory.Write(block, data.ToArray(length));
        }

        /// <summary>
        /// Imorts users list from encrypted container
        /// </summary>
        /// <param name="data">Encrypted data</param>
        public void ImportUsers(byte[] data)
        {
            var block = RSA.Decrypt(data.ToArray(0, RSA.KeySize / 8), RSAEncryptionPadding.Pkcs1);

            Users = Memory.Read(block, br =>
             {
                 var rc4 = new RC4(br.ReadBytes(16));                 

                 return rc4.code(Memory.Write(br.ReadAllBytes(), data.ToArray(RSA.KeySize / 8)));
             })
                .Deserialize<SvcUser[]>();
        }

        #endregion

        #region ==== Private ====

        readonly Dictionary<Guid, SvcUser> UserLookup = new Dictionary<Guid, SvcUser>();

        readonly RSACryptoServiceProvider RSA;

        Action OnDispose;

        void CallBack(IAsyncResult ar)
        {
            var listener = ar.AsyncState as TcpListener;

            // If anybody wonders AsyncWaitHandle is not disposed with EndAcceptTcpClient
            using (ar.AsyncWaitHandle) new Thread(AcceptTcpClient).Start(listener.EndAcceptTcpClient(ar));

            if (disposed == 0) listener.BeginAcceptTcpClient(CallBack, listener);
        }

        /// <summary>
        /// Examine this method for crypto protocol analysis
        /// </summary>
        /// <param name="obj">TcpClient object</param>
        void AcceptTcpClient(object obj)
        {
            lock (this)
                using (var tcpClient = obj as TcpClient) using (var started = new ManualResetEvent(false)) try
                    {
                        lock (this) OnDispose += tcpClient.Dispose;

                        // Withstanding DDOS attack
                        var handle = ThreadPool.RegisterWaitForSingleObject(started, (state, timeout) =>
                        {
#if !DEBUG
                            if (timeout) tcpClient.Close();
#endif
                        }
                        , null, 100, true);

                        // Reading and decrypting input message
                        byte[] block; using (var reader = new BinaryReader(tcpClient.GetStream(), Encoding.Default, true))
                            block = RSA.Decrypt(reader.ReadBytes(RSA.KeySize / 8), RSAEncryptionPadding.Pkcs1);

                        started.Set(); using (started) handle.Unregister(started);

                        // Used for encrypting the response message
                        RC4 authKey = new RC4(block.HashBytes()), userKey = null, encrypt = null, decrypt = null;

                        // User idenification
                        SvcUser user = null; Memory.Read(block, br =>
                           {
                               var userID = new Guid(br.ReadBytes(16));

                               lock (UserLookup) UserLookup.TryGetValue(userID, out user);

                               if (user == null) // For systems with many users
                                   if ((user = UserResolve?.Invoke(userID)) != null)
                                       lock (UserLookup) UserLookup[userID] = user;
                                   else throw new Exception();

                               block = br.ReadAllBytes(); // read remaining data

                               userKey = user.RC4; // key is hash of user's id and password-or-blob

                               block = userKey?.code(block) ?? block;

                               //User authentication
                               if (block.ToArray(0, block.Length - 16).HashGuid() != new Guid(block.ToArray(block.Length - 16)))
                                   throw new Exception();

                               return null;
                           });

                        // Set user information to be used by implementation classes
                        Thread.CurrentPrincipal = user.Principal;

                        using (var userRSA = user.AuthType == AuthType.StrongAuth ? new RSACryptoServiceProvider() : null)
                        {
                            userRSA?.ImportCspBlob(user.KeyOrBlob); // for users with strong data protection

                            // Session keys generation
                            var keySequence = new byte[RSA.KeySize / 8 - (userRSA == null ? 0 : 16) - 16].Random();

                            encrypt = new RC4(keySequence.ToArray(0, keySequence.Length / 2).HashBytes());

                            decrypt = new RC4(keySequence.ToArray(keySequence.Length / 2).HashBytes());

                            // Additional security for a case when server’s private key is compromised,
                            // but password hashes are still secret 
                            if (userRSA == null) userKey?.code(keySequence);

                            // Server authentication and stream keys protection
                            keySequence = authKey.code(Memory.Write(keySequence, keySequence.HashBytes()));

                            // Send response
                            using (var writer = new BinaryWriter(tcpClient.GetStream(), Encoding.Default, true))
                            {
                                // Even if ALL server data (private key and user’s hashes) is compromised
                                // in case "user.RSA != null" an attacker can not decrypt data
                                writer.Write(userRSA?.Encrypt(keySequence, RSAEncryptionPadding.Pkcs1) ?? keySequence);

                                writer.BaseStream.Flush();
                            }
                        }

                        Trace.WriteLine($"==== Name={user.UserName} connected", "Server");

                        // Start server
                        using (new SvcStream(tcpClient, encrypt, decrypt, CreateServices()))
                            lock (this) OnDispose -= tcpClient.Dispose;

                        Trace.WriteLine($"==== Name={user.UserName} disconnected", "Server");
                    }
                    catch (Exception ex) { Trace.WriteLine(ex, "Server"); }
        }

        /// <summary>
        /// Store implementation assamblies in this directory
        /// </summary>
        public static readonly string codeBase = Path.GetDirectoryName(typeof(SvcServer).Assembly.Location);

        /// <summary>
        /// Loads implementation assemblies and selects intefaces according to user permitions
        /// </summary>
        /// <returns>List of interface-implementation pairs</returns>
        KeyValuePair<Type, Type>[] CreateServices()
        {
            // User's permitions 
            IPrincipal principal = Thread.CurrentPrincipal;

            // User's authentication type 
            AuthType userAuthType = (AuthType)Enum.Parse(typeof(AuthType), principal.Identity.AuthenticationType);

            return (Assemblies ?? new string[0]).Select(path => Assembly.LoadFrom($"{codeBase}/{path}"))
            .Concat(typeof(SvcServer).Assembly)
            .SelectMany(x => x.GetTypes()).Where(t => t.IsSubclassOf(typeof(NetService)))
            .SelectMany(service => service.GetInterfaces().Where(x => typeof(INetService).IsAssignableFrom(x) && x != typeof(INetService))
            .Where(iservice => // access granting
            {
                // Interface attribute
                var attribute = iservice.GetCustomAttribute<SvcAccessAttribute>();

                var authType = attribute?.AuthType ?? AuthType.Any;

                var roles = attribute?.Roles; // roles==null if-and-only-if attribute is not defined

                return roles == null ? true
                    : authType != AuthType.Any && (userAuthType & authType) == 0 ? false
                    : roles.Length == 0 ? true
                    : roles.FirstOrDefault(role => principal.IsInRole(role)) != null;
            })
            .Select(iservice => new KeyValuePair<Type, Type>(iservice, service))).ToArray();
        }
        #endregion

        /// <summary>
        /// IDisposable interface implementation
        /// </summary>
        void IDisposable.Dispose()
        {
            if (Interlocked.Exchange(ref disposed, ~0) == 0) { OnDispose?.Invoke(); GC.SuppressFinalize(this); }
        }
        int disposed;
    }


}