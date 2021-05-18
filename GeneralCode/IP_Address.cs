using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace GeneralCode
{
    public class IP_Address
    {
        //获取本机的主机名
        public String getLocalHostName()
        {
            String hostName = CSTR.trim(Dns.GetHostName());
            return hostName;
        }

        //获取本机的IP地址
        public List<String> getIP_List()
        {
            String hostName = getLocalHostName();
            //Dns.GetHostAddresses()会返回所有地址，包括IPv4和IPv6
            IPAddress[] addressList = Dns.GetHostAddresses(hostName);
            if (null == addressList) return null;

            List<String> list = new List<string>();
            foreach (IPAddress ip in addressList)
            {
                list.Add(CSTR.ObjectTrim(ip));
            }
            
            return list;
        }

        //获取本机的局域网IP地址
        public String getLocalIP()
        {
            //本网段的IP地址过滤
            String IPSection = "192.168.";//指定本地局域网的网段的格式

            String hostName = getLocalHostName();
            IPHostEntry localHost = Dns.GetHostEntry(hostName);
            if (null == localHost) return "";

            foreach (IPAddress ip in localHost.AddressList)
            {
                String strIP = CSTR.ObjectTrim(ip);
                if (strIP.IndexOf(IPSection) >= 0)
                {
                    return strIP;
                }
            }

            return "";
        }


    }//end class
}//end namespace
