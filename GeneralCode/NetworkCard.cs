using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace GeneralCode
{
    public class NetworkCard
    {
        //默认的网络连接名称
        private String network_card_connection_name = "tijian_wifi";

        public NetworkCard()
        {

        }
        public NetworkCard(String connection_name)
        {
            network_card_connection_name = connection_name;
        }

        public void disableNetCard()
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
                String command = String.Format("netsh interface set interface name=\"{0}\" admin=DISABLE\r\n",
                    network_card_connection_name);
                myProcess.StandardInput.WriteLine(command);
            }
            catch (Exception exp)
            {
                System.Console.Out.WriteLine(exp.Message);
            }
        }

        public void enableNetCard()
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
                String command = String.Format("netsh interface set interface name=\"{0}\" admin=ENABLE\r\n",
                    network_card_connection_name);
                myProcess.StandardInput.WriteLine(command);
            }
            catch (Exception exp)
            {
                System.Console.Out.WriteLine(exp.Message);
            }
        }

        public void resetNetCard()
        {
            disableNetCard();

            System.Threading.Thread.Sleep(10 * 1000);

            enableNetCard();
        }

    }//end class
}//end namespace
