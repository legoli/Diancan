using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeneralCode
{
    public class Base64Convertor
    {
        public static String convertToBase64(byte[] buffer)
        {
            String strRet = "";
            if (null == buffer) return "";

            try
            {
                strRet = Convert.ToBase64String(buffer);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                strRet = "";
            }

            return strRet;
        }
        public static String convertToBase64(MemoryStream stream)
        {
            if (null == stream) return "";

            //如果用buffer=stream.GetBuffer()提示错误
            long bufferSize = stream.Length;
            byte[] buffer = new byte[bufferSize + 100];
            //从stream中读出字节并写入到buffer中
            stream.Read(buffer, 0, buffer.Length);

            return convertToBase64(buffer);
        }

        public static byte[] regainByteBuffer(String base64Cluster)
        {
            byte[] buffer = null;
            if (CSTR.isEmpty(base64Cluster)) return null;

            try
            {
                buffer = Convert.FromBase64String(base64Cluster);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                buffer = null;
            }

            return buffer;
        }
    }
}
