using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NetServices
{
    /// <summary>
    /// Represents user information
    /// </summary>    
    [Serializable]
    public sealed class SvcUser
    {
        #region ==== Public members ====

        /// <summary>
        /// Creates a class instance
        /// </summary>
        /// <param name="userName">User's identification name</param>
        /// <param name="keyOrBlob">
        /// If keyOrBlob.Length == 16 then it is the password hash.
        /// Othetwise - rsaPublicCspBlob
        /// </param>
        /// <param name="roles">User roles (ToString values are used to create GenericPrincipal object)</param>
        public SvcUser(string userName, byte[] keyOrBlob, params object[] roles)
        {
            if (string.IsNullOrEmpty(userName) && keyOrBlob != null || !string.IsNullOrEmpty(userName) && keyOrBlob == null)
                throw new ArgumentException("Use anonymous constructor");

            UserName = userName;

            KeyOrBlob = keyOrBlob;

            Roles = roles.Select(r => $"{r}").ToArray();
        }

        /// <summary>
        /// Anonymous user constructor
        /// </summary>
        public SvcUser() : this("", null) { }
        
        /// <summary>
        /// User's identification name
        /// </summary>        
        public readonly string UserName;
        
        /// <summary>
        /// User identifier (see <see cref="SvcTools.HashGuid(string)"/> extention method)
        /// </summary>
        public Guid UserID => string.IsNullOrEmpty(UserName) ? Guid.Empty : UserName.HashGuid();

        /// <summary>
        /// Password hash or RSA public CspBlob 
        /// </summary>
        public readonly byte[] KeyOrBlob;

        /// <summary>
        /// User "roles"
        /// </summary>
        public readonly string[] Roles;

        /// <summary>
        /// The value is used in GenericPricipal constructor call and with access granting procedure (see documentation)
        /// </summary>
        public AuthType AuthType => KeyOrBlob == null ? AuthType.Anonimous
            : KeyOrBlob.Length == 16 ? AuthType.Password
            : AuthType.StrongAuth;

        /// <summary>
        /// The value is stored to Thread.CurrentPrincipal and may be examened by implementations
        /// </summary>
        public GenericPrincipal Principal => new GenericPrincipal(new GenericIdentity(UserName, $"{AuthType}"), Roles);

        #endregion

        #region ==== Internal ====

        internal RC4 RC4 => KeyOrBlob == null ? null
            : new RC4(Memory.Write(UserID.ToByteArray(), KeyOrBlob).HashBytes());

        #endregion
    }
}
