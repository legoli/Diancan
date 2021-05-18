using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;//引入BackgroundWorker
using System.Threading;

namespace GeneralCode
{
    //调用者必须实现此接口
    public interface ILightThreadable
    {
        //线程执行的主体函数
        //获得运行参数,执行后返回需要ReportProcess的数据
        //返回数据为Dictionary<String,Object>
        Object worker_main(Object argument);

        //线程和窗口进程之间的交互平台(线程与进程安全空间)
        //运行参数为worker_main返回的数据,建议为 Dictionary<String,Object> 格式
        void worker_Report(Object dataObj);

        //线程结束后调用
        void worker_Completed(Object dataObj);
    }

    public class LightThread
    {
        //类变量定义
        private BackgroundWorker worker = null;//工作线程
        private ILightThreadable master = null;//使用LightThread的主进程
        public int intervalMillisecod = 1000;//循环停顿时间
        public bool IsBlockInProcessReport = true;//设定是否在ReportProcess时暂停线程执行
        public int n_lock = 0;//lock为0,线程循环可继续,否则等待
        public int loopCount = 0;

        //Caller设置的数据
        public object callerKey = null;//caller用于识别不同线程
        public object callerData = null;//caller保存的数据

        //lock使用的线程同步变量(多个LightTread在执行caller.worker_main()时线程同步
        private static readonly object single_thread_obj = new object();

        //构造函数
        public LightThread(ILightThreadable caller)
        {
            master = caller;
        }

        public LightThread(ILightThreadable caller,int intervalTime)
        {
            master = caller;
            if (intervalTime > 0) intervalMillisecod = intervalTime;
        }

        //启动线程
        public void run()
        {
            //如果woker不为空,说明正在运行
            if (null != worker) return;

            //实例化线程
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;//设置可以通告进度
            worker.WorkerSupportsCancellation = true;//设置可以用户干预取消

            //增加时间处理回调函数的挂接
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkCompleted);

            //构建线程参数
            Dictionary<String, Object> argument = new Dictionary<string, object>();
            argument.Add("worker", worker);
            argument.Add("LightThread", this);
            argument.Add("caller", master);

            //启动线程
            worker.RunWorkerAsync(argument);//指定实现了ILightThreadable的调用者作为参数
        }

        //线程循环函数
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //对线程执行体进行整体的TRY-CATCH封装
            try
            {
                //获取启动参数,获得master,worker,this等实例
                Dictionary<String, Object> argument = e.Argument as Dictionary<String, Object>;

                BackgroundWorker currWorker = argument["worker"] as BackgroundWorker;
                LightThread THIS = argument["LightThread"] as LightThread;
                ILightThreadable caller = argument["caller"] as ILightThreadable;

                while (true)
                {
                    loopCount++;//循环计数
                    if (loopCount > 60000) loopCount = 0;//reset

                    //执行线程主函数时线程同步
                    //每次循环调用一次caller的线程主体函数
                    //窗口主体函数运行后返回需要Report的数据做参数
                    object result = null;
                    result = caller.worker_main(argument);

                    //报告进度
                    THIS.n_lock = 1;//Lock
                    //System.Windows.Forms.MessageBox.Show("worker_DoWork:线程空间");//不会阻塞主界面
                    currWorker.ReportProgress(loopCount, result);
                    Thread.Sleep(100);//暂停100ms


                    //如果ReportProcess需要等待,则在此死循环
                    while (THIS.n_lock > 0)
                    {
                        Thread.Sleep(100);//停止100ms
                    }

                    //设置每次查询的间隔时间
                    Thread.Sleep(THIS.intervalMillisecod);

                    //判断是否需要终止执行
                    //判断是否窗口发出终止执行命令
                    if (currWorker.CancellationPending)
                    {
                        //线程返回值
                        //e.Result为Object类型,可以构建复杂参数作为返回值
                        //其值将在worker_RunWorkCompleted()
                        //中被访问
                        argument.Add("result", 1);
                        e.Result = argument;
                        return;//终止执行
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Dictionary<String, Object> argument = e.Argument as Dictionary<String, Object>;
                argument.Add("result", -1);//非常规结束,返回-1
                e.Result = argument;

                return;//终止执行
            }
        }

        //线程和窗口进程之间的交互平台(线程与进程安全空间)
        public void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //获取线程Report来的数据
            //int nCount = e.ProgressPercentage;//循环次数,对应worker.ReportProgress()第一个参数
            //BackgroundWorker currWorker = sender as BackgroundWorker;//sender为BackgroundWorker本身
            //获取主线程执行后返回的参数
            //Dictionary<String, Object> result = e.UserState as Dictionary<String, Object>;


            //调用窗口的接口处理函数,发送主线程的执行返回参数
            lock (single_thread_obj)
            {
                master.worker_Report(e.UserState);
            }
            //System.Windows.Forms.MessageBox.Show("worker_ProgressChanged:界面进程空间");//主界面将被阻塞

            //如果worker的工作方式为默认的在报告时阻塞的工作方式,则处理完毕后需要解锁
            unLock();
        }

        //线程结束后调用
        public void worker_RunWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //获取启动参数,获得master,worker,this等实例
            Dictionary<String, Object> argument = e.Result as Dictionary<String, Object>;

            BackgroundWorker currWorker = argument["worker"] as BackgroundWorker;
            LightThread THIS = argument["LightThread"] as LightThread;
            ILightThreadable caller = argument["caller"] as ILightThreadable;
            int nReturn = Convert.ToInt16(argument["result"].ToString());

            //调用chaunkou的对应函数,做一些资源释放工作
            //e.Result可以在线程中设置为Object,可以存放多种变量
            caller.worker_Completed(argument);
        }

        public BackgroundWorker getWorker()
        {
            return this.worker;
        }
        public bool IsLock()
        {
            return (n_lock > 0) ? true : false;
        }
        public void setLock()
        {
            n_lock = 1;
        }
        public void unLock()
        {
            n_lock = 0;
        }
    }//end class
}//end namespace
