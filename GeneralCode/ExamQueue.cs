using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.ComponentModel;//引入BackgroundWorker
using System.Threading;

namespace GeneralCode
{
    public class ExamQueue
    {
        //常量定义
        //public const String db_ip = "rm-bp1o78n1h04dl3t44.mysql.rds.aliyuncs.com", db_user = "r56448z34r", db_password = "liyulong_2016", db_database = "r56448z34r";
        //public const String db_ip = "192.168.3.202", db_user = "root", db_password = "root", db_database = "test_tj";
        //public const String db_ip = "localhost", db_user = "root", db_password = "root", db_database = "test_tj";
        //public const String db_ip = "192.168.1.209", db_user = "root", db_password = "root", db_database = "test_tj";
        //public const String db_ip = "192.168.60.5", db_user = "root", db_password = "root", db_database = "test";
        public const String db_ip = "192.168.3.211", db_user = "root", db_password = "root", db_database = "test_tj";

        #region 类变量定义
        private ConfigParam CONFIG = new ConfigParam();//xml配置文件参数获取
        private BackgroundWorker workerQueryNewArrival = null;//查询新的到检登记线程
        public String siteID = "";//进程ID
        public String roomID = "";//ModalityID
        public String roomName = "";//检查室名称
        private MysqlOperator jiaohaoDB = null;
        private SqlServerOperator usDB = null;
        public String strTest = "";//测试线程使用
        public int workerQueryNewArrival_loopTimes = 0;//到检登记查询线程的循环计数
        private IWorkerCallback caller = null;//窗口启动线程时传入的参数,是窗口本身,实现了IWorkerCallback接口
        public DataTable tblDoing = null;//最近一次的正在检查table
        public DataTable tblPending = null;//最近一次的等待检查table
        #endregion
        //Get-Set of MaxExamID
        public String MaxExamID
        {
            get
            {
                //int i = 10;
                //if (i > 0) return "1000";
                String sql = "select MaxExamID from JiaohaoParam";
                DataTable tbl = getDB().query(sql);
                if (null != tbl)
                {
                    if (tbl.Rows.Count > 0)
                    {
                        return tbl.Rows[0]["MaxExamID"].ToString();
                    }
                }

                return "";
            }
            set
            {
                String sql = String.Format("update JiaohaoParam set MaxExamID={0}", value);
                getDB().update(sql);
            }
        }
        //构造函数
        public ExamQueue()
        {
            if (null == CONFIG)
            {
                CONFIG = new ConfigParam();
            }

            siteID = CONFIG.system_param["site_id"];
            roomID = CONFIG.system_param["room_id"];
            roomName = CONFIG.system_param["room_name"];
        }


        #region Worker线程
        //初始化并启动线程
        public void worker_QueryNewArrival_Start(object wnd)//窗口启动线程时传入的参数,是窗口本身,实现了IWorkerCallback接口
        {
            //保存调用窗口的句柄
            caller = wnd as IWorkerCallback;//用于调此窗口的回调函数返回查询数据

            //如果woker不为空,说明正在运行
            if (null != workerQueryNewArrival) return;

            //实例化线程
            workerQueryNewArrival = new BackgroundWorker();
            workerQueryNewArrival.WorkerReportsProgress = true;//设置可以通告进度
            workerQueryNewArrival.WorkerSupportsCancellation = true;//设置可以用户干预取消

            //增加时间处理回调函数的挂接
            workerQueryNewArrival.DoWork += new DoWorkEventHandler(worker_DoWork);
            workerQueryNewArrival.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            workerQueryNewArrival.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkCompleted);

            //启动线程
            workerQueryNewArrival.RunWorkerAsync(wnd);//指定具体变量作为参数
        }

        //线程主体函数
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ////////////////可选参数设置////////////////////////
            int main_loop_interval = 3*1000;//设置线程主循环等待时间(ms)
            ////////////////////////////////////////

            //对线程执行体进行整体的TRY-CATCH封装
            try
            {
                while (true)
                {
                    //循环计数(使用Int64)
                    workerQueryNewArrival_loopTimes++;
                    if (workerQueryNewArrival_loopTimes > 65000) workerQueryNewArrival_loopTimes = 0;

                    //封装主线程,在发生错误时有机会恢复正常
                    try
                    {
                        //调用线程主体方法
                        main_thread(workerQueryNewArrival, e);
                    }
                    catch (Exception exp)
                    {
                        Console.WriteLine(exp.Message);
                    }

                    //执行一次查询后进入休眠
                    Thread.Sleep(main_loop_interval);//设置每次查询的间隔时间

                    //判断是否窗口发出终止执行命令
                    if (workerQueryNewArrival.CancellationPending)
                    {
                        //线程返回值
                        e.Result = 1;
                        strTest = "终止执行";
                        return;//终止执行
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
        #region    ***********<<<<<Main Thread主线程_begin>>>>>*********************************************************
        private int main_thread(BackgroundWorker worker, DoWorkEventArgs e)
        {
            strTest = "运行中...";

            int newExamCount = 0;

            //查询最新的当前在检人员信息和待检信息
            //DataTable DoingTable = Exam_Get_Doing();
            //DataTable PendingTable = Exam_Get_Pending();

            //重新刷新DataCache
            DatabaseCache.DB_Reflash_JiaohaoTable_And_JiaohaoExamInfo();
            DatabaseCache.DB_Reflesh_JiaohaoRoomInfo();

            //查询最新的当前在检人员信息和待检信息
            DataTable DoingTable = DatabaseCache.makeJiaoTable(DatabaseCache.Exam_Get_Doing());
            DataTable PendingTable = null;

            //如果为[319][322][320]室则直接显示所有未完成检查
            if ("[319][322][320][406]".IndexOf(CONFIG.system_param["room_id"]) >= 0)
            {
                PendingTable = DatabaseCache.makeJiaoTable(DatabaseCache.Exam_Get_Unfinished_Exams());
            }
            else
            {
                PendingTable = DatabaseCache.makeJiaoTable(DatabaseCache.Exam_Get_Pending());
            }

            //RoomInfo变化信息

            //构建Report参数
            Dictionary<String, DataTable> map = new Dictionary<string, DataTable>();
            map.Add("DOING", DoingTable);
            map.Add("PENDING", PendingTable);

            //报告进度
            worker.ReportProgress(newExamCount, map);

            return newExamCount;
        }
        #endregion ***********<<<<<Main Thread主线程_end>>>>>***********************************************************

        //线程和窗口进程之间的交互平台(线程与进程安全空间)
        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //获取线程Report来的数据
            int n = e.ProgressPercentage;
            Dictionary<String, DataTable> map = (Dictionary<String, DataTable>)e.UserState;

            //调用窗口的回调函数,向窗口返回最新的叫号数据
            caller.WorkerCallback(map);

        }

        //线程结束后调用
        private void worker_RunWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //lblInfo2.Text = String.Format("线程结束,共执行{0}次", e.Result);
            strTest = "已结束";
            if (null != workerQueryNewArrival)
            {
                workerQueryNewArrival.Dispose();
                workerQueryNewArrival = null;
            }
        }

        //取消线程的执行
        public void Worker_Stop()
        {
            //取消线程的执行
            if (null != workerQueryNewArrival) workerQueryNewArrival.CancelAsync();
        }

        #endregion Worker线程

        #region 查询RIS数据库操作:发现新的到检登记,并记录到叫号表中
        //叫号数据库(test)
        public MysqlOperator getDB()
        {
            if (null == jiaohaoDB) //------------------------>叫号数据库连接
            {
                jiaohaoDB = new MysqlOperator(db_ip, db_user, db_password, db_database);
            }

            return jiaohaoDB;
        }

        //RIS数据(gecris)
        public SqlServerOperator getTijianDB() //------------------------>体检数据库连接
        {
            if (null == usDB)
            {
                usDB = new SqlServerOperator();
            }

            return usDB;
        }

        //每次线程主循环一次即重新链接数据库
        //ExamQueue类是非静态类,不会影响其他进程
        private void DB_ClearAll()
        {
            if (null != usDB)
            {
                usDB.reset();
                usDB = null;
            }
            if (null != jiaohaoDB)
            {
                jiaohaoDB.reset();
                jiaohaoDB = null;
            }
        }


        #endregion 查询新到的到检检查
        //////////////////////////////////////////////////////////////////////////////////////

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        #region 体检系统激活检查并写入到叫号表

        public void tj_NewExam_setVIP(String DJLSH)
        {
            if (CSTR.isEmpty(DJLSH)) return;

            String sql = String.Format("Update JiaohaoTable Set IsVIP=1,PreExamFrom='过号重排' Where DJLSH='{0}'", DJLSH);
            getDB().update(sql);
        }

        //个检登记专用,天方达登记调用登记台的btnMatchRingNo_Click(object sender, EventArgs e)来完成登记
        public int tj_SpecialExamActivate(String SpecialExamName, String SpecialExamItemList, String patientName, String patientGender, bool isVIP, String Rfid, String RfidName)
        {
            //////////////可选参数////////////////////////////////////////
            //参数:(String Rfid-卡号,String RfidName-手环编号)
            //登记员选择体检人员并匹配手环后, 调用本方法
            //把指定的体检人员检查push入叫号队列
            //SpecialExamName --指定特殊体检名称,如'车检'
            //SpecialExamItemList--格式 1,2,3,5  代表JiaohaoSpecialExam.id(选择的项目)
            //////////////////////////////////////////////////////////////
            
            String sql = String.Format(@"select
CONCAT('{0}',DATE_FORMAT(NOW(),'%H%i')) as TJBH,
'{3}' as XM,
'{4}' as XB,
'' as SFZH,
'' as p_PHONE,
'' as d_PHONE,
CONCAT('{0}',DATE_FORMAT(NOW(),'%H%i')) as DJLSH,
'0000' as DWBH,
'非天方达' as DWMC,
now() as TJRQ,
now() as jlb_JCRQ,
TJLB,
TJLBMC,
'01' as RYLB,
'SA' as RYLBMC,
CONCAT(CONCAT('{0}',DATE_FORMAT(NOW(),'%H%i')),ProcedureStepID) XH,
ExamType,
OrderProcedureName as ExamTypeMC,
CONCAT(TJLB,ProcedureStepID) as TJXMBH,
ProcedureStepName as TJXMMC,
ExamTip as TSXX,
RoomID as CheckRoom,
'0' as ISOVER
from JiaohaoSpecialExam
where -- TJLBMC='{1}' and
 id in ({2})", RfidName, SpecialExamName, SpecialExamItemList,patientName,patientGender);


            DataTable tblNewArrival = getDB().query(sql);
            if (CSTR.IsTableEmpty(tblNewArrival)) return 0;
            int nCount = tblNewArrival.Rows.Count;//设置返回值

            //调用Rule登记
            RuleForRegister Rule = new RuleForRegister(tblNewArrival, Rfid, RfidName);
            //写入数据库,获取新登记的待检流水号
            String new_DJLSH = Rule.register();
            //如果返回为null,表示登记失败
            if (CSTR.isEmpty(new_DJLSH))
            {
                return -1;
            }

            //设置VIP标志
            if (isVIP)
            {
                //如果是VIP,则设置检查标志
                tj_NewExam_setVIP(new_DJLSH);
            }

            //设置一个活动的检查
            TJ_Queue_Arrange_Search_And_Activate_One_Item_By_DJLSH(new_DJLSH);

            return nCount;
        }

        //JiaohaoRoomInfo房间信息查询
        private DataTable RoomInfo_tbl = null;
        private DataTable RoomInfo_get_JiaohaoRoomInfo_tbl()
        {
            if (CSTR.IsTableEmpty(RoomInfo_tbl))
            {
                String sql = "Select * from JiaohaoRoomInfo";
                RoomInfo_tbl = getDB().query(sql);
            }

            return RoomInfo_tbl;
        }
        public void RoomInfo_Reset_JiaohaoRoomInfo_tbl()
        {
            RoomInfo_tbl = null;
        }
        private DataRow RoomInfo_get_Room_row(String roomID)
        {
            DataTable tbl = RoomInfo_get_JiaohaoRoomInfo_tbl();
            if (CSTR.IsTableEmpty(tbl)) return null;

            foreach (DataRow row in tbl.Rows)
            {
                if (CSTR.ObjectTrim(row["RoomID"]).Equals(roomID))
                {
                    return row;//找到匹配的row直接返回
                }
            }

            return null;
        }
        public bool RoomInfo_is_need_queue(String roomID)
        {
            DataRow row = RoomInfo_get_Room_row(roomID);
            if (null == row) return false;

            String strValue = CSTR.ObjectTrim(row["IsNeedQueue"]);

            return (strValue.Equals("1")) ? true : false;
        }
        public bool RoomInfo_is_need_voice(String roomID)
        {
            DataRow row = RoomInfo_get_Room_row(roomID);
            if (null == row) return false;

            String strValue = CSTR.ObjectTrim(row["IsNeedVoice"]);

            return (strValue.Equals("1")) ? true : false;
        }
        public bool RoomInfo_is_active(String roomID)
        {
            DataRow row = RoomInfo_get_Room_row(roomID);
            if (null == row) return false;

            String strValue = CSTR.ObjectTrim(row["RoomState"]);

            return (strValue.Equals("active")) ? true : false;
        }
        public void RoomInfo_Set_active(String roomID,String roomState)
        {
            String sql = String.Format("Update JiaohaoRoomInfo Set RoomState='{0}' " +
                "Where RoomID='{1}'", roomState, roomID);

            getDB().update(sql);
        }
        #endregion 体检系统激活检查并写入到叫号表
        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

        #region 数据库的叫号操作

        // 获取本房间的当前正在检查的信息(当前的,不呼叫下一个)(一个房间<=1条记录)
        public DataTable Exam_Get_Doing()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where RoomID='{0}' " +
                "And QueueActive=1 And status=1 And IsChecking=1 And IsOver=0 order by QueueID DESC", strRoomID);
            return getDB().query(sql);
        }

        // 获取本房间的当前正在检查的信息(当前的,不呼叫,用于显示正在检查)(对于234室,返回结果可能为多条)
        public DataTable Exam_Get_Doing_To_Show()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            //IsSaid初始为0,在显示完信息后设置为1
            String sql = String.Format("select * from JiaohaoTable where RoomID='{0}' " +
                "and status=1 and IsChecking=1 order by IsSaid", strRoomID);
            return getDB().query(sql);
        }

        // 获取本房间的当前正在检查的信息(当前的,不呼叫下一个)
        public int Exam_Get_Doing_To_Show_Update(String strIndexID)
        {
            if (CSTR.trim(strIndexID).Length <= 0) return 0;

            //IsSaid初始为0,在显示完信息后设置为1
            String sql = String.Format("update JiaohaoTable set IsSaid=1 where IndexID={0}", strIndexID);
            return getDB().update(sql);
        }

        /// 获取指定房间的等待叫号的信息
        public DataTable Exam_Get_Pending()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where QueueActive=1 and RoomID='{0}' " +
                "and status=0 and IsChecking=0 order by QueueID", strRoomID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }
        /// 获取指定房间的等待叫号的排在第一个的信息
        public DataTable Exam_Get_Pending_First_One()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where QueueActive=1 and RoomID='{0}' " +
                "and status=0 and IsChecking=0 order by QueueID " +
                "Limit 1", strRoomID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }
        public DataTable Exam_Get_This_Room_All_Un_Finished_Exam()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where RoomID='{0}' and IsOver=0 order by QueueID", strRoomID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }
        public DataTable Exam_Get_Specified_Room_All_Un_Finished_Exam(String strRoomID)
        {
            //取得本进程对应的ModalityID(room_id)
            String sql = String.Format("select * from JiaohaoTable where RoomID='{0}' and IsOver=0 order by QueueID", strRoomID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }
        /// 获取指定IndexID的一条信息
        public DataTable Exam_Get_Single_Exam_By_IndexID(String strIndexID)
        {
            String sql = String.Format("select * from JiaohaoTable where IndexID={0}", strIndexID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }

        /// 获取本房间的相同DJLSH的信息(不管状态)
        public DataTable Exam_Get_SameRoom_SameDJLSH(String strDJLSH)
        {
            strDJLSH = CSTR.trim(strDJLSH);
            if (strDJLSH.Length <= 0) return null;

            //取得本进程对应的room_id
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("Select * from JiaohaoTable Where RoomID like '%{0}%' " +
                "AND DJLSH='{1}'",
                strRoomID,
                strDJLSH);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }

        /// 获取相同体检查(DJLSH)的推荐的下一个房间号
        public DataTable Exam_Get_NextRecommendedRoom_SameDJLSH(String strDJLSH)
        {
            strDJLSH = CSTR.trim(strDJLSH);
            if (strDJLSH.Length <= 0) return null;

            String sql = String.Format("Select * from JiaohaoTable Where QueueActive=1 and status=0 And IsChecking=0 And IsOver=0 " +
                "AND DJLSH='{0}' limit 1", strDJLSH);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }

        /// 设置检查完成标志(IsOver)->本房间的相同DJLSH的信息(不管状态)
        public int Exam_SetIsOver_SameRoom_SameDJLSH(String strDJLSH)
        {
            strDJLSH = CSTR.trim(strDJLSH);
            if (strDJLSH.Length <= 0) return -1;

            //取得本进程对应的room_id
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=1,IsChecking=0,IsOver=1 Where " +
                "RoomID='{0}' " +
                "AND DJLSH='{1}'",
                strRoomID,
                strDJLSH);
            Console.WriteLine(sql);
            return getDB().update(sql);
        }

        /// 设置检查完成标志(IsOver)->本房间的相同Rfid的信息(不管状态)
        /// 使用环境,配合嘀卡处理函数TJ_Queue_Arrange_Time_Set_CardReadTime_By_IndexID
        public int Exam_SetIsOver_SameRoom_While_Card_Read_By_RFID(String strRfid)
        {
            strRfid = CSTR.trim(strRfid);
            if (strRfid.Length <= 0) return -1;

            //取得本进程对应的room_id,必须指定,不然会更新多条记录.
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=1,IsChecking=0,IsOver=1 " +
                "Where RoomID='{0}' " +
                "AND Rfid='{1}'",
                strRoomID,
                strRfid);
            Console.WriteLine(sql);
            return getDB().update(sql);
        }

        /// 获取本房间的相同RFID的信息(不管状态)
        public DataTable Exam_Get_All_Exam_Ingone_Status_SameRoom_SameRFID(String strRFID)
        {
            strRFID = CSTR.trim(strRFID);
            if (strRFID.Length <= 0) return null;

            //取得本进程对应的room_id
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("Select * from JiaohaoTable Where RoomID='{0}' AND Rfid='{1}'", strRoomID, strRFID);
            Console.WriteLine(sql);
            return getDB().query(sql);
        }

        /// 通过RfidNo来获取手环号RingNo
        public String Exam_Get_RingNo_By_RfidNo(String strRFID)
        {
            strRFID = CSTR.trim(strRFID);
            if (strRFID.Length <= 0) return null;

            String sql = String.Format("Select * from JiaohaoRfid Where Rfid='{0}' ", strRFID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return "";

            return CSTR.ObjectTrim(tbl.Rows[0]["CardNo"]);
        }

        public DataTable Exam_Get_Passed_Exams()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where RoomID='{0}' and status=1 and IsChecking=0 And IsOver=1 order by QueueID DESC", strRoomID);
            return getDB().query(sql);
        }

        public DataTable Exam_Get_Need_Waiting_Exams()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("select * from JiaohaoTable where IsNeedWaiting=1 and ExamSatus=2 and status=0 and IsChecking=0 order by QueueID", strRoomID);
            return getDB().query(sql);
        }


        //检查转入本检查室
        public bool Exam_Transfer_Exams(List<Exam> listExam) 
        {
            if (null == listExam) return false;
            if (listExam.Count <= 0) return false;

            //获得IndexID串
            String strIndexCluster = "";
            foreach (Exam exam in listExam)
            {
                if (strIndexCluster.Length > 0) strIndexCluster += ",";
                strIndexCluster += exam.IndexID;
            }

            //生成新的房间号
            String strNewRoomID = "0";
            if (CONFIG.system_param["room_id"].IndexOf("4,") >= 0) strNewRoomID = "4";

            //2.更新检查为IsChecking
            String sql = String.Format("update JiaohaoTable set RoomID={0} where IndexID in ({1})",
                            strNewRoomID, strIndexCluster);

            int nRowCount = getDB().update(sql);


            return nRowCount > 0 ? true : false;
        }        
        
        //憋尿确认
        public bool Exam_Bieniao_Confirm_Exams(List<Exam> listExam)
        {
            if (null == listExam) return false;
            if (listExam.Count <= 0) return false;

            //获得IndexID串
            String strIndexCluster = "";
            foreach (Exam exam in listExam)
            {
                if (strIndexCluster.Length > 0) strIndexCluster += ",";
                strIndexCluster += exam.IndexID;
            }

            //2.更新检查为IsNeedWaiting=0
            String sql = String.Format("update JiaohaoTable set IsNeedWaiting=0 where IndexID in ({0})",
                            strIndexCluster);

            int nRowCount = getDB().update(sql);


            return nRowCount > 0 ? true : false;
        }


        //取消检查(设为过号)
        public bool Exam_Cancel_Exams(List<Exam> listExam)
        {
            if (null == listExam) return false;
            if (listExam.Count <= 0) return false;

            //获得IndexID串
            String strIndexCluster = "";
            foreach (Exam exam in listExam)
            {
                if (strIndexCluster.Length > 0) strIndexCluster += ",";
                strIndexCluster += exam.IndexID;
            }

            //2.更新检查为IsChecking
            String sql = String.Format("update JiaohaoTable set status=1,IsChecking=0 where IndexID in ({0})", strIndexCluster);

            int nRowCount = getDB().update(sql);


            return nRowCount > 0 ? true : false;
        }

        //取消检查(设为匹配)
        public bool Exam_Delete_Exams(List<Exam> listExam)
        {
            if (null == listExam) return false;
            if (listExam.Count <= 0) return false;

            //获得IndexID串
            String strIndexCluster = "";
            foreach (Exam exam in listExam)
            {
                if (strIndexCluster.Length > 0) strIndexCluster += ",";
                strIndexCluster += exam.IndexID;
            }

            //2.更新检查为IsChecking=0,ExamSatus=3,这样就不再在过号列表中出现
            String sql = String.Format("update JiaohaoTable set ExamSatus=3,status=1,IsChecking=0 where IndexID in ({0})", strIndexCluster);

            int nRowCount = getDB().update(sql);


            return nRowCount > 0 ? true : false;
        }

        //过号重排,不直接呼叫
        public bool Exam_back_to_queue_Passed_Exams(String strIndexID)
        {
            String sql = "update JiaohaoTable set ExamSatus=2,status=0,IsChecking=0,CheckSiteID='',IsRecall=1 where IndexID=" + strIndexID;
            int nRowCount = getDB().update(sql);

            return nRowCount > 0 ? true : false;
        }

        //延后检查,把待检的检查序号向后退5位,通过修改QueueID的方法
        public bool Exam_Postpone(String strIndexID) //------------------>不再使用
        {
            if(strIndexID.Trim().Length<=0)return false;

            //获取本检查室的待检列表
            DataTable tbl = Exam_Get_Pending();
            if (CSTR.IsTableEmpty(tbl)) return false;

            int nCount = tbl.Rows.Count;//待检队列总数
            int myPoint = -1;//指定重排检查的指针
            int toPoint = -1;//重排序号应该夹在point1和point2所对应检查的中间
            int delayCount = 0;//指示向后移动了多少个检查
            for (int i = 0; i < nCount; i++)
            {
                //对指定的IndexID进行定位
                if (tbl.Rows[i]["IndexID"].ToString().Equals(strIndexID))
                {
                    myPoint = i; toPoint = i;//定位成功并初始化toPoint
                }
                if (myPoint < 0) continue;//在没有对myPoint定位前不做下一步处理

                //记录移动的次数,如果移动了预期的数目后直接跳出循环
                delayCount++;
                if (delayCount > 5) break;

                //移动指针
                toPoint = i;
            }

            //判断是否检索到移动的位置
            if (myPoint < 0) return false;
            if (toPoint < 0) return false;
            if (toPoint < myPoint) return false;

            Int64 toQueueID = Convert.ToInt64(tbl.Rows[toPoint]["QueueID"].ToString());
            Int64 toQueueID_Next = 0;
            if (toPoint == nCount - 1)
            {
                //到了队列的最后一个
                toQueueID_Next = toQueueID + 100;
            }
            else
            {
                //取得下一个检查的QueueID
                toQueueID_Next = Convert.ToInt64(tbl.Rows[toPoint+1]["QueueID"].ToString());
            }
            //重新计算QueueID=前后两个Queue的平均数
            Int64 newQueue = (toQueueID + toQueueID_Next) / 2;

            String sql = String.Format("update JiaohaoTable set QueueID={0} where IndexID={1}",
                                       newQueue, strIndexID);
            int nRowCount = getDB().update(sql);

            return nRowCount > 0 ? true : false;

        }

        public DataTable Exam_Get_Other_Room_Pending_Exams(String strRoomID)
        {
            //取得别的检查室的检查列表
            //[规则]别的检查室不出现互斥的检查,即[ExamType]为'非4号室'的不出现
            String sql = String.Format("select * from JiaohaoTable " +
                "where RoomID in ({0}) and ExamSatus=2 and status=0 and IsChecking=0 " +
                "and ExamType not in ('非4号室') "+
                "and RoomID not in (10,20,30,40)  "+//不包括指定检查室
                "order by QueueID", strRoomID);
            return getDB().query(sql);
        }


        #region 体检叫号专有查询#########################################################################
        //获取指定房间的Pending的队列总数,忽略QueueActive状态(use case:在新到检时需要知道各个房间的总检查,而非活动检查)
        public int TJ_Queue_Arrange_Get_Specified_Room_Pending_Queue_Count_Ignore_QueueActive(String roomID)
        {
            //只要IsOver=0就属于未完成检查
            String sql = String.Format("Select count(*) as num From JiaohaoTable " +
                "Where  IsOver=0 And RoomID='{0}'", roomID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return -1;

            String strNum = CSTR.ObjectTrim(tbl.Rows[0]["num"]);
            if (strNum.Length <= 0) return -1;
            int count = CSTR.convertToInt(strNum);

            return count;
        }

        //获取指定房间的Pending的实际队列总数
        public int TJ_Queue_Arrange_Get_Specified_Room_Actual_Pending_Queue_Count(String roomID)
        {
            //包含状态字为 1000,1110的检查,即只要QueueActive=1,IsOver=0
            String sql = String.Format("Select count(*) as num From JiaohaoTable " +
                "Where QueueActive=1 And IsOver=0 And RoomID='{0}'", roomID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return -1;
            int count = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[0]["num"]));

            return count;
        }
        //获取指定QueueID的活动检查在指定房间的队列次序(前面还有?人)
        public int TJ_Queue_Arrange_Get_Position_In_Specified_Room_Actual_Pending_Queue(String roomID, String queueID)
        {
            //包含状态字为 1000,1110的检查,即只要QueueActive=1,IsOver=0
            String sql = String.Format("Select count(*) as num From JiaohaoTable " +
                "Where QueueActive=1 And IsOver=0 And RoomID='{0}' And QueueID<{1}", roomID, queueID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return -1;
            int count = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[0]["num"]));

            return count;
        }

        //设置指定的IndexID的检查为检查完成状态
        public void TJ_Queue_Arrange_Set_Specified_IndexID_To_ExamOver(String strIndexID)
        {
            strIndexID = CSTR.trim(strIndexID);
            if (strIndexID.Length <= 0) return;

            //无需排队的尿检也可以设置[已确认检查],因此IsNeedQueue也统一更改为1
            String sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=1,IsChecking=0,IsOver=1,IsNeedQueue=1,EndTime=NOW() " +
                "Where IndexID={0}", strIndexID);

            getDB().update(sql);
        }

        //设置指定的IndexID的检查为检查完成状态
        public void TJ_Queue_Arrange_Set_Specified_IndexID_To_Pending_First_One(String strIndexID)
        {
            strIndexID = CSTR.trim(strIndexID);
            if (strIndexID.Length <= 0) return;

            //取得目前PendingFirstOne的QueueID
            DataTable tbl = Exam_Get_Pending_First_One();
            if (CSTR.IsTableEmpty(tbl)) return;

            int nowQueueID = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[0]["QueueID"]));
            if (nowQueueID <= 0) return;
            nowQueueID -= 50;//排在第一个之前

            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];

            String sql = String.Format("Update JiaohaoTable Set QueueActive=1,status=0,IsChecking=0,IsOver=0," +
                "QueueID={0} Where IndexID={1} and RoomID='{2}'", nowQueueID, strIndexID, strRoomID);

            getDB().update(sql);
        }

        //设置嘀卡时间
        //返回: 1:第一次嘀卡 2:第二次嘀卡 -1:失败
        public int TJ_Queue_Arrange_Time_Set_CardReadTime_By_IndexID(String strIndexID)
        {
            int nRet = -1;

            strIndexID = CSTR.trim(strIndexID);
            if (strIndexID.Length <= 0) return -1;

            //获取指定IndexID的检查
            String sql = String.Format("Select * From JiaohaoTable Where IndexID={0}", strIndexID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return -1;

            String strCallTime1 = CSTR.ObjectTrim(tbl.Rows[0]["CardReadTime1"]);

            if (strCallTime1.Length <= 0)//首次嘀卡
            {
                sql = String.Format("Update JiaohaoTable Set CardReadTime1=NOW() " +
                    "Where IndexID={0}", strIndexID);
                nRet = 1;
            }
            else//第二次嘀卡,同时设置EndTime
            {
                sql = String.Format("Update JiaohaoTable Set CardReadTime2=NOW(),EndTime=NOW() " +
                    "Where IndexID={0}", strIndexID);
                nRet = 2;
            }

            getDB().update(sql);

            return nRet;
        }

        //获取指定DJLSH的当前活动检查(1000与1110的在检都算是活动检查
        public DataTable TJ_Queue_Arrange_Get_Activated_Exam(String DJLSH)
        {
            DJLSH = CSTR.trim(DJLSH);
            if (DJLSH.Length <= 0) return null;

            //String sql = String.Format("Select * From JiaohaoTable Where QueueActive=1 " +
            //    "And status=0 And IsChecking=0 And IsOver=0 " +
            //    "And DJLSH='{0}'", DJLSH);

            String sql = String.Format("Select * From JiaohaoTable Where " +
                "QueueActive=1 And IsOver=0 " +
                "And DJLSH='{0}'", DJLSH);

            return getDB().query(sql);
        }


        //检查室内预处理,如果同类检查室全部关闭,则强行打开一个
        //返回处理后的clusterRooms
        public String TJ_Queue_Arrange_MultiRooms_Pre_Muniply(String clusterRooms)
        {
            String full_rooms = "[302][303][304][305][306][307][308][309][311][312][313][315][316][324][325][326][401][402][403][404][405][406][322]";

            //把输入房间串转换成独立数组
            String[] clusterRooms_arr = CSTR.splitRooms(clusterRooms);
            if (null == clusterRooms_arr) return clusterRooms;

            //1.检测clusterRooms是否需要预处理
            bool is_has_other_room = false;
            foreach (String roomID in clusterRooms_arr)
            {
                if (full_rooms.IndexOf(roomID) < 0)
                {
                    is_has_other_room = true;
                    break;
                }
            }
            //则无需处理,直接返回原串
            if (is_has_other_room) return clusterRooms;

            //2.过滤clusterRooms中disactive的房间
            String active_rooms = "";
            foreach (String roomID in clusterRooms_arr)
            {
                if (DatabaseCache.RoomInfo_is_active(roomID)) active_rooms += roomID;
            }
            //有active的房间串,则直接返回此房间串
            if (active_rooms.Length > 0) return active_rooms;

            //3.所有的房间都为disactive,则在相关房间中查找有无active的房间
            //至此,处理开始---------------------------------------------------------------
            String ReletiveMuniplyRoomID = clusterRooms_arr[0];//只处理串中的第一个RoomID
            //3.1.构建基本数据

            //mapRooms结构 Dictionary<String.强制打开房间, String[].同类房间>
            Dictionary<String, String[]> mapRooms = new Dictionary<string, string[]>();
            mapRooms.Add("[304]", new String[] { "[302]", "[303]", "[304]", "[305]", "[405]" });
            mapRooms.Add("[306]", new String[] { "[306]","[307]", "[401]", "[402]", "[403]", "[404]" });
            mapRooms.Add("[311]", new String[] { "[309]", "[311]" });
            mapRooms.Add("[313]", new String[] { "[312]", "[313]", "[308]" });
            mapRooms.Add("[315]", new String[] { "[315]", "[316]" });
            mapRooms.Add("[324]", new String[] { "[324]", "[325]", "[326]" });
            mapRooms.Add("[322]", new String[] { "[322]", "[406]" });


            //3.2 查找ReletiveMuniplyRoomID所属的房间类型
            bool isMatched = false;
            String matchedKey = "";
            String[] matchRoomedArr = null;
            foreach (String key in mapRooms.Keys)
            {
                String[] roomArr = mapRooms[key];
                foreach (String roomID in roomArr)
                {
                    if (roomID.Equals(ReletiveMuniplyRoomID))
                    {
                        isMatched = true;
                        matchedKey = key;
                        matchRoomedArr = roomArr;
                    }
                }
            }
            //如果没有匹配项,则返回ReletiveMuniplyRoomID
            if (false == isMatched) return ReletiveMuniplyRoomID;
            
            //3.3 对匹配的key和RoomArr逐个测试,返回第一个active的房间
            if (DatabaseCache.RoomInfo_is_active(matchedKey)) return matchedKey;
            foreach (String roomID in matchRoomedArr)
            {
                if (DatabaseCache.RoomInfo_is_active(roomID)) return roomID;
            }

            //所有的处理都无效,则返回ReletiveMuniplyRoomID
            return ReletiveMuniplyRoomID;
        }

        //NewArrival登记时使用
        //同类房间登记前预处理:特别规则,房间开启状态的综合考虑
        //Prefer:未完成检查少的房间
        public String TJ_Queue_Recommended_RoomID_For_New_Register(String clusterRooms, String patientGender, String TJLB)
        {
            //调用房间预处理程序,disactive的房间
            clusterRooms = TJ_Queue_Arrange_MultiRooms_Pre_Muniply(clusterRooms);

            #region 房间组合的处理---------------------------Start
            //对[309][311]特别处理
            if (clusterRooms.Equals("[309][311]"))
            {
                if (patientGender.Equals("女"))
                {
                    return "[311]";
                }
                else
                {
                    return "[309]";
                }
            }
            #endregion 房间组合的处理---------------------------End


            //1.分解房间串,如果只有一个房间则不予处理直接返回
            String[] arrRooms = CSTR.splitRooms(clusterRooms);
            if (null == arrRooms) return "";
            if (arrRooms.Length <= 0) return "";
            if (arrRooms.Length == 1) return arrRooms[0];

            //从此处起,房间数一定两个或以上
            int countRooms = arrRooms.Length;

            //2.对多个房间比较PendingExam的数目-----------------------------------------------------------Start
            int minPendingCount = 9999;//Pending检查数最少的数目
            List<String> minPendingRooms = new List<string>();//活动检查数最少的房间
            for (int i = 0; i < countRooms; i++)
            {
                //Pending检查数比较
                int qCount = TJ_Queue_Arrange_Get_Specified_Room_Pending_Queue_Count_Ignore_QueueActive(arrRooms[i]);
                if (qCount < 0) qCount = 0;

                if (qCount < minPendingCount)
                {
                    //清除之前的房间,并添加新的最少Pending检查的房间
                    minPendingRooms.Clear();
                    minPendingRooms.Add(arrRooms[i]);
                    //更新最小队列数目
                    minPendingCount = qCount;
                }
                else if (qCount == minPendingCount)
                {
                    //增加最少Pending检查的房间
                    minPendingRooms.Add(arrRooms[i]);
                }
            }
            if (minPendingRooms.Count <= 0) return arrRooms[0];

            //如果活动检查数最少房间只有一个,直接返回
            if (minPendingRooms.Count == 1) return minPendingRooms[0];
            //  对多个房间比较PendingExam的数目-----------------------------------------------------------End

            //3.比较活动检查数目,并比较选择一个优选房间---------------------------------------------------Start
            int minQueueActiveCount = 9999;//活动检查数最少的数目
            List<String> minQueueActiveRooms = new List<string>();//活动检查数最少的房间
            for (int i = 0; i < minPendingRooms.Count; i++)
            {
                //活动检查数比较
                int qCount = TJ_Queue_Arrange_Get_Specified_Room_Actual_Pending_Queue_Count(minPendingRooms[i]);
                if (qCount < 0) qCount = 0;

                if (qCount < minQueueActiveCount)
                {
                    //清除之前的房间,并添加新的最少活动检查的房间
                    minQueueActiveRooms.Clear();
                    minQueueActiveRooms.Add(minPendingRooms[i]);
                    //更新最小队列数目
                    minQueueActiveCount = qCount;
                }
                else if (qCount == minQueueActiveCount)
                {
                    //增加最少活动检查的房间
                    minQueueActiveRooms.Add(minPendingRooms[i]);
                }
            }
            if (minQueueActiveRooms.Count <= 0) return minPendingRooms[0];

            //如果活动检查数最少房间只有一个,直接返回
            if (minQueueActiveRooms.Count == 1) return minQueueActiveRooms[0];
            //  比较活动检查数目,并比较选择一个优选房间---------------------------------------------------End

            //4.如果多个房间的PendingExam数目也相同,随机返回一个
            Random ran = new Random();
            int p = ran.Next(minQueueActiveRooms.Count);//返回0~(Count-1)
            return minQueueActiveRooms[p];
        }

        //使用时机:
        //不用于新登记,而是用于选择下一个活动检查
        //多个房间选择一个作为活动检查时,调用此方法,根据活动队列的多少,得到推荐房间
        //优化:如果活动检查一样多,则选择总未完成检查数少的那一个
        public String TJ_Queue_Recommended_RoomID_For_QueueActive(String clusterRooms)
        {
            //1.分解房间串,如果只有一个房间则不予处理直接返回
            String[] arrRooms = CSTR.splitRooms(clusterRooms);
            if (null == arrRooms) return "";
            if (arrRooms.Length <= 0) return "";
            if (arrRooms.Length == 1) return arrRooms[0];

            //从此处起,房间数一定两个或以上
            int countRooms = arrRooms.Length;

            //2.对有效房间分别查询Pending的活动检查数目,并比较选择一个优选房间
            int minQueueActiveCount = 9999;//活动检查数最少的数目
            List<String> minQueueActiveRooms = new List<string>();//活动检查数最少的房间
            for (int i = 0; i < countRooms; i++)
            {
                //活动检查数比较
                int qCount = TJ_Queue_Arrange_Get_Specified_Room_Actual_Pending_Queue_Count(arrRooms[i]);
                if (qCount < 0) qCount = 0;

                if (qCount < minQueueActiveCount)
                {
                    //清除之前的房间,并添加新的最少活动检查的房间
                    minQueueActiveRooms.Clear();
                    minQueueActiveRooms.Add(arrRooms[i]);
                    //更新最小队列数目
                    minQueueActiveCount = qCount;
                }
                else if (qCount == minQueueActiveCount)
                {
                    //增加最少活动检查的房间
                    minQueueActiveRooms.Add(arrRooms[i]);
                }
            }
            if (minQueueActiveRooms.Count <= 0) return arrRooms[0];

            //如果活动检查数最少房间只有一个,直接返回
            if (minQueueActiveRooms.Count == 1) return minQueueActiveRooms[0];
            
            //3.对多个活动检查数相同的房间,比较PendingExam的数目
            int minPendingCount = 9999;//Pending检查数最少的数目
            List<String> minPendingRooms = new List<string>();//活动检查数最少的房间
            for (int i = 0; i < minQueueActiveRooms.Count; i++)
            {
                //Pending检查数比较
                int qCount = TJ_Queue_Arrange_Get_Specified_Room_Pending_Queue_Count_Ignore_QueueActive(minQueueActiveRooms[i]);
                if (qCount < 0) qCount = 0;

                if (qCount < minPendingCount)
                {
                    //清除之前的房间,并添加新的最少Pending检查的房间
                    minPendingRooms.Clear();
                    minPendingRooms.Add(minQueueActiveRooms[i]);
                    //更新最小队列数目
                    minPendingCount = qCount;
                }
                else if (qCount == minPendingCount)
                {
                    //增加最少Pending检查的房间
                    minPendingRooms.Add(minQueueActiveRooms[i]);
                }
            }
            if (minPendingRooms.Count <= 0) return "";

            //如果活动检查数最少房间只有一个,直接返回
            if (minPendingRooms.Count == 1) return minPendingRooms[0];

            //4.如果多个房间的PendingExam数目也相同,随机返回一个
            Random ran = new Random();
            int p = ran.Next(minPendingRooms.Count);//返回0~(Count-1)
            return minPendingRooms[p];
        }


        //插入指定队列的第n位--------------------------------------------------
        /// <summary>
        /// 返回新的QueueID,0:空队列(用原有的QueueID) 大于0:可以使用的QueueID -1:数据库链接错误
        /// 参数strRoomID:房间号 nIndex:默认为0,表示插入到最后一个,如>0则表示插入到第nIndex之后
        /// </summary>
        /// <param name="roomID"></param>
        /// <returns></returns>
        private int TJ_Queue_Arrange_Get_ReArrange_QueueID(String strRoomID, int nIndex = 0)
        {
            //1.查找指定房间最后一个检查的QueueID,并生成新的QueueID,返回0表示该房间队列目前为空
            String sql = String.Format("Select Count(*) as num From JiaohaoTable " +
                                "Where RoomID='{0}' And QueueActive=1 And status=0 " +
                                "Order by QueueID DESC", strRoomID);
            DataTable tblCount = getDB().query(sql);
            if (CSTR.IsTableEmpty(tblCount)) return -1;
            int nCount = CSTR.convertToInt(CSTR.ObjectTrim(tblCount.Rows[0]["num"]));
            if (nCount <= 0) return 0;//指定的房间号目前队列为空


            //(nCount > 0)计算出新的QueueID
            sql = String.Format("Select QueueID From JiaohaoTable " +
                                "Where RoomID='{0}' And QueueActive=1 And status=0 " +
                                "Order by QueueID", strRoomID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return 0;
            nCount = tbl.Rows.Count;
            if (nCount <= 0) return 0;

            //如果指定插入最后一个或指定的nIndex>=nCount,返回最后一个QueueID+50
            if (nIndex == 0 || nIndex >= nCount)
            {
                return CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[nCount - 1]["QueueID"])) + 50;
            }

            //获取第nIndex-1和nIndex记录的QueueID,并返回中间值
            int queueID1 = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[nIndex - 1]["QueueID"]));
            int queueID2 = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[nIndex]["QueueID"]));

            return (queueID1 + queueID2) / 2;
        }

        //设置指定IndexID的检查为活动检查--------------------------------------------------
        public void TJ_Queue_Arrange_Activate_Specified_IndexID(String indexID)
        {
            getDB().Call_SP_Activate_Specified_IndexID(indexID);
        }

        //没有使用存储过程的版本
        //设置指定IndexID的检查为活动检查--------------------------------------------------
        public void TJ_Queue_Arrange_Activate_Specified_IndexID_BK(String indexID)
        {
            indexID = CSTR.trim(indexID);
            if (indexID.Length <= 0) return;

            //1.查找对应的IndexID检查信息
            String sql = String.Format("Select * From JiaohaoTable Where IndexID={0}", indexID);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return;
            String strRoomID = CSTR.ObjectTrim(tbl.Rows[0]["RoomID"]);
            String strDJLSH = CSTR.ObjectTrim(tbl.Rows[0]["DJLSH"]);
            int nQueueID = CSTR.convertToInt(CSTR.ObjectTrim(tbl.Rows[0]["QueueID"]));
            String strStatus = CSTR.ObjectTrim(tbl.Rows[0]["status"]);
            String strIsChecking = CSTR.ObjectTrim(tbl.Rows[0]["IsChecking"]);

            //2.Reset same DJLSH Exam state(包括IsOver=1)
            sql = String.Format("Update JiaohaoTable Set QueueActive=0 Where DJLSH='{0}'", strDJLSH);
            getDB().update(sql);
            //2.避免出现状态串由'1110'->'0110'的非法情况
            sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=0,IsChecking=0,IsOver=0,IsSaid=0 Where "+
                                "IsOver=0 And DJLSH='{0}'", strDJLSH);
            getDB().update(sql);

            //3.检查是否为IsChecking的检查
            if (strIsChecking.Equals("1"))
            {
                //直接拉回队列,保留原有的QueueID
                sql = String.Format("Update JiaohaoTable Set QueueActive=1,status=0,IsChecking=0,IsOver=0,IsSaid=0,IsRecall=1,ArrangeTime=NOW() " +
                                    "Where IndexID={0}", indexID);
                getDB().update(sql);

                return;
            }

            //4.检查是否为过号重排
            if (strStatus.Equals("1"))
            {
                //直接拉回队列,QueueID向后挪动3位
                int nInsertIndex = 3;//指定插入位置
                int nRecallQueueID = TJ_Queue_Arrange_Get_ReArrange_QueueID(strRoomID, nInsertIndex);
                if (nRecallQueueID > 0)//其他情况本IndexID对应的QueueID保持原有值
                {
                    nQueueID = nRecallQueueID;
                }
                sql = String.Format("Update JiaohaoTable Set QueueID={0},QueueActive=1,status=0,IsChecking=0,IsOver=0,IsSaid=0,IsRecall=1 " +
                                    "Where IndexID={1}", nQueueID, indexID);
                getDB().update(sql);

                return;
            }


            //3.查找指定房间最后一个检查的QueueID,并生成新的QueueID,返回0表示该房间队列目前为空
            int nNewQueueID = TJ_Queue_Arrange_Get_ReArrange_QueueID(strRoomID, 0);//插入到最后一个
            if (nNewQueueID > 0)//其他情况本IndexID对应的QueueID保持原有值
            {
                nQueueID = nNewQueueID;
            }

            //2.设置活动检查并重新指定QueueID
            sql = String.Format("Update JiaohaoTable Set QueueID={0},QueueActive=1,status=0,IsChecking=0,IsOver=0,IsSaid=0,ArrangeTime=NOW() " +
                                "Where IndexID={1}", nQueueID, indexID);
            getDB().update(sql);
        }

        //同一个DJLSH的多个检查,选择一个为当前有效检查--------------------------------------------------
        //当is_force_do_arrange为true时,不管是否当前有无有效的活动检查都重新安排
        //默认处理为:当前有有效活动检查时,不予处理,当前有过检的检查则入队重排
        public void TJ_Queue_Arrange_Search_And_Activate_One_Item_By_DJLSH(String DJLSH, bool is_force_do_arrange = false)
        {
            DJLSH = CSTR.trim(DJLSH);
            if (DJLSH.Length <= 0) return;

            //1.查找指定DJLSH的所有未完成检查(包括过号与在检)
            String sql = String.Format("Select * From JiaohaoTable Where DJLSH='{0}' And IsOver=0", DJLSH);
            DataTable tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return;

            //如果不是强制重排,则查找是否已经存在合规的活动检查,找到则直接返回
            if (is_force_do_arrange == false)
            {
                foreach (DataRow row in tbl.Rows)
                {
                    //生成状态串
                    String status_cluster=String.Format("{0}{1}{2}{3}",
                        CSTR.ObjectTrim(row["QueueActive"]),
                        CSTR.ObjectTrim(row["status"]),
                        CSTR.ObjectTrim(row["IsChecking"]),
                        CSTR.ObjectTrim(row["IsOver"]));

                    //是否存在合规的活动检查
                    if (status_cluster.Equals("1000"))
                    {
                        //查找到已经存在合规的活动检查,找到则直接返回
                        return;
                    }

                    //是否存在正在检查的记录
                    if (status_cluster.Equals("1110"))
                    {
                        //查找到已经存在正在检查的记录,找到则直接返回
                        return;
                    }
                }
            }

            String strGender = CSTR.ObjectTrim(tbl.Rows[0]["PatientGender"]);
            String TJLB = CSTR.ObjectTrim(tbl.Rows[0]["TJLB"]);
            int nTJLB = CSTR.convertToInt(TJLB);//职检的TJLB编号为 07~15:(nTJLB != -1 && nTJLB >= 7 && nTJLB <= 15)
            
            //如果只有一条记录,则直接设置此记录为活动检查
            if (tbl.Rows.Count == 1)
            {
                TJ_Queue_Arrange_Activate_Specified_IndexID(CSTR.ObjectTrim(tbl.Rows[0]["IndexID"]));
                return;
            }

            //2.如果存在过号或在检的记录,有则优先处理
            foreach (DataRow row in tbl.Rows)
            {
                if (CSTR.ObjectTrim(row["status"]).Equals("1"))
                {
                    TJ_Queue_Arrange_Activate_Specified_IndexID(CSTR.ObjectTrim(row["IndexID"]));
                    return;
                }
            }

            //3.检查有无特定的开始和总检房间
            bool has_begin_room_01 = false;
            bool has_other_room = false;
            bool has_fsk_us_room = false;
            bool has_end_room = false;

            String strBeginRooms_01 = "";
            bool strBeginRooms_is_Kids = false;//是否儿童入园(只要检查中的其中一个TJLB为C1)
            String strOtherRooms = "";
            String strFskUsRooms = "";
            String strEndRooms = "";

            //总检房间的定义(职检无总检定义,[312][313]不必作为最后房间,最后是前台)
            String possibleEndRooms = "[309][311]";

            foreach (DataRow row in tbl.Rows)
            {
                String strRoomID = CSTR.ObjectTrim(row["RoomID"]);

                //开始房间的定义 ([322][320][319][406])
                if (strRoomID.Equals("[322]") || strRoomID.Equals("[406]") || strRoomID.Equals("[320]") || strRoomID.Equals("[319]"))
                {
                    has_begin_room_01 = true;
                    strBeginRooms_01 += strRoomID;
                    //判断是否为"C1"-"儿童入园"
                    if (CSTR.ObjectTrim(row["TJLB"]).Equals("C1")) strBeginRooms_is_Kids = true;
                }
                else if(possibleEndRooms.IndexOf(strRoomID)>=0)
                {
                    has_end_room = true;
                    strEndRooms += strRoomID;
                }
                else if ("[影像科][超声科]".IndexOf(strRoomID) >= 0)
                {
                    has_fsk_us_room = true;
                    strFskUsRooms += strRoomID;
                }
                else
                {
                    has_other_room = true;
                    strOtherRooms += strRoomID;
                }
            }//end foreach

            if ((has_begin_room_01) && strBeginRooms_is_Kids && (strBeginRooms_01.IndexOf("[319]") >= 0) 
                && (strBeginRooms_01.IndexOf("[322]") >= 0 || strBeginRooms_01.IndexOf("[406]") >= 0))
            {
                //3.[规则]:儿童入园要先做[319]->[322],因为如果先抽血再做血压,孩子会再次害怕
                strBeginRooms_01 = "[319]";
            }
            else if ((has_begin_room_01) && (has_other_room))//必须有首检房间的同时有OtherRoom才处理
            {
                //3.[规则]:取消首检房间均衡设置:如果首检房间的活动检查数大于15,启动应急均衡

                //三个首检房间的活动检查总数
                int total_first_room_length = DatabaseCache.Queue_Length_Count_MultiRoom_Cluster_Active_Exam("[322][320][319]");
                //当前检查的首检房间中最小的队列长度
                //int min_first_room_length = DatabaseCache.Queue_Length_Min_Count_MultiRoom_Active_Exam(strBeginRoomCluster);
                //当前检查的OtherRoom的最小队列长度
                //int min_other_room_length = DatabaseCache.Queue_Length_Min_Count_MultiRoom_Active_Exam(strOtherRooms);

                //如果首检房间活动检查总数>15,同时首检房间的最小队列都大于其他房间的最小队列时,启动应急均衡
                //实现途径:把首检房间全部加入到OtherRoom,统一分配
                int EMERGENT_START_LENGTH = 15;//定义应急均衡的首检房间队列总数
                if (total_first_room_length > EMERGENT_START_LENGTH)
                {
                    if (has_begin_room_01)
                    {
                        strOtherRooms += strBeginRooms_01;
                        has_begin_room_01 = false;
                        strBeginRooms_01 = "";
                    }
                }
            }

            //4.选择分配,首选begin房间,其次其他房间,最后总检室
            String strRecommendedRoom = "";
            if (has_begin_room_01)
            {
                strRecommendedRoom = TJ_Queue_Recommended_RoomID_For_QueueActive(strBeginRooms_01);
            }
            else if (has_other_room)
            {
                strRecommendedRoom = TJ_Queue_Recommended_RoomID_For_QueueActive(strOtherRooms);
            }
            else if (has_fsk_us_room)
            {
                strRecommendedRoom = TJ_Queue_Recommended_RoomID_For_QueueActive(strFskUsRooms);
            }
            else if (has_end_room)
            {
                strRecommendedRoom = TJ_Queue_Recommended_RoomID_For_QueueActive(strEndRooms);
            }

            //5.通过房间号查找对应的IndexID,并设置为活动检查
            foreach (DataRow row in tbl.Rows)
            {
                if (CSTR.ObjectTrim(row["RoomID"]).Equals(strRecommendedRoom))
                {
                    TJ_Queue_Arrange_Activate_Specified_IndexID(CSTR.ObjectTrim(row["IndexID"]));
                    break;
                }
            }
        }
        //------------------------------------------------------------------------------------------------------------------


        /// 设置检查重新入队->(用于被插队的在检重新入队,)####################################################################
        public int Exam_Queue_Arrange_Back_ToQueue_SameRoom_SameRFID(String strRfid)
        {
            strRfid = CSTR.trim(strRfid);
            if (strRfid.Length <= 0) return -1;

            //取得本进程对应的room_id
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("Update JiaohaoTable Set QueueActive=1,status=0,IsChecking=0,IsSaid=0,IsOver=0 Where " +
                "IsOver=0 "+
                "And RoomID='{0}' " +
                "AND Rfid='{1}'",
                strRoomID,
                strRfid);
            Console.WriteLine(sql);
            return getDB().update(sql);
        }


        //根据手环号码删除检查列表(不分状态,全部)--------------------------------------------------
        public void TJ_Exam_Delete_All_Item_By_RingNo(String ringNo)
        {
            ringNo = CSTR.trim(ringNo);
            if (ringNo.Length <= 0) return;

            //删除前先备份
            String sql = String.Format("Select * From JiaohaoTable Where RfidName='{0}'", ringNo);
            DataTable tbl = getDB().query(sql);
            BackupTable bk = new BackupTable(getDB(), "Backup_JiaohaoTable");
            bk.insert(tbl);

            //然后删除JiaohaoTable的记录
            sql = String.Format("Delete From JiaohaoTable Where RfidName='{0}'", ringNo);
            getDB().update(sql);

            Log.log(String.Format("手环回收:{0}------------------------手环号码:{1}",
                CONFIG.system_param["room_id"], ringNo));

            //System.Windows.Forms.MessageBox.Show("手环回收-测试");//[TEST][]
        }
        public void TJ_Exam_Delete_All_Item_By_RingNo_Backup___________1(String ringNo)
        {
            ringNo = CSTR.trim(ringNo);
            if (ringNo.Length <= 0) return;

            //删除前先备份
            String sql = String.Format("Select * From JiaohaoExamInfo Where RfidName='{0}'", ringNo);
            DataTable tbl = getDB().query(sql);
            BackupTable bk = new BackupTable(getDB(), "Backup_JiaohaoExamInfo");
            bk.insert(tbl);

            //先删除JiaohaoExamInfo的记录
            sql = String.Format("Delete From JiaohaoExamInfoWhere RfidName='{0}'", ringNo);
            getDB().update(sql);

            //删除前先备份
            sql = String.Format("Select * From JiaohaoTable Where RfidName='{0}'", ringNo);
            tbl = getDB().query(sql);
            bk = new BackupTable(getDB(), "Backup_JiaohaoTable");
            bk.insert(tbl);

            //然后删除JiaohaoTable的记录
            sql = String.Format("Delete From JiaohaoTable Where RfidName='{0}'", ringNo);
            getDB().update(sql);

            Log.log(String.Format("手环回收:{0}------------------------手环号码:{1}",
                CONFIG.system_param["room_id"], ringNo));

            //System.Windows.Forms.MessageBox.Show("手环回收-测试");//[TEST][]
        }

        //根据手环号码获取检查列表(不分状态,相同房间)--------------------------------------------------
        public DataRow[] TJ_Exam_Get_Same_Room_Item_Form_JiaohaoExamInfo_By_RingNo(String ringNo, String roomID)
        {
            //设置查询时间
            String sql = String.Format("Update JiaohaoTable Set QueryTime=NOW() Where  RfidName='{0}'", ringNo);
            getDB().update(sql);

            //此处因为有转移房间并直接呼叫,所以要重新刷新Cache,获取最新记录
            DatabaseCache.DB_Reflash_JiaohaoTable_And_JiaohaoExamInfo();

            return DatabaseCache.TJ_Exam_Get_Same_Room_Item_Form_JiaohaoExamInfo_By_RingNo(ringNo, roomID);
        }
        public DataTable TJ_Exam_Get_Same_Room_Item_Form_JiaohaoExamInfo_By_RingNo_Backup______________1(String ringNo, String roomID)
        {
            //设置查询时间
            String sql = String.Format("Update JiaohaoTable Set QueryTime=NOW() Where  RfidName='{0}'", ringNo);
            getDB().update(sql);

            //直接查询JiaohaoExamInfo表获取的项目才是最完整
            sql = String.Format("Select JiaohaoExamInfo.ProcedureStepName,JiaohaoTable.* " +
                                        "From JiaohaoExamInfo,JiaohaoTable " +
                                        "Where JiaohaoExamInfo.OrderID=JiaohaoTable.OrderID " +
                                        "AND JiaohaoTable.RfidName='{0}' " +
                                        "AND JiaohaoTable.RoomID='{1}'", ringNo, roomID);
            return getDB().query(sql);
        }

        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Exam_Form_JiaohaoTable_By_DJLSH(String DJLSH)
        {
            //直接查询JiaohaoExamInfo表获取的项目才是最完整
            String sql = String.Format("Select * From JiaohaoTable Where DJLSH='{0}'", DJLSH);
            return getDB().query(sql);
        }

        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Exam_Form_JiaohaoTable_By_RfidNo(String RfidNo)
        {
            //直接查询JiaohaoExamInfo表获取的项目才是最完整
            String sql = String.Format("Select * From JiaohaoTable Where Rfid='{0}'", RfidNo);
            return getDB().query(sql);
        }

        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Exam_Form_JiaohaoTable_By_ringNo(String ringNo)
        {
            //直接查询JiaohaoExamInfo表获取的项目才是最完整
            String sql = String.Format("Select * From JiaohaoTable Where RfidName='{0}'", ringNo);
            return getDB().query(sql);
        }

        //获取所有房间的在检信息--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Rooms_IsChecking_Exam()
        {
            //
            String sql = "Select * from JiaohaoTable Where status=1 And IsChecking=1";
            return getDB().query(sql);
        }

        //获取指定房间的下一个待检信息--------------------------------------------------
        public DataTable TJ_Exam_Get_Specified_Room_InReady_Exam(String roomID)
        {
            roomID = CSTR.trim(roomID);
            if (roomID.Length <= 0) return null;

            String sql = String.Format("select * from JiaohaoTable " +
                "where QueueActive=1 and RoomID='{0}' and status=0 and IsChecking=0 " +
                "order by QueueID Limit 1", roomID);
            return getDB().query(sql);
        }

        //获取所有房间的下一个检查的信息(仅供信息屏使用)--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Rooms_InReady_Exam_For_Midia_Screen()
        {
            //1.获取所有活动检查
            String sql = "Select * From JiaohaoTable Where QueueActive=1 And IsChecking=0";

            return getDB().query(sql);
        }

        //获取所有房间的下一个检查的信息--------------------------------------------------
        public DataTable TJ_Exam_Get_All_Rooms_InReady_Exam_Backup()
        {
            //1.获取需要叫号的房间列表
            String sql = "Select * From JiaohaoRoomInfo Where RoomState='active' And IsNeedQueue=1";
            DataTable tblRoomInfo = getDB().query(sql);
            if (CSTR.IsTableEmpty(tblRoomInfo)) return null;

            //2.对每个房间获取下一个待检信息
            DataTable tblInReady = null;
            foreach (DataRow rowRoomInfo in tblRoomInfo.Rows)
            {
                DataTable tbl = TJ_Exam_Get_Specified_Room_InReady_Exam(CSTR.ObjectTrim(rowRoomInfo["RoomID"]));
                if (CSTR.IsTableEmpty(tbl)) continue;

                //Merge Tables
                if (CSTR.IsTableEmpty(tblInReady))
                {
                    tblInReady = tbl;
                }
                else
                {
                    //合并查询结果
                    tblInReady.Merge(tbl);
                }
            }

            return tblInReady;
        }

        #endregion 体检叫号专有查询----------------------------------------------------------------------


        //写入到叫号TTS表, 登记台查询并语音呼叫(增加手环号码,和姓名合并)
        public void TTS_send(String roomID, String roomName, String patName, String siteID,String ringNo)
        {
            //去掉名字中的字母
            patName = CSTR.trim(patName);
            for (int i = 0; i < 10; i++)
            {
                patName = patName.Replace(i.ToString(), "");
            }

            //手环和名字合并
            patName = String.Format("({0})({1})", patName, CSTR.NumberToChinese(ringNo));

            String sql = String.Format("insert into JiaohaoTTS " +
                "(RoomID,RoomName,PatientNameChinese,SiteID,InsertTime) VALUES " +
                "('{0}','{1}','{2}','{3}',now())",
                roomID, roomName, patName, siteID);
            getDB().update(sql);
        }

        //取最后一条未处理的叫号信息
        public DataTable TTS_recvTopOne()
        {
            //String sql = "select JiaohaoTTS.*," +
            //    "abs(timestampdiff(second,now(),InsertTime)) as seconds " +
            //    "from JiaohaoTTS " +
            //    "where status=0 " +
            //    "order by id DESC limit 1";
            //return getDB().query(sql);
            return getDB().Call_SP_TTS_Get_One_Item_To_Speak();
        }

        #endregion


        #region Token操作 在对[RIS]数据库操作时需要,[叫号]数据查询与更新不需要
        //token操作步骤:
        //1.检查Token是否可用(Token_IsFree)
        //2.占用Token(Token_TakeIt())
        //3.等待一个时间段(Sleep)
        //4.检测Token是否真的被自己占用成功(Token_IsOwned)
        //5.释放Token,当RIS的查询更新工作完成

        //释放Token
        public void Token_Release()
        {
            String sql = "update JiaohaoParam " +
                "set CurrentHandlerAccessTime=DATE_ADD(now(),INTERVAL -10 minute)";//设置回10分钟前
            //"set CurrentHandler='XXXX-XXXX',CurrentHandlerAccessTime=now()-100000";
            getDB().update(sql);
        }

        public bool Token_Own()
        {
            /////////////////////可配置参数/////////////////////////////////////
            const int tokenFreeWaitTime_Second = 20;//token的线程占用最大时长(单位:秒,不是ms)
            //////////////////////////////////////////////////////////

            //token操作步骤:
            //1.检查Token是否可用(Token_IsFree)
            //返回当前拥有token的进程名称和占用时长(单位:秒)
            String sql = "select CurrentHandler," +
                "ABS(TIMESTAMPDIFF(second,now(),CurrentHandlerAccessTime)) as Sec " +
                "from JiaohaoParam " +
                "where CurrentHandlerAccessTime!=0 " +
                "and CurrentHandlerAccessTime is not null";
            DataTable tbl = getDB().query(sql);
            
            //如果为空说明出现非法字段,重新设置日期字段的内容
            if (CSTR.IsTableEmpty(tbl))
            {
                sql = "update JiaohaoParam set CurrentHandlerAccessTime=DATE_ADD(now(),INTERVAL -2 minute)";
                getDB().update(sql);
                return false;
            }


            Int64 nSeconds = 0;
            String strCurrentHandler = tbl.Rows[0]["CurrentHandler"].ToString();

            //获取token的占用时间
            //ABS(TIMESTAMPDIFF(second,now(),CurrentHandlerAccessTime)) as Sec
            //sec的值可能为NULL,需要做特别处理
            try
            {
                nSeconds = Convert.ToInt64(tbl.Rows[0]["Sec"].ToString());

            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }

            if (nSeconds < 0) return false;//sql中加了ABS(),必须大于等于0

            //如果进程名称和自己的相同或占用时长Timeout,都表示token可用
            if (nSeconds < tokenFreeWaitTime_Second) return false;

            //2.占用Token(Token_TakeIt())
            sql = String.Format("update JiaohaoParam " +
                "set CurrentHandler='{0}',CurrentHandlerAccessTime=now()", siteID);
            getDB().update(sql);

            //3.等待一个时间段(Sleep)
            System.Threading.Thread.Sleep(10);//等待10ms

            //4.检测Token是否真的被自己占用成功(Token_IsOwned)
            //返回当前拥有token的进程名称和占用时长(单位:秒)
            sql = "select CurrentHandler," +
                "ABS(TIMESTAMPDIFF(second,now(),CurrentHandlerAccessTime)) as Sec from JiaohaoParam";
            tbl = getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl)) return false;

            strCurrentHandler = tbl.Rows[0]["CurrentHandler"].ToString();
            nSeconds = Convert.ToInt64(tbl.Rows[0]["Sec"].ToString());

            if (nSeconds < 0) return false;//sql中加了ABS(),必须大于等于0
            if (siteID.Trim().Length <= 0) return false;//如果配置文件中siteID为空,非法

            //如果进程名称和自己的相同而且占用时长没有Timeout,才表示拥有
            if (nSeconds < tokenFreeWaitTime_Second && strCurrentHandler.Equals(siteID)) return true;

            return false;
        }

        #endregion Token

        #region 天方达数据库查询

        public DataTable TFD_QueryDJLSH(String strDJLSH)
        {
            if (CSTR.isEmpty(strDJLSH)) return null;

            String sql = String.Format("Select * From futian_user.TJ_TJDJB where DJLSH='{0}'", strDJLSH);

            return getTijianDB().query(sql);
        }
        public DataTable TFD_QueryAllExamToday()
        {
            String strToday = DateTime.Now.ToString("yyyy-MM-dd");
            String sql = String.Format("Select * from futian_user.TJ_TJDJB Where TJRQ>'{0} 01:01:01' Order By TJRQ DESC", strToday);

            return getTijianDB().query(sql);
        }
        #endregion 天方达数据库查询


    }//end class

    //定义接口类型
    public interface IWorkerCallback
    {
        //由窗口函数实现本接口,在线程的progress report中实现数据交互
        void WorkerCallback(Object obj);
    }
}//end namespace
