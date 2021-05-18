using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

namespace GeneralCode
{
    public class NetworkCheck
    {
        //引入Windows dll
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int connectionDescription, int reservedValue);

        public static bool isNetworkConnected()
        {
            bool state = false;

            int nDesc = 0;
            int nReservedValue = 0;

            try
            {
                state = InternetGetConnectedState(out nDesc, nReservedValue);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                state = false;
            }

            return state;
        }

        public static bool pingDatabaseServer()
        {
            bool bRet = false;

            String ip = ExamQueue.db_ip;

            try
            {
                Ping conn = new Ping();
                PingReply reply = conn.Send(ip);
                if (IPStatus.Success == reply.Status)
                {
                    bRet = true;

                    Console.WriteLine(reply.Address.ToString());
                    Console.WriteLine(reply.RoundtripTime.ToString());
                    Console.WriteLine(reply.Options.Ttl.ToString());
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                bRet = false;
            }

            return bRet;
        }

    }
}
