using System;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;

namespace NetServices
{
    /// <summary>
    /// Base for services interfaces
    /// </summary>
    public interface INetService : IDisposable { }

    /// <summary>
    /// Base class for services implementations
    /// </summary>
    public abstract class NetService : INetService
    {
        /// <summary>
        /// IDisposable interface imlementation
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, ~0) == 0) { Dispose(true); GC.SuppressFinalize(this); }
        }
        int disposed;

        /// <summary>
        /// Override to do some disposing job
        /// </summary>
        /// <param name="disposing">If false - free only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)=> disposed = ~0;
    }

    /// <summary>
    /// Authentication types
    /// </summary>
    [Flags]
    public enum AuthType
    {
        /// <summary>
        /// Any type
        /// </summary>
        Any = 0,
        /// <summary>
        /// Anonimous
        /// </summary>
        Anonimous = 1,
        /// <summary>
        /// Password authentication
        /// </summary>
        Password = 2,
        /// <summary>
        /// Public key CspBlob
        /// </summary>
        StrongAuth = 4,
    }

    /// <summary>
    /// Used to specify access restrictions
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class SvcAccessAttribute : Attribute
    {
        /// <summary>
        /// Access is granted to users that "are in one of the roles"
        /// </summary>
        public readonly string[] Roles;

        /// <summary>
        /// The value is used in access granting algorithm
        /// </summary>
        public readonly AuthType AuthType;

        /// <summary>
        /// Attribute constructor
        /// </summary>
        /// <param name="authType">Combination of types. Set value AuthType.Any for no restrictions</param>
        /// <param name="roles">The ToString() values are used in IPrincipal.IsInRole call</param>
        public SvcAccessAttribute(AuthType authType, params object[] roles)
        {
            AuthType = authType;

            Roles = roles.Select(x => x.ToString()).ToArray();
        }
    }
}
