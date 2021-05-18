using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;
using System.Collections;
using System.IO;

namespace GeneralCode
{
    /**
     * 项目中需要：添加引用
     * 在 程序集/扩展/Mysql.Data(多个选其中一个打勾)
     * 部署时如果不能访问数据库，可能缺少驱动，需运行mysql-connector-net-6.9.5.msi
     * **/
    public class MysqlOperator
    {
        //链接字符串(默认)
        //private String connectString = "server=192.168.60.5;uid=risadmin;password=dragonris;Database=gecris";
        private String connectString = "server=localhost;uid=master;password=psyy123456;Database=diancan;Charset=utf8";
        //private String connectString = "server=192.168.3.202;uid=risadmin;password=dragonris;Database=gecris";
        //private String connectString = "server=rm-bp1o78n1h04dl3t44.mysql.rds.aliyuncs.com;uid=r56448z34r;password=liyulong_2016;Database=r56448z34r";
        private MySqlConnection conn = null;

        //默认构造函数
        public MysqlOperator()
        {
            //使用默认的链接串
        }

        //指定链接参数
        public MysqlOperator(String server, String uid, String password, String database)
        {
            //加Charset=utf8可以防止在插入Blob数据时出错
            //this.connectString = String.Format("server={0};uid={1};password={2};Database={3};Charset=utf8",
            //this.connectString = String.Format("Server={0};User Id={1};password={2};Database={3}",//效果一样
            //把Charset设为gbk,少一次转换
            this.connectString = String.Format("server={0};uid={1};password={2};Database={3};Charset=gbk",
                server, uid, password, database);
            Console.WriteLine(connectString);
        }

        //建立连接并返回连接句柄
        private MySqlConnection getConn()
        {          
            if (null == conn)
            {
                try
                {
                    conn = new MySqlConnection(connectString);
                    conn.Open();
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
                    Log.log("[mysql.getConn()] " + exp.Message);
                    conn = null;
                }
            }

            return conn;
        }


        //清除conn连接,为下一次查询做全新连接
        public void reset()
        {
            try
            {
                if (null != conn)
                {
                    conn.Close();
                    conn.Dispose();
                    conn = null;
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }

        //查询并返回结果
        public DataTable query(String sql)
        {
            DataTable ret = null;
            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return null;

                //查询Sql
                MySqlDataAdapter adapter = new MySqlDataAdapter(sql, getConn());
                
                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                //关闭本次查询
                //adapter.Dispose();

                if (null != ds.Tables[0]) ret = ds.Tables[0];
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.query()] " + exp.Message);
                Log.log(sql);
                conn = null;
            }

            return ret;
        }

        //影响行数 -1:表示执行错误
        public int update(String sql)
        {
            int nCount = -1;
            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return -1;

                //建立命令
                MySqlCommand command = new MySqlCommand(sql, getConn());
                //执行update,并返回"影响行数"
                nCount = command.ExecuteNonQuery();

            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.update()] " + exp.Message);
                Log.log(sql);

                conn = null;
                nCount = -1;
            }

            return nCount;
        }

        //影响行数 -1:表示执行错误(事务支持)
        public int update_use_Transaction(List<String> sql_list)
        {
            int nCount = -1;

            if (null == sql_list) return -1;
            if (sql_list.Count <= 0) return -1;

            try
            {
                //获取Connection
                MySqlConnection connection = getConn();
                if (null == connection) return -1;

                //生成事务
                MySqlTransaction trans = connection.BeginTransaction();

                //建立命令
                MySqlCommand cmd = connection.CreateCommand();

                //指定命令事务
                cmd.Transaction = trans;

                try
                {
                    foreach (String sql in sql_list)
                    {
                        if (CSTR.isEmpty(sql)) continue;

                        cmd.CommandText = sql;
                        //执行update,并返回"影响行数"
                        int n = cmd.ExecuteNonQuery();
                    }

                    //Commit(事务提交)
                    trans.Commit();
                    nCount = sql_list.Count;
                }
                catch (Exception exp_cmd)
                {
                    try
                    {
                        //Rollback(事务回滚)
                        trans.Rollback();
                    }
                    catch
                    {

                    }

                    Console.WriteLine(exp_cmd.Message);
                    conn = null;
                    nCount = -1;
                }
            }
            catch (Exception exp)
            {

                //Log出现的错误
                Console.WriteLine(exp.Message);
                Log.log("[mysql.update()] " + exp.Message);

                conn = null;
                nCount = -1;

            }

            return nCount;
        }



        //读取Blob数据
        public Object ReadBlob(String photoName)
        {
            if (CSTR.isEmpty(photoName)) return null;

            String sql = String.Format("Select * from JiaohaoPhoto where PhotoName='{0}'", photoName);
            MySqlCommand command = new MySqlCommand(sql, getConn());

            MySqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                MemoryStream buf = new MemoryStream((byte[])reader["PhotoData"]);
                return buf;
            }

            return null;
        }


        /// <summary>
        /// Update数据库(带参数),用于解决字符串中存在引号的情况
        /// </summary>
        /// <param name="sql"></param>
        /// <returns>影响行数 -1:表示执行错误</returns>
        /// <usage>
        ///MysqlOperator my = new MysqlOperator();
        ///String sql = "update ExamInfo set ExamArrivalTime=@Time,ExamStudyUID=@UID,ExamArrivalDate=@RQ Where ExamID=299999;";
        ///Dictionary<String, String> mapParam = new Dictionary<string, string>();
        ///mapParam.Add("@Time", "12:00:00");
        ///mapParam.Add("@UID", "1.2.3456");
        ///mapParam.Add("@RQ", "2011-04-12");
        ///int n = my.update(sql,mapParam);
        ///</usage>
        public int update(String sql, Dictionary<String, String> mapParam)
        {
            int nCount = -1;
            try
            {
                //建立命令
                MySqlCommand command = new MySqlCommand(sql, getConn());

                //参宿映射
                foreach (KeyValuePair<String, String> map in mapParam)
                {
                    String strInfo = String.Format("Key:{0} Value:{1}", map.Key, map.Value);
                    Console.WriteLine(strInfo);
                    //分别指定参数和值
                    //command.Parameters.Add(map.Key, MySqlDbType.String);
                    //command.Parameters[map.Key].Value = map.Value;
                    //同时指定参数和值
                    command.Parameters.AddWithValue(map.Key, map.Value);
                }
                //执行update,并返回"影响行数"
                nCount = command.ExecuteNonQuery();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.update(sql,mapParam)] " + exp.Message);
                Log.log(sql);

                conn = null;
                nCount = -1;
            }

            return nCount;
        }

        public int updateBlob(String sql, Object img)
        {
            int nCount = -1;
            try
            {
                //建立命令
                MySqlCommand command = new MySqlCommand(sql, getConn());

                //关联Blob数据
                command.Parameters.Add("@images", MySqlDbType.Blob).Value = img;

                //测试连接状态,必要时重新建立连接
                if (command.Connection.State == ConnectionState.Closed)
                {
                    command.Connection.Open();
                }

                //执行update,并返回"影响行数"
                nCount = command.ExecuteNonQuery();
            }
            catch (Exception exp)
            {
                nCount = -1;
                Console.WriteLine(exp.Message);
                Log.log("Blob error:" + exp.Message);
                conn = null;
            }

            return nCount;
        }

        /// <summary>
        /// 判断指定条件的记录是否存在
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public bool isExist(String sql)
        {
            bool ret = false;
            try
            {
                //查询Sql
                MySqlDataAdapter adapter = new MySqlDataAdapter(sql, getConn());
                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                //关闭本次查询
                adapter.Dispose();

                if (null != ds.Tables[0])
                {
                    if (ds.Tables[0].Rows.Count > 0) ret = true;
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                conn = null;
            }

            return ret;
        }

        public void bulk()
        {
            //参考文章:把Excel批量导入到Mysql
            //http://blog.csdn.net/zhou2s_101216/article/details/50875211
            MySqlBulkLoader bulk = new MySqlBulkLoader(getConn())
            {
                FieldTerminator = ",",
                FieldQuotationCharacter = '"',
                EscapeCharacter = '"',
                LineTerminator = "\r\n",
                FileName = "abc",
                NumberOfLinesToSkip = 0,
                TableName = "backup_tbl",
            };

        }

        public String Call_StoreProcedure_demo(String roomID)
        {
            String strRet = "";
            roomID = CSTR.trim(roomID);
            if (roomID.Length <= 0) return "";

            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return "";

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "demo2";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //in - roomID
                MySqlParameter parameter_room_id = new MySqlParameter("?room_ID", MySqlDbType.VarChar, 20);
                parameter_room_id.Value = roomID;
                adapter.SelectCommand.Parameters.Add(parameter_room_id);

                //out - count(*)
                MySqlParameter parameter_exam_count = new MySqlParameter("?roomCount", MySqlDbType.VarChar, 20);
                parameter_exam_count.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_exam_count);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //查看是否有返回的表
                int nTableCount = ds.Tables.Count;
                if (nTableCount > 0)
                {
                    //即使存储过程中未返回表,Tables.Count也会为1,但为空表
                    DataTable tbl = ds.Tables[0];
                    if (CSTR.IsTableEmpty(tbl) == false)
                    {
                        foreach (DataRow row in tbl.Rows)
                        {
                            System.Windows.Forms.MessageBox.Show(CSTR.ObjectTrim(row[0]));
                        }
                    }
                }

                //取得Out参数
                strRet = CSTR.ObjectTrim(parameter_exam_count.Value);

            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.call_storeProcedure()] " + exp.Message);

                conn = null;
                strRet = "";
            }

            return strRet;
        }

        //定时刷新叫号表数据
        public Dictionary<String, Object> Call_SP_Jiaohao_Reflesh(String roomID, int last_trigger_Table_id, int last_trigger_ExamInfo_id)
        {
            Dictionary<String, Object> map = new Dictionary<string, object>();

            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return map;

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "Reflesh_Jiaohao_Info";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //in - roomID
                MySqlParameter parameter_room_id = new MySqlParameter("?in_room_id", MySqlDbType.VarChar, 20);
                parameter_room_id.Value = roomID;
                adapter.SelectCommand.Parameters.Add(parameter_room_id);

                //inout - in_trigger_Table_id
                MySqlParameter parameter_in_trigger_Table_id = new MySqlParameter("?in_trigger_Table_id", MySqlDbType.Int32, 11);
                parameter_in_trigger_Table_id.Direction = ParameterDirection.InputOutput;
                parameter_in_trigger_Table_id.Value = last_trigger_Table_id;
                adapter.SelectCommand.Parameters.Add(parameter_in_trigger_Table_id);

                //inout - in_trigger_ExamInfo_id
                MySqlParameter parameter_in_trigger_ExamInfo_id = new MySqlParameter("?in_trigger_ExamInfo_id", MySqlDbType.Int32, 11);
                parameter_in_trigger_ExamInfo_id.Direction = ParameterDirection.InputOutput;
                parameter_in_trigger_ExamInfo_id.Value = last_trigger_ExamInfo_id;
                adapter.SelectCommand.Parameters.Add(parameter_in_trigger_ExamInfo_id);

                //out - out_reflesh_flag
                MySqlParameter parameter_out_reflesh_flag = new MySqlParameter("?out_reflesh_flag", MySqlDbType.Int32, 11);
                parameter_out_reflesh_flag.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_out_reflesh_flag);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //取得Out参数
                int result_flag = CSTR.convertToInt(CSTR.ObjectTrim(parameter_out_reflesh_flag.Value));
                int result_max_id1 = CSTR.convertToInt(CSTR.ObjectTrim(parameter_in_trigger_Table_id.Value));
                int result_max_id2 = CSTR.convertToInt(CSTR.ObjectTrim(parameter_in_trigger_ExamInfo_id.Value));

                //构建返回的映射表
                map.Add("flag", result_flag);
                map.Add("max_id_1", result_max_id1);
                map.Add("max_id_2", result_max_id2);
                map.Add("data_set", ds);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.Call_SP_Jiaohao_Reflesh()] " + exp.Message);

                conn = null;
                map = null;
            }

            return map;
        }
        //定时刷新RoomInfo表数据
        public Dictionary<String, Object> Call_SP_RoomInfo_Reflesh(int last_trigger_RoomInfo_id)
        {
            Dictionary<String, Object> map = new Dictionary<string, object>();

            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return map;

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "Reflesh_RoomInfo";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //inout - in_trigger_RoomInfo_id
                MySqlParameter parameter_in_trigger_RoomInfo_id = new MySqlParameter("?in_trigger_RoomInfo_id", MySqlDbType.Int32, 11);
                parameter_in_trigger_RoomInfo_id.Direction = ParameterDirection.InputOutput;
                parameter_in_trigger_RoomInfo_id.Value = last_trigger_RoomInfo_id;
                adapter.SelectCommand.Parameters.Add(parameter_in_trigger_RoomInfo_id);

                //out - out_reflesh_flag
                MySqlParameter parameter_out_reflesh_flag = new MySqlParameter("?out_reflesh_flag", MySqlDbType.Int32, 11);
                parameter_out_reflesh_flag.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_out_reflesh_flag);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //取得Out参数
                int result_flag = CSTR.convertToInt(CSTR.ObjectTrim(parameter_out_reflesh_flag.Value));
                int result_max_id1 = CSTR.convertToInt(CSTR.ObjectTrim(parameter_in_trigger_RoomInfo_id.Value));

                //构建返回的映射表
                map.Add("flag", result_flag);
                map.Add("max_id_1", result_max_id1);
                map.Add("data_set", ds);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.Call_SP_RoomInfo_Reflesh()] " + exp.Message);

                conn = null;
                map = null;
            }

            return map;
        }
        //Token获取
        public int Call_SP_Token(String strHandlerName)
        {
            if (CSTR.isEmpty(strHandlerName)) return 0;

            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return 0;

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "Tocken";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //in - parameter_Handler_Name
                MySqlParameter parameter_Handler_Name = new MySqlParameter("?Handler_Name", MySqlDbType.VarChar, 128);
                parameter_Handler_Name.Direction = ParameterDirection.Input;
                parameter_Handler_Name.Value = strHandlerName;
                adapter.SelectCommand.Parameters.Add(parameter_Handler_Name);

                //out - out_token_flag
                MySqlParameter parameter_out_token_flag = new MySqlParameter("?out_token_flag", MySqlDbType.Int32, 11);
                parameter_out_token_flag.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_out_token_flag);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //取得Out参数
                int result_flag = CSTR.convertToInt(CSTR.ObjectTrim(parameter_out_token_flag.Value));

                return result_flag;
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.Call_SP_Token()] " + exp.Message);

                conn = null;
            }

            return 0;
        }

        //设置指定的IndexID记录为活动检查
        public bool Call_SP_Activate_Specified_IndexID(String strIndexID)
        {
            strIndexID = CSTR.trim(strIndexID);
            if (strIndexID.Length <= 0) return false;
            int nIndexID = CSTR.convertToInt(strIndexID);
            if (nIndexID <= 0) return false;

            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return false;

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "TJ_Queue_Activate_Specified_IndexID";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //in - parameter_Handler_Name
                MySqlParameter parameter_in_index_id = new MySqlParameter("?in_index_id", MySqlDbType.Int32, 11);
                parameter_in_index_id.Direction = ParameterDirection.Input;
                parameter_in_index_id.Value = nIndexID;
                adapter.SelectCommand.Parameters.Add(parameter_in_index_id);

                //out - out_result_flag
                MySqlParameter parameter_out_result_flag = new MySqlParameter("?out_result_flag", MySqlDbType.Int32, 11);
                parameter_out_result_flag.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_out_result_flag);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //取得Out参数
                int result_flag = CSTR.convertToInt(CSTR.ObjectTrim(parameter_out_result_flag.Value));

                return (result_flag == 1) ? true : false;
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.Call_SP_Activate_Specified_IndexID()] " + exp.Message);

                conn = null;
            }

            return false;
        }
        //语音叫号获取一条播报的信息
        public DataTable Call_SP_TTS_Get_One_Item_To_Speak()
        {
            try
            {
                MySqlConnection connection = getConn();
                if (null == connection) return null;

                //指定存储过程名称和连接
                MySqlDataAdapter adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand();
                adapter.SelectCommand.Connection = connection;
                adapter.SelectCommand.CommandText = "TTS_Get_One_Item_To_Speak";//存储过程名称
                adapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                //设置参数  //mysql的存储过程参数是以?打头的！！！！
                //in - parameter_Handler_Name
                //MySqlParameter parameter_in_index_id = new MySqlParameter("?in_index_id", MySqlDbType.Int32, 11);
                //parameter_in_index_id.Direction = ParameterDirection.Input;
                //parameter_in_index_id.Value = nIndexID;
                //adapter.SelectCommand.Parameters.Add(parameter_in_index_id);

                //out - out_result_flag
                MySqlParameter parameter_out_result_flag = new MySqlParameter("?out_result_flag", MySqlDbType.Int32, 11);
                parameter_out_result_flag.Direction = ParameterDirection.Output;
                adapter.SelectCommand.Parameters.Add(parameter_out_result_flag);

                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);

                //取得Out参数
                int result_flag = CSTR.convertToInt(CSTR.ObjectTrim(parameter_out_result_flag.Value));

                //返回结果不为1,表示没有任何结果集
                if (1 != result_flag) return null;
                if (null == ds) return null;
                if (ds.Tables.Count <= 0) return null;

                //返回获取的一条记录
                return ds.Tables[0];
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Log.log("[mysql.Call_SP_TTS_Get_One_Item_To_Speak()] " + exp.Message);

                conn = null;
            }

            return null;
        }


    }//end class MysqlOperator
}//end namespace
