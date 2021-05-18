using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    public class DB_AutoFetcher : ILightThreadable
    {
        //时钟总控,用于同步主程序中的timer_stop()与timer_start()
        public static bool stop_fetch = false;

        private ExamQueue queue = new ExamQueue();
        private JSON json = new JSON();

        //保存三个trigger.id
        private Int32 sp_last_trigger_Table_id1 = 0;
        private Int32 sp_last_trigger_ExainInfo_id2 = 0;
        private Int32 sp_last_trigger_RoomInfo_id3 = 0;

        //保存最近一次更新JIaohaoTable和JiaohaoRoomInfo的时间,用于超时强行更新
        private DateTime sp_last_JiaohaoTable_updateTime = DateTime.Now;
        private DateTime sp_last_RoomInfo_updateTime = DateTime.Now;

        //启动Auto_Fetch_Thread
        public void run()
        {
            worker_init();
        }

        #region LightThread线程-------------------------------------------------------Start
        private LightThread worker_thread = null;
        private long worker_thread_loop_times = 0;
        private void worker_init()
        {
            //1秒循环一次,读取数据库的时间控制通过计数器
            worker_thread = new LightThread(this, 1 * 1000);
            worker_thread.IsBlockInProcessReport = true;//Report时阻塞线程
            worker_thread.run();//启动线程
        }

        private DateTime worker_main_JiaohaoTable_Reflesh = DateTime.Now;
        private DateTime worker_main_RoomInfo_Reflesh = DateTime.Now;
        private DateTime worker_main_Tocken_Reflesh = DateTime.Now;
        private DateTime worker_main_Ultrasound_Reflesh = DateTime.Now;
        public Object worker_main(Object e)
        {
            worker_thread_loop_times++;
            if (worker_thread_loop_times > 60000) worker_thread_loop_times = 0;

            //时钟总控,用于同步主程序中的timer_stop()与timer_start()
            if (stop_fetch) return null;

            //1.检测无活动检查的序列,并完成活动检查设置-------------------------------------------------Start
            if ((DateTime.Now - worker_main_Tocken_Reflesh).TotalSeconds > 60)
            {
                worker_main_Tocken_Reflesh = DateTime.Now;

                Non_ActiveExam_Check();
            }
            //检测无活动检查的序列,并完成活动检查设置---------------------------------------------------End

            //2.刷新房间开闭信息RoomInfo----------------------------------------------------------------Start
            if ((DateTime.Now - worker_main_RoomInfo_Reflesh).TotalSeconds > 17)
            {
                worker_main_RoomInfo_Reflesh = DateTime.Now;

                return update_RoomInfo();
            }
            //刷新房间开闭信息RoomInfo------------------------------------------------------------------End   

            //3.刷新JiaohaoTable和JiaohaoExamInfo-------------------------------------------------------Start
            if ((DateTime.Now - worker_main_JiaohaoTable_Reflesh).TotalSeconds > 6)
            {
                worker_main_JiaohaoTable_Reflesh = DateTime.Now;

                return update_JiaohaoTable_and_JiaohaoExamInfo();
            }
            //刷新JiaohaoTable和JiaohaoExamInfo---------------------------------------------------------End

            //4.检测[超声科]检查是否完成-------------------------------------------------Start
            if ((DateTime.Now - worker_main_Ultrasound_Reflesh).TotalSeconds > 120)
            {
                worker_main_Ultrasound_Reflesh = DateTime.Now;

                //超声科检查有完成的,则设置检查状态为已完成
                DB_UltraSound3Detector us_detec = new DB_UltraSound3Detector();
                us_detec.check_and_set_over();
            }
            //检测[超声科]检查是否完成---------------------------------------------------End


            return null;
        }
        public void worker_Report(Object dataObj)
        {
            //时钟总控,用于同步主程序中的timer_stop()与timer_start()
            if (stop_fetch) return;

            if (null == dataObj) return;

            Dictionary<String, DataTable> map = dataObj as Dictionary<String, DataTable>;
            //更新Cache数据库
            DatabaseCache.Worker_DB_Fetched(map);
        }
        public void worker_Completed(Object dataObj)
        {

        }

        //  刷新JiaohaoTable和JiaohaoExamInfo-------------------------------------------------------Start
        public Dictionary<String, DataTable> update_JiaohaoTable_and_JiaohaoExamInfo()
        {
            //获取指定的房间号(DocBench,TV_Shower有房间号,而其他则没有)
            //String roomID = DatabaseCache.RoomID_for_StoreProcedure;
            String roomID = "";//所有的更新均不区分房间

            //调用存储过程
            Dictionary<String, Object> map = queue.getDB().Call_SP_Jiaohao_Reflesh(
                        roomID, sp_last_trigger_Table_id1, sp_last_trigger_ExainInfo_id2);
            bool is_has_table = true;
            int flag = 0;
            if (null == map)
            {
                is_has_table = false;
            }
            else if (map.Count <= 0)
            {
                is_has_table = false;
            }
            else
            {
                flag = (int)map["flag"];
                if (flag <= 0) is_has_table = false;
            }

            //如果无有效结果集,启动强行更新设置
            if (false == is_has_table)
            {
                //如果超时无更新,则设置trigger.id在下一次做强行更新
                TimeSpan interval = DateTime.Now - sp_last_JiaohaoTable_updateTime;
                if (interval.TotalSeconds > 1800)
                {
                    sp_last_trigger_Table_id1 = 0;
                    sp_last_trigger_ExainInfo_id2 = 0;
                }

                return null;
            }

            //刷新trigger.id
            sp_last_trigger_Table_id1 = (int)map["max_id_1"];
            sp_last_trigger_ExainInfo_id2 = (int)map["max_id_2"];

            DataSet ds = (DataSet)map["data_set"];
            //当flag>0而ds为空表示JiaohaoTable中无内容,需要强行清空Cache
            bool is_JiaohaoTable_vanish = false;
            if (null == ds)
            {
                is_JiaohaoTable_vanish = true;
            }
            else if (ds.Tables.Count <= 0)
            {
                is_JiaohaoTable_vanish = true;
            }
            else
            {
                if (CSTR.IsTableEmpty(ds.Tables[0])) is_JiaohaoTable_vanish = true;
            }
            //判断是否JiaohaoTable已空
            if (flag > 0 && is_JiaohaoTable_vanish)
            {
                Dictionary<String, DataTable> mapEmpty = new Dictionary<string, DataTable>();
                mapEmpty.Add("JiaohaoTable", null);
                mapEmpty.Add("JiaohaoExamInfo", null);

                return mapEmpty;
            }

            //转换表格式
            DataTable sp_JiaohaoTable = ds.Tables[0];
            sp_JiaohaoTable = json.revertDataTable(json.toJson(sp_JiaohaoTable));

            //构建返回参数
            Dictionary<String, DataTable> mapTables = new Dictionary<string, DataTable>();
            mapTables.Add("JiaohaoTable", sp_JiaohaoTable);

            //刷新数据表的最新的Update时间
            sp_last_JiaohaoTable_updateTime = DateTime.Now;

            Log.log("AutoFecher JiaohaoExamInfo&JiaohaoTable");

            return mapTables;
        }
        //刷新JiaohaoTable和JiaohaoExamInfo---------------------------------------------------------End

        //1.刷新房间开闭信息RoomInfo----------------------------------------------------------------Start
        public Dictionary<String, DataTable> update_RoomInfo()
        {
            //调用存储过程
            Dictionary<String, Object> map = queue.getDB().Call_SP_RoomInfo_Reflesh(sp_last_trigger_RoomInfo_id3);
            bool is_has_table = true;
            int flag = 0;
            if (null == map)
            {
                is_has_table = false;
            }
            else if (map.Count <= 0)
            {
                is_has_table = false;
            }
            else
            {
                flag = (int)map["flag"];
                if (flag <= 0) is_has_table = false;
            }

            //如果无有效结果集,启动强行更新设置
            if (false == is_has_table)
            {
                //如果超时无更新,则设置trigger.id在下一次做强行更新
                TimeSpan interval = DateTime.Now - sp_last_RoomInfo_updateTime;
                if (interval.TotalSeconds > 1800)
                {
                    sp_last_trigger_RoomInfo_id3 = 0;
                }

                return null;
            }

            //刷新trigger.id
            sp_last_trigger_RoomInfo_id3 = (int)map["max_id_1"];

            DataSet ds = (DataSet)map["data_set"];
            if (null == ds) return null;
            int tbl_count = ds.Tables.Count;
            if (tbl_count <= 0) return null;

            DataTable sp_RoomInfo = ds.Tables[0];
            if (CSTR.IsTableEmpty(sp_RoomInfo)) return null;

            //转换表格式
            sp_RoomInfo = json.revertDataTable(json.toJson(sp_RoomInfo));

            //构建返回参数
            Dictionary<String, DataTable> mapTables = new Dictionary<string, DataTable>();
            mapTables.Add("JiaohaoRoomInfo", sp_RoomInfo);

            //刷新数据表的最新的Update时间
            sp_last_RoomInfo_updateTime = DateTime.Now;

            Log.log("AutoFecher JiaohaoRoomInfo");

            return mapTables;
        }
        //刷新房间开闭信息RoomInfo------------------------------------------------------------------End

        //检测无活动检查的序列,并完成活动检查设置---------------------------------------------------Start
        NonActiveStudyOperator Non_ActiveExam_opt = new NonActiveStudyOperator();
        private void Non_ActiveExam_Check()
        {
            //如果没有主动设置Handler_Name,则不用执行
            if (CSTR.isEmpty(DatabaseCache.Tocken_Handler_Name_for_StoreProcedure)) return;

            //1.获取Tocken
            int tocken = queue.getDB().Call_SP_Token(DatabaseCache.Tocken_Handler_Name_for_StoreProcedure);
            if (1 != tocken)
            {
                //System.Windows.Forms.MessageBox.Show("Tocken is not available.");
                return;
            }

            //2.检测
            Non_ActiveExam_opt.Check();

            //执行数据库例行程序
            //System.Windows.Forms.MessageBox.Show("Tocken Fetched.");
        }
        //检测无活动检查的序列,并完成活动检查设置---------------------------------------------------End   

        #endregion LightThread线程-------------------------------------------------------End
    
    }//end class
}//end namespace
