using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace NetServices
{
    /// <summary>
    /// A place-holder 
    /// </summary>
    public sealed class SvcStream : IDisposable
    {
        internal SvcStream(TcpClient tcp, RC4 encrypt, RC4 decrypt, KeyValuePair<Type, Type>[] services = null) { }

        public void Dispose() { }
    }
}
