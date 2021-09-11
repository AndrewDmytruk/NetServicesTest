using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

namespace NetServices
{
    /// <summary>
    /// Contains server connection information
    /// </summary>
    public sealed class SvcServerRef 
    {
        /// <summary>
        /// Server IP address
        /// </summary>
        public readonly IPAddress Address;
        /// <summary>
        /// Server port number
        /// </summary>
        public IPEndPoint First => Port[0];
        /// <summary>
        /// Reserved 
        /// </summary>
        public IPEndPoint Second => Port[1];
        /// <summary>
        /// Server reference information
        /// </summary>
        /// <param name="rsaPulicKeyBlob">Server public key blob <see cref="RSACryptoServiceProvider.ExportCspBlob(bool)"/></param>
        /// <param name="server">Server IP address.
        /// <para>Use <see cref="Dns.GetHostAddresses(string)"/> if server is known by its name</para> 
        /// </param>
        /// <param name="firstPort">Connection port</param>
        /// <param name="secondPort">Reserved</param>
        public SvcServerRef(byte[] rsaPulicKeyBlob, IPAddress server, int firstPort, int secondPort = 0)
        {
            CspBlob = rsaPulicKeyBlob;

            Address = server;

            Port[0] = new IPEndPoint(Address, firstPort);

            Port[1] = new IPEndPoint(Address, secondPort);
        }

        internal readonly byte[] CspBlob;

        internal void SwapPorts() { var port = Port[0]; Port[0] = Port[1]; Port[1] = port; }

        readonly IPEndPoint[] Port = new IPEndPoint[2];
    }
}
