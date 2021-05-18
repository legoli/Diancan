using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace GeneralCode
{
    public class Log
    {
        static private String prefix = "log";

        static public void log(String strInfo)
        {
            try
            {
                String filename = String.Format("{0}{1}.txt", prefix, DateTime.Now.ToString("yyyyMMdd"));
                StreamWriter sw = new StreamWriter(filename, true);//可追加
                sw.WriteLine(String.Format("{0} {1}",
                    System.DateTime.Now.ToString(),
                    strInfo));
                sw.Close();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }
    }
}
