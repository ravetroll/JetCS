using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Helpers
{
    public static class CompressionTools
    {
        public static byte[] CompressData(string plainText)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(plainText);

            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(buffer, 0, buffer.Length);
                }
                return ms.ToArray();
            }
        }

        public static string DecompressData(byte[] compressedData, int length)
        {
            using (MemoryStream ms = new MemoryStream(compressedData, 0, length))
            using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
            using (StreamReader sr = new StreamReader(zip))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
