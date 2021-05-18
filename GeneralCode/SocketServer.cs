using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Threading;

namespace GeneralCode
{
    class SocketServer : ILightThreadable
    {
        private int port = 2000;
        private string host = "127.0.0.1";
        private Socket socketServer = null;//用于服务的监听Socket
        private Dictionary<Socket, LightThread> connectionMap = null;//记录连接的socket和对应的线程

        //构造函数
        public SocketServer(String ip)
        {
            host = ip;
        }
        public SocketServer(String ip,int ipPort)
        {
            host = ip;
            port = ipPort;
        }

        public void Listen()
        {
            //变量初始化
            connectionMap = new Dictionary<Socket, LightThread>();

            ///创建终结点（EndPoint）
            IPAddress ip = IPAddress.Parse(host);//把ip地址字符串转换为IPAddress类型的实例
            IPEndPoint ipe = new IPEndPoint(ip, port);//用指定的端口和ip初始化IPEndPoint类的新实例

            ///创建socket并开始监听
            socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//创建一个socket对像，如果用udp协议，则要用SocketType.Dgram类型的套接字
            socketServer.Bind(ipe);//绑定EndPoint对像（2000端口和ip地址）
            socketServer.Listen(0);//开始监听
            Console.WriteLine("等待客户端连接");

            while (true)
            {
                //等待,有客户链接即建立新到soket
                Socket newSocket = socketServer.Accept();//为新建连接创建新的socket
                Console.WriteLine("建立连接");

                //为新的Sokcet创建一个LightThread线程
                LightThread newThread = new LightThread(this);
                newThread.intervalMillisecod = 1000;//执行间隔(ms)
                newThread.IsBlockInProcessReport = true;//阻塞ReportProcess
                newThread.callerKey = newSocket;//设置识别不同线程

                //把链接写入到map
                connectionMap.Add(newSocket, newThread);

                //执行线程
                newThread.run();

                //继续监听下一个链接
            }
        }

        #region 实现ILightThread接口的示例代码

        //namespace引入
        //using GeneralCode;//引入LightThread(自定义库)
        //using System.ComponentModel;//引入BackgroundWorker
        //using System.Threading;

        //线程执行的主体函数(线程空间,不可以直接访问调用者的类变量等,如需要在e.argument中有调用者指针)
        public Object worker_main(Object e)
        {
            //获取启动参数,获得master,worker,this等实例
            Dictionary<String, Object> argument = e as Dictionary<String, Object>;
            BackgroundWorker currWorker = argument["worker"] as BackgroundWorker;
            LightThread lightThread = argument["LightThread"] as LightThread;
            ILightThreadable caller = argument["caller"] as ILightThreadable;//调用者指针
            //MyClass me = argument["master"] as MyClass;//调用者指针,转换后可访问本进程变量

            //通过LightThread.callerKey获取Socket
            Socket socket = lightThread.callerKey as Socket;

            //处理需要时间和繁杂的任务,不会阻塞前台窗口程序
            //此处为线程空间,尽量避免直接修改其他进程空间的数据
            //do some background work
            //1.接收信息
            String strRecv = "";
            while (strRecv.Length <= 0)
            {
                Thread.Sleep(100);
                strRecv = receive(socket);
            }

            //构建Report参数
            Dictionary<String, Object> map = new Dictionary<string, Object>();
            map.Add("worker", currWorker);
            map.Add("LightThread", lightThread);
            map.Add("caller", caller);

            map.Add("socket", socket);//返回给Report的数据
            map.Add("str", strRecv);

            return map;//提供给LightThread主线程发送Report
        }
        //********************************************************************************************************************

        //线程和窗口进程之间的交互平台(线程与进程安全空间)
        public void worker_Report(Object dataObj)
        {
            //获取线程Report来的数据
            Dictionary<String, Object> argument = dataObj as Dictionary<String, Object>;
            //ILightThreadable me = argument["caller"] as ILightThreadable;//调用者指针
            BackgroundWorker currWorker = argument["worker"] as BackgroundWorker;
            LightThread lightThread = argument["LightThread"] as LightThread;
            ILightThreadable caller = argument["caller"] as ILightThreadable;//调用者指针
            //数据
            Socket socket = argument["socket"] as Socket;//特别传送数据
            String strInfo = argument["str"] as String;//特别传送数据

            //调用窗口的其他处理事项
            //对调用窗口的变量和方法的访问和窗口的其他函数一样(线程安全)
            //分析字符串
            CXml xmldoc = new CXml();
            Dictionary<String, String> map = xmldoc.parseStringXml(strInfo);
            if (null == map) return;
            if (map.Count <= 0) return;

            String strSentence = String.Format("请 {0} 到{1}检查", map["name"], map["room"]);
            //Call TTS to Speak
        }

        //线程结束后调用
        public void worker_Completed(Object dataObj)
        {
            //获取线程Report来的数据
            Dictionary<String, Object> argument = dataObj as Dictionary<String, Object>;
            //ILightThreadable me = argument["caller"] as ILightThreadable;//调用者指针
            BackgroundWorker currWorker = argument["worker"] as BackgroundWorker;
            LightThread lightThread = argument["LightThread"] as LightThread;
            ILightThreadable caller = argument["caller"] as ILightThreadable;//调用者指针
            //结果
            int nReturn = Convert.ToInt16(argument["result"].ToString());

            //做一些资源释放工作
            //e.Result可以在线程中设置为Object,可以存放多种变量
            //MessageBox.Show(String.Format("thread 返回:{0},执行次数:{1}", nReturn, lightThread.loopCount));
            return;
        }

        #endregion //实现ILightThread接口的示例代码

        public void start()
        {
            ///创建终结点（EndPoint）
            IPAddress ip = IPAddress.Parse(host);//把ip地址字符串转换为IPAddress类型的实例
            IPEndPoint ipe = new IPEndPoint(ip, port);//用指定的端口和ip初始化IPEndPoint类的新实例

            ///创建socket并开始监听
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//创建一个socket对像，如果用udp协议，则要用SocketType.Dgram类型的套接字
            s.Bind(ipe);//绑定EndPoint对像（2000端口和ip地址）
            s.Listen(0);//开始监听
            Console.WriteLine("等待客户端连接");

            ///接受到client连接，为此连接建立新的socket，并接受信息
            while (true)
            {
                Socket temp = s.Accept();//为新建连接创建新的socket
                Console.WriteLine("建立连接");
                string recvStr = "";
                byte[] recvBytes = new byte[1024];
                int bytes;
                bytes = temp.Receive(recvBytes, recvBytes.Length, 0);//从客户端接受信息
                //recvStr += Encoding.ASCII.GetString(recvBytes, 0, bytes);
                recvStr += Encoding.UTF8.GetString(recvBytes, 0, bytes);

                ///给client端返回信息
                Console.WriteLine("server get message:{0}", recvStr);//把客户端传来的信息显示出来
                string sendStr = "ok!Client send message successful!行";
                //byte[] bs = Encoding.ASCII.GetBytes(sendStr);
                byte[] bs = Encoding.UTF8.GetBytes(sendStr);
                temp.Send(bs, bs.Length, 0);//返回信息给客户端
                temp.Close();
            }
        }

        public void send(Socket socket, String strInfo)
        {
        }
        public String receive(Socket socket)
        {
            string recvStr = "";
            byte[] recvBytes = new byte[1024];//定义接收缓冲区的大小
            int bytes;//接收到的字节数
            bytes = socket.Receive(recvBytes, recvBytes.Length, 0);//阻塞接收中...
            //recvStr += Encoding.ASCII.GetString(recvBytes, 0, bytes);//编码转换
            recvStr += Encoding.UTF8.GetString(recvBytes, 0, bytes);//编码转换

            return recvStr;
        }
    }//end class
} //end namespace
