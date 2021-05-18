using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GeneralCode
{
    /**
     * 压缩/解压缩字符串
     *      使用示例
            String str= "123";
            ZipString zip = new ZipString();
            MemoryStream stream = zip.GZipCompress(str);
            byte[] buffer = zip.getBufferFromMemoryStream(stream);
            int bLength = buffer.Length;
     * 
     * **/
    public class ZipString
    {
        //压缩时GZipStream构造函数第三个参数一定要设置为true，
        //并且一定要先调用Close再来复制，否则不能正常解压(测试发现调用Close后会追加几字节内容)
        public MemoryStream GZipCompress(String str)
        {
            str = CSTR.trim(str);
            if (str.Length <= 0) return null;

            //字符串转换为字节数组byte[]
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(str);
            MemoryStream streamRet = null;

            try
            {
                using (MemoryStream msTemp = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(msTemp, CompressionMode.Compress, true))
                    {
                        //压缩
                        gzip.Write(buffer, 0, buffer.Length);
                        gzip.Close();
                        //复制到返回的Stream
                        streamRet = new MemoryStream(msTemp.GetBuffer(), 0, (int)msTemp.Length);
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                streamRet = null;
            }

            return streamRet;
        }
        //从MemoryStream中获取byte[]字节buffer
        public byte[] getBufferFromMemoryStream(MemoryStream stream)
        {
            if (null == stream) return null;

            //如果用buffer=stream.GetBuffer()提示错误
            long bufferSize = stream.Length;
            byte[] buffer = new byte[bufferSize + 100];
            //从stream中读出字节并写入到buffer中
            stream.Read(buffer, 0, buffer.Length);

            return buffer;
        }

        //解压时直接使用Read方法读取内容，不能调用GZipStream实例的Length等属性，否则会出错
        public String GZipDecompress(MemoryStream stream)
        {
            String strRet = "";
            if (null == stream) return "";

            byte[] buffer = new byte[1024];

            try
            {
                using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress))
                {
                    using (MemoryStream msTemp = new MemoryStream())
                    {
                        int length = 0;
                        //分段读取,一次写入指定的buffer长度的解压后数据
                        while ((length = gzip.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            //把buffer中的字节写入到当前流
                            msTemp.Write(buffer, 0, buffer.Length);
                        }

                        strRet = System.Text.Encoding.UTF8.GetString(msTemp.ToArray());
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                strRet = "";
            }

            return strRet;
        }
        public String GZipDecompress(byte[] buffer)
        {
            if (null == buffer) return "";

            MemoryStream stream = new MemoryStream(buffer);
            return GZipDecompress(stream);
        }
    }//end class
}//end namespace
