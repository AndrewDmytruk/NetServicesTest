using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace NetServices
{
    /// <summary>
    /// Defines usefull extentions
    /// </summary>
    public static class SvcTools
    {
        /// <summary>
        /// Gets all loaded types
        /// </summary>
        /// <param name="domain">Current domain</param>
        /// <param name="predicate">Selection predicate</param>
        /// <returns>IEnumerable<Type></returns>
        public static IEnumerable<Type> GetTypes(this AppDomain domain, Func<Type, bool> predicate = null)
             => domain.GetAssemblies().SelectMany(x => { try { return x.GetTypes(); } catch { return Type.EmptyTypes; } })
                 .Where(t => predicate?.Invoke(t) ?? true);

        /// <summary>
        /// Gets loaded type
        /// </summary>
        /// <param name="domain">Current domain</param>
        /// <param name="fullName">Type full name</param>
        /// <returns>Found type</returns>
        public static Type GetType(this AppDomain domain, string fullName)
            => domain.GetTypes().FirstOrDefault(t => t.FullName == fullName);

        /// <summary>
        /// Simplifies System.String.Split method usage
        /// </summary>
        /// <param name="str">Source string</param>
        /// <param name="count">Maximum number of resulting strings (0 - undefined)</param>
        /// <param name="chars">Split characters or null to split with wightspaces</param>
        /// <returns>Array of strings</returns>
        public static string[] Split(this string str, int count, string chars = null)
        {
            var set = chars?.ToCharArray() ?? wightspace;

            return count > 0 ? str.Split(set, count, StringSplitOptions.RemoveEmptyEntries)
                : str.Split(set, StringSplitOptions.RemoveEmptyEntries);
        }
        /// <summary>
        /// Wightspace chars: " \t\r\n"
        /// </summary>
        public static readonly char[] wightspace = " \t\r\n".ToCharArray();

        /// <summary>
        /// MD5.Create().ComputeHash(value)
        /// </summary>
        /// <param name="value">Source byte array</param>
        /// <returns>MD5 hash</returns>
        public static byte[] HashBytes(this byte[] value)
            => MD5.Create().ComputeHash(value);

        /// <summary>
        /// MD5(Encoding.UTF8)
        /// </summary>
        /// <param name="value">Source string</param>
        /// <returns>byte[16] MD5 hash</returns>
        public static byte[] HashBytes(this string value)
            => Encoding.UTF8.GetBytes(value).HashBytes();

        /// <summary>
        /// Computes MD5 hash and creates Guid struct
        /// </summary>
        /// <param name="value">Source bytes</param>
        /// <returns>Guid struct</returns>
        public static Guid HashGuid(this byte[] value)
            => new Guid(value.HashBytes());

        /// <summary>
        /// Computes MD5 hash and creates Guid struct
        /// </summary>
        /// <param name="value">Source string</param>
        /// <returns>Guid struct</returns>
        public static Guid HashGuid(this string value)
            => new Guid(value.HashBytes());

        /// <summary>
        /// Generates random bytes with System.Security.Cryptography.RandomNumberGenerator
        /// <para>
        /// Note: Unless we do not have control over RSA implementation
        /// there is no sence to do something more complex
        /// </para>
        /// </summary>
        /// <param name="array">The array to fill with cryptographically strong random bytes</param>
        /// <returns>Random bytes array</returns>
        public static byte[] Random(this byte[] array)
        {
            RandomNumberGenerator.Create().GetBytes(array); return array;
        }

        /// <summary>
        /// Produses an array from array segment
        /// </summary>
        /// <param name="bytes">Source array</param>
        /// <param name="offset">Segment offset (default==0)</param>
        /// <param name="length">Segment lengt (default==to-the-end)</param>
        /// <returns>Byte array</returns>
        public static byte[] ToArray(this byte[] bytes, int offset = 0, int length = -1)
        {
            if (length < 0) length = bytes.Length - offset;
            var array = new byte[length]; 
            Array.Copy(bytes, offset, array, 0, length);
            return array;
        }
        /// <summary>
        /// Concats items to IEnumerable (used with LINQ)
        /// </summary>
        /// <typeparam name="T">Item's Type</typeparam>
        /// <param name="iEnum">Sequence</param>
        /// <param name="args">Items to concat</param>
        /// <returns>IEnumerable</returns>
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> iEnum, params T[] args) => iEnum.Concat(args as IEnumerable<T>);

        /// <summary>
        /// Returns an IDisposable object to free delegate resources
        /// </summary>
        /// <param name="delegate">Remote method delegate object</param>
        /// <returns>An IDisposable object</returns>
        public static IDisposable AsIDisposabe(this Delegate @delegate) => @delegate.Target as IDisposable;
    }

    /// <summary>
    /// Usefull methods for binary messages parsing and ceating
    /// </summary>
    public static class Memory
    {
        /// <summary>
        /// Parse an input message and create a response 
        /// </summary>
        /// <param name="input">Input message</param>
        /// <param name="read">Parsing agorithm implementation</param>
        /// <returns>Created response</returns>
        public static byte[] Read(byte[] input, Func<BinaryReader, byte[]> read)
        {
            using (var br = new BinaryReader(new MemoryStream(input)))
                return read(br);
        }
 
        /// <summary>
        /// Create a binary massege
        /// </summary>
        /// <param name="write">Creation algorithm implementation</param>
        /// <returns>Created message</returns>
        public static byte[] Write(Action<BinaryWriter> write)
        {
            using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms))
            {
                write(bw); return ms.ToArray();
            }
        }
        
        /// <summary>
        /// Implements a simple concatanation algorithm
        /// </summary>
        /// <param name="values">Values to concatanate</param>
        /// <returns>Resulting message</returns>
        public static byte[] Write(params object[] values)
        {
            using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms))
            {
                foreach (var value in values)
                    bw.GetType().GetMethod("Write", new Type[] { value.GetType() })
                        .Invoke(bw, new object[] { value });
                return ms.ToArray();
            }
        }

        public static int Position(this BinaryReader br) => (int)br.BaseStream.Position;

        public static int Length(this BinaryReader br) => (int)br.BaseStream.Length;

        public static byte[] ReadAllBytes(this BinaryReader br) => br.ReadBytes(br.Length() - br.Position());
    }

    /// <summary>
    /// An implementation of RC4 crypto algorythm
    /// </summary>
    public class RC4
    {
        readonly byte[] S = new byte[256];

        byte si = 0, sj = 0;

        /// <summary>
        /// Creates an instance initialized with given key
        /// </summary>
        /// <param name="key">Key bytes</param>
        /// <param name="wipe">
        /// Wipe key array (defaul == true)
        /// <para>Recovery of source key from resulting internal state is a complex cryptanalytic task</para>
        /// </param>
        public RC4(byte[] key, bool wipe = true)
        {
            for (int i = 1; i < 256; i++) S[i] = (byte)i;

            for (int i = 0, j = 0; i < 256; i++)
            {
                j = (j + S[i] + key[i % key.Length]) % 256;
                byte t = S[i]; S[i] = S[j]; S[j] = t;
            }

            if (wipe) for (int i = 0; i < key.Length; key[i++] = 0) ;
        }

        /// <summary>
        /// In-place cryptotransform metod
        /// </summary>
        /// <param name="data">Source array</param>
        /// <param name="offset">Segment offset (default: 0)</param>
        /// <param name="length">Segment length (default: to end)</param>
        /// <returns>Source array</returns>
        public byte[] code(byte[] data, int offset = 0, int length = -1)
        {
            if (length < 0) length = data.Length - offset;

            for (int i = 0; i < length; i++)
            {
                sj += S[++si];
                byte t = S[si]; S[si] = S[sj]; S[sj] = t;
                data[offset + i] ^= S[(byte)(S[si] + S[sj])];
            }

            return data;
        }
    }
}
