using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    /**
     * DB_UltraSound3Detector类
     * 用于检测分配到[超声科]的检查是否已经完成
     * 方法:通过直接访问超声数据库,利用DJLSH来作查询,检测检查状态
     * Ris:ExamInfo.OrderHISNumber <-> US:[patient].patient_his_id(体检DJLSH)
     * 完成条件判断US:[study].study_state '已登记':未完成,其他状态:已完成
     * **/
    public class DB_UltraSound3Detector
    {
        public void check_and_set_over()
        {
            Dictionary<String, bool> result = check_us_exam_status();
            if (null == result) return;
            if (result.Count < 1) return;

            ExamQueue queue = new ExamQueue();
            foreach (String key in result.Keys)
            {
                //返回结果为true表示检查已经完成
                if (result[key])
                {
                    try
                    {
                        //设置对应的检查为已完成
                        String sql = String.Format("Update JiaohaoTable Set " +
                            "QueueActive=0,status=1,IsChecking=0,IsOver=1,IsNeedQueue=1,EndTime=NOW() " +//无EndTime则被认为正在检查中
                            "Where RoomID='[超声科]' And DJLSH='{0}'", key);
                        queue.getDB().update(sql);
                    }
                    catch (Exception exp)
                    {
                        System.Console.WriteLine(exp.Message);
                    }
                }
            }

        }

        /// <summary>
        /// check_us_exam_status()
        /// 用于查找超声系统中,指定的DJLSH的体检检查的完成状态
        /// </summary>
        /// <param name="lstDJLSH">需要查询的DJLSH列表</param>
        /// <returns>map('DJLSH',true/false) true表示已经完成,false表示不存在或未完成</returns>
        private Dictionary<String, bool> check_us_exam_status()
        {
            try
            {
                //获取[超声科]房间的未完成检查列表
                List<String> lstDJLSH = getUltrasoundExamList();
                if (CSTR.IsListEmpty(lstDJLSH)) return null;

                //生成检查状态sql语句
                String sql = generateQueryString(lstDJLSH);
                if (CSTR.isEmpty(sql)) return null;

                //执行查询
                SqlServerOperator us_db = new SqlServerOperator(true);
                DataTable tbl = us_db.query(sql);
                if (CSTR.IsTableEmpty(tbl)) return null;

                //构建返回map
                Dictionary<String, bool> map = new Dictionary<string, bool>();
                foreach (DataRow row in tbl.Rows)
                {
                    //获取DJLSH
                    String DJLSH = CSTR.ObjectTrim(row["patient_his_id"]);
                    bool is_over = CSTR.ObjectTrim(row["study_state"]).Equals("已登记") ? false : true;
                    if (CSTR.isEmpty(DJLSH)) continue;

                    //在map.key中查找是否存在,如果没有则添加
                    bool is_key_exist = false;
                    foreach (String key in map.Keys)
                    {
                        if (DJLSH.Equals(key)) is_key_exist = true;
                    }

                    if (is_key_exist)
                    {
                        //多个相同DJLSH只要有一个已完成就OK
                        bool is_over_exist = map[DJLSH];
                        if (false == is_over_exist && true == is_over)
                        {
                            map[DJLSH] = true;
                        }
                    }
                    else
                    {
                        //new,加入到map
                        map[DJLSH] = is_over;
                    }
                }

                return map;
            }
            catch(Exception exp)
            {
                System.Console.WriteLine(exp.Message);
            }

            //出错返回null值
            return null;
        }

        //获取系统中未完成的[超声科]房间的检查列表
        private List<String> getUltrasoundExamList()
        {
            DataTable tbl = DatabaseCache.DB_Get_JiaohaoTable_table();
            if (CSTR.IsTableEmpty(tbl)) return null;

            DataRow[] rows = tbl.Select("RoomID='[超声科]' and IsOver='0'");
            if (CSTR.IsRowArrEmpty(rows)) return null;

            List<String> list = new List<string>();
            foreach (DataRow row in rows)
            {
                String DJLSH = CSTR.ObjectTrim(row["DJLSH"]);
                if (CSTR.isEmpty(DJLSH)) continue;
                DJLSH = DJLSH.Replace("'", "");
                list.Add(DJLSH);
            }

            return list;
        }

        //构建查询sql
        private String generateQueryString(List<String> lstDJLSH)
        {
            if (CSTR.IsListEmpty(lstDJLSH)) return "";

            //把list中的DJLSH拼接成'11','22'格式
            String DJLSH_cluster = "";
            foreach (String item in lstDJLSH)
            {
                if (CSTR.isEmpty(item)) continue;

                if (DJLSH_cluster.Length > 0) DJLSH_cluster += ",";
                DJLSH_cluster += String.Format("'{0}'", item);
            }
            if (CSTR.isEmpty(DJLSH_cluster)) return "";

            //生成查询语句
            String sql = String.Format("select [patient].patient_his_id,[patient].name," +
                            "[study].study_state,[study].study_begin_time,[study].study_end_time " +
                            "from [patient],[order],[study] " +
                            "where [order].patient_id=[patient].id " +
                            "and [order].id=[study].order_id " +
                            "and [order].register_time>getdate()-1 " +
                            "and [patient].patient_his_id in ({0}) ", DJLSH_cluster);
            return sql;
        }
    }
}
