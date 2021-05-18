using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace GeneralCode
{

    public class DatabaseCache
    {
        //版本号
        public const String version_string = "[202103-31-FS-KA]";

        private static ConfigParam CONFIG = new ConfigParam();//xml配置文件参数获取

        private static  ExamQueue queue = new ExamQueue();
        private static  JSON json = new JSON();

        //Cache的数据库
        private static  DataTable tbl_JiaohaoExamInfo = null;
        private static  DataTable tbl_JiaohaoRfid = null;
        private static  DataTable tbl_JiaohaoRoomInfo = null;
        private static  DataTable tbl_JiaohaoSpecialExam = null;
        public static  DataTable tbl_JiaohaoTable = null;

        //数据库的更新时间
        private static  DateTime time_JiaohaoExamInfo = DateTime.Now;
        private static  DateTime time_JiaohaoRfid = DateTime.Now;
        private static  DateTime time_JiaohaoRoomInfo = DateTime.Now;
        private static  DateTime time_JiaohaoSpecialExam = DateTime.Now;
        private static  DateTime time_JiaohaoTable = DateTime.Now;

        //指示是否初始化
        private static  bool is_tbl_initiated = false;

        //本室房间号码(在AutoFetch调用存储过程时需不需要指定房间号)(医生诊台与叫号屏幕要,其他不要)
        public static String RoomID_for_StoreProcedure = "";
        public static String Tocken_Handler_Name_for_StoreProcedure = "";

        //lock使用的线程同步变量
        private static readonly object lock_obj = new object();

        #region 线程(自动DB获取)-------------------------------------------------------------Start
        private static DB_AutoFetcher Auto_Fetcher_Thread = new DB_AutoFetcher();//Cache自动刷新线程

        //DB_AutoFetcher的线程回调函数
        public static void Worker_DB_Fetched(Dictionary<String,DataTable> tables)
        {
            lock (lock_obj)
            {
                if (null == tables) return;
                if (tables.Count <= 0) return;

                foreach (String key in tables.Keys)
                {
                    if (key.Equals("JiaohaoTable"))
                    {
                        //把JiaohaoTable中的QueueID全部加前导0到8位,为了数字排序转字符排序的一致性
                        DataTable tempTbl = tables[key];
                        if (CSTR.IsTableEmpty(tempTbl) == false)
                        {
                            foreach (DataRow row in tempTbl.Rows)
                            {
                                String strQueueID = CSTR.ObjectTrim(row["QueueID"]);
                                strQueueID = strQueueID.PadLeft(10, '0');
                                row["QueueID"] = strQueueID;
                            }
                        }

                        tbl_JiaohaoTable = tempTbl;
                        time_JiaohaoTable = DateTime.Now;

                        //如果是JiaohaoTable更新,则刷新界面
                        Worker_DB_Fetched_need_reflesh_UI = true;
                    }
                    else if (key.Equals("JiaohaoExamInfo"))
                    {
                        tbl_JiaohaoExamInfo = tables[key];
                        time_JiaohaoExamInfo = DateTime.Now;
                    }
                    else if (key.Equals("JiaohaoRfid"))
                    {
                        tbl_JiaohaoRfid = tables[key];
                        time_JiaohaoRfid = DateTime.Now;
                    }
                    else if (key.Equals("JiaohaoRoomInfo"))
                    {
                        tbl_JiaohaoRoomInfo = tables[key];
                        time_JiaohaoRoomInfo = DateTime.Now;
                    }
                    else if (key.Equals("JiaohaoSpecialExam"))
                    {
                        tbl_JiaohaoSpecialExam = tables[key];
                        time_JiaohaoSpecialExam = DateTime.Now;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        //提示主程序是否需要刷新界面
        private static bool Worker_DB_Fetched_need_reflesh_UI = false;
        public static bool is_JiaohaoTable_changed()
        {
            bool is_changed = Worker_DB_Fetched_need_reflesh_UI;
            Worker_DB_Fetched_need_reflesh_UI = false;//每次读取后自动复位
            return is_changed;
        }

        //DB_AutoFetcher的线程启动
        public static void DatabaseCache_Auto_Fetcher_Start()
        {
            Auto_Fetcher_Thread.run();
        }

        #endregion 线程(自动DB获取)-------------------------------------------------------------End

        #region 数据库Cache初始化------------------------------------------------------------Start

        //是否初始化Cache成功----------------------------------------------------------------
        public static bool DB_is_init_ok()
        {
            return is_tbl_initiated;
        }

        public static bool DB_Reflesh_AllTable()
        {
            is_tbl_initiated = false;

            //Reflash each Table
            DB_Reflash_JiaohaoTable_And_JiaohaoExamInfo();

            DB_Reflesh_JiaohaoRfid();
            DB_Reflesh_JiaohaoRoomInfo();
            DB_Reflesh_JiaohaoSpecialExam();

            //任何一个配置Table无内容,都被认为异常
            is_tbl_initiated = true;
            if (CSTR.IsTableEmpty(tbl_JiaohaoRfid)) is_tbl_initiated = false;
            if (CSTR.IsTableEmpty(tbl_JiaohaoRoomInfo)) is_tbl_initiated = false;
            if (CSTR.IsTableEmpty(tbl_JiaohaoSpecialExam)) is_tbl_initiated = false;

            return is_tbl_initiated;
        }

        public static void DB_Reflash_JiaohaoTable_And_JiaohaoExamInfo()
        {
            Worker_DB_Fetched(Auto_Fetcher_Thread.update_JiaohaoTable_and_JiaohaoExamInfo());
        }

        public static void DB_Clear_ExamTable()
        {
            tbl_JiaohaoTable = null;
            tbl_JiaohaoExamInfo = null;
        }

        //把原有数据设为空后,重新刷新最新数据------------------------------------------------
        //配置表不可以为空
        public static void DB_Reflesh_JiaohaoRfid()
        {
            String sql = "Select * From JiaohaoRfid";
            tbl_JiaohaoRfid = json.revertDataTable(json.toJson(queue.getDB().query(sql)));
        }
        public static void DB_Reflesh_JiaohaoRoomInfo()
        {
            Worker_DB_Fetched(Auto_Fetcher_Thread.update_RoomInfo());
        }
        public static void DB_Reflesh_JiaohaoSpecialExam()
        {
            String sql = "Select * From JiaohaoSpecialExam";
            tbl_JiaohaoSpecialExam = json.revertDataTable(json.toJson(queue.getDB().query(sql)));
        }

        #endregion 数据库Cache初始化------------------------------------------------------------End

        #region 获取指定的Cache Table-----------------------------------------------------------Start
        //配置表不可以为空
        public static DataTable DB_Get_JiaohaoRfid_table()
        {
            return tbl_JiaohaoRfid;
        }

        public static DataTable DB_Get_JiaohaoRoomInfo_table()
        {
            return tbl_JiaohaoRoomInfo;
        }

        public static DataTable DB_Get_JiaohaoSpecialExam_table()
        {
            return tbl_JiaohaoSpecialExam;
        }

        //JiaohaoExamInfo与JiaohaoTable可以为空
        public static DataTable DB_Get_JiaohaoExamInfo_table()
        {
            return tbl_JiaohaoExamInfo;
        }

        public static DataTable DB_Get_JiaohaoTable_table()
        {
            return tbl_JiaohaoTable;
        }
        #endregion 获取指定的Cache Table--------------------------------------------------------End

        #region RFID手环入库查询-----------------------------------------------------Start
        public static DataRow[] RFID_get_Rfid_row(String rfid)
        {
            DataTable tbl = DB_Get_JiaohaoRfid_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            rfid = CSTR.trim(rfid);
            if (rfid.Length <= 0) return null;

            DataRow[] rowArr = tbl.Select(String.Format("Rfid='{0}'", rfid));

            return rowArr;
        }

        public static bool RFID_Is_Rfid_registed(String rfid)
        {
            DataRow[] rowArr = RFID_get_Rfid_row(rfid);
            if (CSTR.IsRowArrEmpty(rowArr)) return false;

            return true;
        }

        public static String RFID_get_ringNo(String rfid)
        {
            DataRow[] rowArr = RFID_get_Rfid_row(rfid);
            if (CSTR.IsRowArrEmpty(rowArr)) return "";

            String strRet = CSTR.ObjectTrim(rowArr[0]["CardNo"]);
            return strRet;
        }
        #endregion RFID手环入库查询-----------------------------------------------------End

        #region FinalCheckWnd专属查询-------------------------------------------Start
        /**
        * 获取所有的检查项目(ExamInfo关联)
        * 使用:
        * FinalCheckWnd.cs在显示检查列表时调用
        * 
        * **/
        public static DataTable ExamInfo_get_All_Items_by_RingNo(String ringNo)
        {
            DataTable tblRet = null;

            ringNo = CSTR.trim(ringNo);
            if (ringNo.Length <= 0) return null;

            //获取JiaohaoTable表
            DataTable cache_tbl = DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(cache_tbl)) return null;

            //查询符合条件的行集合(排序优先级别"QueueActive DESC,IsOver"
            DataRow[] JiaohaoTable_row_result =
                cache_tbl.Select(String.Format("RfidName='{0}'", ringNo), "IsOver,QueueActive DESC,IsNeedQueue");
            if (CSTR.IsArrayEmpty(JiaohaoTable_row_result)) return null;

            //构建返回表
            tblRet = new DataTable();
            tblRet = cache_tbl.Clone();//克隆表结构,但无数据
            foreach (DataRow row_JiaohaoTable in JiaohaoTable_row_result)
            {
                //Split本检查的ArrayProcedureStepName,并逐条添加到返回表
                String ArrayProcedureStepName = CSTR.ObjectTrim(row_JiaohaoTable["ArrayProcedureStepName"]);
                if (CSTR.isEmpty(ArrayProcedureStepName))
                {
                    //如果ArrayProcedureStepName无内容,直接Copy本条记录到返回表
                    DataRow newRow = Row_Copy(row_JiaohaoTable, tblRet);
                    tblRet.Rows.Add(newRow);
                    continue;
                }

                String[] procedureName_arr = ArrayProcedureStepName.Split(';');
                if (null == procedureName_arr || procedureName_arr.Length <= 1)
                {
                    //如果检查Item数量少于2个,直接Copy本条记录到返回表
                    DataRow newRow = Row_Copy(row_JiaohaoTable, tblRet);
                    tblRet.Rows.Add(newRow);
                    continue;
                }

                foreach(String procedureName in procedureName_arr)
                {
                    //把每个ProcedureStepName的数据Copy到新的行newRow
                    DataRow newRow = Row_Copy(row_JiaohaoTable, tblRet);
                    if (null != newRow)
                    {
                        newRow["ProcedureStepName"] = procedureName;
                        tblRet.Rows.Add(newRow);
                    }
                }

            }//end foreach (DataRow row in JiaohaoTable_row_result)

            return tblRet;
        }

        public static DataRow[] TJ_Exam_Get_Same_Room_Item_Form_JiaohaoExamInfo_By_RingNo(String ringNo, String roomID)
        {
            DataTable tbl = ExamInfo_get_All_Items_by_RingNo(ringNo);
            if (CSTR.IsTableEmpty(tbl)) return null;

            String sql = String.Format("RoomID='{0}'", roomID);

            return tbl.Select(sql);
        }

        //把row的数据copy到新的一行,并返回,结构按照tbl的
        public static DataRow Row_Copy(DataRow row, DataTable tbl)
        {
            if (null == row) return null;
            if (null == tbl) return null;//tbl.Rows可以为0

            DataRow newRow = tbl.NewRow();
            Object[] data = row.ItemArray;
            if (null == data) return null;

            for (int i = 0; i < data.Length; i++)
            {
                newRow[i] = data[i];
            }

            return newRow;
        }
        public static DataTable makeJiaoTable(DataRow[] rows)
        {
            if (null == rows) return null;
            if (rows.Length <= 0) return null;

            DataTable tbl = new DataTable();
            tbl = tbl_JiaohaoTable.Clone();

            foreach (DataRow row in rows)
            {
                tbl.ImportRow(row);
            }

            return tbl;
        }

        /**
        * 获取指定DJLSH的当前活动检查
        * 使用:
        * FinalCheckWnd.cs在显示检查列表时调用
        * 
        * **/
        //获取指定DJLSH的当前活动检查
        public static DataRow[] TJ_Queue_Arrange_Get_Activated_Exam(String DJLSH)
        {
            DJLSH = CSTR.trim(DJLSH);
            if (DJLSH.Length <= 0) return null;

            String sql = String.Format("QueueActive='1' And status='0' And IsChecking='0' And IsOver='0' " +
                "And DJLSH='{0}'", DJLSH);

            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        public static DataRow[] TJ_Queue_Arrange_Get_Checking_or_Activated_Exam(String DJLSH)
        {
            DJLSH = CSTR.trim(DJLSH);
            if (DJLSH.Length <= 0) return null;

            //包括状态串为 1000 与 1110 的检查
            String sql = String.Format("QueueActive='1' And DJLSH='{0}'", DJLSH);

            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        /**
         * 获取指定QueueID的活动检查在指定房间的队列次序(前面还有?人)
         * 使用:
         * FinalCheckWnd.cs在显示检查列表时调用
         * 
         * **/
        public static int TJ_Queue_Arrange_Get_Position_In_Specified_Room_Actual_Pending_Queue(String roomID, String queueID)
        {
            //String sql = String.Format("QueueActive=1 And status=0 And IsChecking=0 And RoomID='{0}' And QueueID<{1}", roomID, queueID);
            String sql = String.Format("QueueActive='1' And RoomID='{0}' And QueueID<'{1}'", roomID, queueID);
            DataTable cache_tbl = DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(cache_tbl)) return -1;

            DataRow[] row_result = cache_tbl.Select(sql);
            if (CSTR.IsArrayEmpty(row_result)) return 0;

            return row_result.Length;
        }
        #endregion FinalCheckWnd专属查询----------------------------------------End

        #region JiaohaoTable专属查询--------------------------------------------Start
        //获取指定行的字段值: JiaohaoTable_get_Field_String_by_IndexID("63","status")
        public static String JiaohaoTable_Get_Field_Value_by_IndexID(String indexID, String fieldName)
        {
            indexID = CSTR.trim(indexID);
            fieldName = CSTR.trim(fieldName);
            if (CSTR.isEmpty(indexID)) return "";
            if (CSTR.isEmpty(fieldName)) return "";

            //获取JiaohaoTable表
            DataTable cache_tbl = DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(cache_tbl)) return null;
            //查询符合条件的行集合
            DataRow[] row_result = cache_tbl.Select(String.Format("IndexID='{0}'", indexID));
            if (CSTR.IsArrayEmpty(row_result)) return "";
            //从第一行中返回指定的列数据
            return CSTR.ObjectTrim(row_result[0][fieldName]);
        }

        //获取指定IndexID的JiaohaoTable一条记录(不分状态,全部)--------------------------------------------------
        public static DataRow[] JiaohaoTable_Get_One_Item_By_IndexID(String indexID)
        {
            String sql = String.Format("IndexID='{0}'", indexID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }
        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public static DataRow[] JiaohaoTable_Get_All_Exam_By_DJLSH(String DJLSH)
        {
            String sql = String.Format("DJLSH='{0}'", DJLSH);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public static DataRow[] JiaohaoTable_Get_All_Exam_By_RfidNo(String RfidNo)
        {
            String sql = String.Format("Rfid='{0}'", RfidNo);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        //根据手环号码获取检查列表(不分状态,全部)--------------------------------------------------
        public static DataRow[] JiaohaoTable_Get_All_Exam_By_ringNo(String ringNo)
        {
            String sql = String.Format("RfidName='{0}'", ringNo);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        #endregion JiaohaoTable专属查询-----------------------------------------End

        #region TV_Shower专属查询-----------------------------------------------Start

        public static DataRow[] Exam_Get_Doing()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("QueueActive='1' and status='1' and IsChecking='1' and IsOver='0' and RoomID='{0}'", strRoomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            //order by QueueID
            return tbl.Select(sql, "QueueID DESC");
        }

        public static DataRow[] Exam_Get_Pending()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("QueueActive='1' and RoomID='{0}' and status='0' and IsChecking='0' and IsOver='0'", strRoomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            //order by QueueID
            return tbl.Select(sql, "QueueID");
        }

        public static DataRow[] Exam_Get_Unfinished_Exams()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("RoomID='{0}' and IsOver='0'", strRoomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            //order by QueueID
            return tbl.Select(sql, "QueueID");
        }

        public static DataRow Exam_Get_Pending_First_One()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("QueueActive='1' and RoomID='{0}' and status='0' and IsChecking='0' and IsOver='0'", strRoomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            //order by QueueID
            DataRow[] rows = tbl.Select(sql, "QueueID");
            if (CSTR.IsArrayEmpty(rows)) return null;

            return rows[0];
        }

        // 获取本房间的当前正在检查的信息(当前的,不呼叫,用于显示正在检查)(对于234室,返回结果可能为多条)
        public static DataRow[] Exam_Get_Doing_To_Show()
        {
            //取得本进程对应的ModalityID(room_id)
            String strRoomID = CONFIG.system_param["room_id"];
            //IsSaid初始为0,在显示完信息后设置为1
            String sql = String.Format("RoomID='{0}' and status='1' and IsChecking='1'", strRoomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            //order by IsSaid
            return tbl.Select(sql, "IsSaid");
        }

        #endregion TV_Shower专属查询--------------------------------------------End

        #region RoomInfo专属查询------------------------------------------------Start
        public static bool RoomInfo_is_need_queue(String roomID)
        {
            String sql = String.Format("RoomID='{0}'", roomID);
            DataRow[] sel_rows = DB_Get_JiaohaoRoomInfo_table().Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return false;

            String strValue = CSTR.ObjectTrim(sel_rows[0]["IsNeedQueue"]);

            //return (strValue.Equals("1")) ? true : false;
            return (strValue.Equals("0")) ? false : true;
        }
        public static bool RoomInfo_is_need_voice(String roomID)
        {
            String sql = String.Format("RoomID='{0}'", roomID);
            DataRow[] sel_rows = DB_Get_JiaohaoRoomInfo_table().Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return false;

            String strValue = CSTR.ObjectTrim(sel_rows[0]["IsNeedVoice"]);

            //return (strValue.Equals("1")) ? true : false;
            return (strValue.Equals("0")) ? false : true;
        }
        public static bool RoomInfo_is_active(String roomID)
        {
            String sql = String.Format("RoomID='{0}'", roomID);
            DataRow[] sel_rows = DB_Get_JiaohaoRoomInfo_table().Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return false;

            String strValue = CSTR.ObjectTrim(sel_rows[0]["RoomState"]);

            return (strValue.Equals("active")) ? true : false;
        }
        public static String RoomInfo_get_OptionRoom(String roomID)
        {
            String sql = String.Format("RoomID='{0}'", roomID);
            DataRow[] sel_rows = DB_Get_JiaohaoRoomInfo_table().Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return "";

            String strValue = CSTR.ObjectTrim(sel_rows[0]["OptionRoom"]);

            return strValue;
        }
        public static String RoomInfo_get_BackupRoom(String roomID)
        {
            String sql = String.Format("RoomID='{0}'", roomID);
            DataRow[] sel_rows = DB_Get_JiaohaoRoomInfo_table().Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return "";

            String strValue = CSTR.ObjectTrim(sel_rows[0]["BackupRoom"]);

            return strValue;
        }
        #endregion RoomInfo专属查询---------------------------------------------End

        #region DocBench专属查询------------------------------------------------Start
        /// 获取本房间的相同RFID的信息(不管状态)
        public static DataRow[] Exam_Get_All_Exam_Ingone_Status_SameRoom_SameRFID(String strRFID)
        {
            strRFID = CSTR.trim(strRFID);
            if (strRFID.Length <= 0) return null;

            //取得本进程对应的room_id
            String strRoomID = CONFIG.system_param["room_id"];
            String sql = String.Format("RoomID='{0}' AND Rfid='{1}'", strRoomID, strRFID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            return tbl.Select(sql);
        }

        public static String Exam_Get_RingNo_By_RfidNo(String strRFID)
        {
            strRFID = CSTR.trim(strRFID);
            if (strRFID.Length <= 0) return null;

            String sql = String.Format("Rfid='{0}' ", strRFID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            DataRow[] sel_rows = tbl.Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return null;

            return CSTR.ObjectTrim(sel_rows[0]["RfidName"]);
        }

        public static DataRow[] Exam_Get_Single_Exam_By_IndexID(String strIndexID)
        {
            String sql = String.Format("IndexID='{0}'", strIndexID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            DataRow[] sel_rows = tbl.Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return null;

            return sel_rows;
        }
        //获取指定房间当前已分配的未完成检查数

        #endregion DocBench专属查询---------------------------------------------End

        #region 队列数目查询----------------------------------------------------Start
        //获取指定房间的Pending的队列总数,忽略QueueActive状态(use case:在新到检时需要知道各个房间的总检查,而非活动检查)
        public static int Queue_Length_Count_Specified_Room_Ignore_QueueActive(String roomID)
        {
            //只要IsOver=0就属于未完成检查
            String sql = String.Format("IsOver='0' And RoomID='{0}'", roomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return 0;

            DataRow[] sel_rows = tbl.Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return 0;

            return sel_rows.Length;
        }

        //获取指定房间的Pending的实际队列总数(包括1000,与1110:Active与在检查)
        public static int Queue_Length_Count_Specified_Room_Active_Exam(String roomID)
        {
            //包含状态字为 1000,1110的检查,即只要QueueActive=1,IsOver=0
            String sql = String.Format("QueueActive='1' And IsOver='0' And RoomID='{0}'", roomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return 0;

            DataRow[] sel_rows = tbl.Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return 0;

            return sel_rows.Length;
        }

        //获取指定房间的过号检查队列总数(过号)
        public static int Queue_Length_Count_Specified_Room_Passed_Exam(String roomID)
        {
            //包含状态字为 1000,1110的检查,即只要QueueActive=1,IsOver=0
            String sql = String.Format("QueueActive='0' And status='1' And IsChecking='0' And IsOver='0' And RoomID='{0}'", roomID);
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return 0;

            DataRow[] sel_rows = tbl.Select(sql);
            if (CSTR.IsArrayEmpty(sel_rows)) return 0;

            return sel_rows.Length;
        }

        //获取多个房间的活动检查队列总数
        public static int Queue_Length_Count_MultiRoom_Cluster_Active_Exam(String clusterRooms)
        {
            //1.分解房间串
            String[] arrRooms = CSTR.splitRooms(clusterRooms);
            if (null == arrRooms) return 0;
            if (arrRooms.Length <= 0) return 0;

            //累加所有房间的队列长度
            int total_length = 0;
            foreach (String strRoom in arrRooms)
            {
                total_length += Queue_Length_Count_Specified_Room_Active_Exam(strRoom);
            }

            return total_length;
        }

        //获取多个房间中最少活动检查的队列人数
        public static int Queue_Length_Min_Count_MultiRoom_Active_Exam(String clusterRooms)
        {
            //1.分解房间串
            String[] arrRooms = CSTR.splitRooms(clusterRooms);
            if (null == arrRooms) return 0;
            if (arrRooms.Length <= 0) return 0;

            //遍历所有房间的队列长度,找到最小的那个
            int min_length = 9999;
            foreach (String strRoom in arrRooms)
            {
                int count = Queue_Length_Count_Specified_Room_Active_Exam(strRoom);
                if (count < min_length) min_length = count;
            }

            return min_length;
        }

        #endregion 队列数目查询-------------------------------------------------End

        //清除数据库(超过指定时间的数据)
        public static void DB_Delete_Outdate_Records()
        {
            String sql = "";
            DataTable tbl = null;
            BackupTable bk = null;

            //JiaohaoTable And JiaohaoExamInfo
            //删除前先备份
            sql = "Select * From JiaohaoTable Where abs(timestampdiff(HOUR,now(),InsertTime))>8";
            tbl = queue.getDB().query(sql);
            bk = new BackupTable(queue.getDB(), "Backup_JiaohaoTable");
            bk.insert(tbl);
            //删除JiaohaoTable
            sql = "Delete From JiaohaoTable Where abs(timestampdiff(HOUR,now(),InsertTime))>8";
            queue.getDB().update(sql);

            //删除JiaohaoTTS
            sql = "Delete From JiaohaoTTS Where abs(timestampdiff(MINUTE,now(),InsertTime))>5";
            queue.getDB().update(sql);

            //删除trigger表数据
            sql = "Delete From trigger_Table Where abs(timestampdiff(HOUR,now(),InsertTime))>4";
            queue.getDB().update(sql);
            sql = "Delete From trigger_ExamInfo Where abs(timestampdiff(HOUR,now(),InsertTime))>4";
            queue.getDB().update(sql);
            sql = "Delete From trigger_RoomInfo Where abs(timestampdiff(HOUR,now(),InsertTime))>4";
            queue.getDB().update(sql);
        }

    }//end class
}//end namespace
