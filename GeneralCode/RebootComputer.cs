using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace GeneralCode
{
    public class RebootComputer
    {
        public void reboot()
        {
            try
            {
                Process myProcess = new Process();
                myProcess.StartInfo.FileName = "cmd.exe";
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.RedirectStandardInput = true;
                myProcess.StartInfo.RedirectStandardOutput = true;
                myProcess.StartInfo.RedirectStandardError = true;
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.Start();
                //myProcess.StandardInput.WriteLine("shutdown -r -t 60\r\n");//60秒后重启
                myProcess.StandardInput.WriteLine("shutdown -r -t 1\r\n");//立即重启
            }
            catch (Exception exp)
            {
                System.Console.Out.WriteLine(exp.Message);
            }
        }


    }//end class
}//end namespace
