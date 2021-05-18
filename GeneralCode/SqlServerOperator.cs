using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

namespace GeneralCode
{
    /**
     * 项目中需要：添加引用
     * 在 程序集/扩展/Mysql.Data(多个选其中一个打勾)
     * 部署时如果不能访问数据库，可能缺少驱动，需运行mysql-connector-net-6.9.5.msi
     * **/
    public class SqlServerOperator
    {
        //链接字符串(默认)
        private String connectString = "user id=sa;password=;" +
                                        "initial catalog=tj_xlms;Server=192.168.1.209;" +
                                        "Connect Timeout=30";
        //链接字符串(超声数据库)
        private String us_connectString = "user id=zzq01;password=gepacs15;" +
                                "initial catalog=UsPacsDb;Server=192.168.3.206;" +
                                "Connect Timeout=30";


        private SqlConnection conn = null;

        //默认构造函数
        public SqlServerOperator()
        {
            //使用默认的链接串
        }

        //超声数据库链接构造函数
        public SqlServerOperator(bool is_ultra_sound_db)
        {
            if (is_ultra_sound_db)
            {
                //把默认连接串替换为超声数据库
                connectString = us_connectString;
            }
        }

        //建立连接并返回连接句柄
        private SqlConnection getConn()
        {
            if (null == conn)
            {
                try
                {
                    conn = new SqlConnection(connectString);
                    conn.Open();
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
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
                //查询Sql
                SqlDataAdapter adapter = new SqlDataAdapter(sql, getConn());
                //把返回结果填入DataSet
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                //关闭本次查询
                adapter.Dispose();

                if (null != ds.Tables[0]) ret = ds.Tables[0];
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                conn = null;
            }

            return ret;
        }

        /// <summary>
        /// Update数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <returns>影响行数 -1:表示执行错误</returns>
        public int update(String sql)
        {
            int nCount = -1;
            try
            {
                //建立命令
                SqlCommand command = new SqlCommand(sql, getConn());
                //执行update,并返回"影响行数"
                nCount = command.ExecuteNonQuery();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                conn = null;
                nCount = -1;
            }

            return nCount;
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
        public int update(String sql,Dictionary<String,String>mapParam)
        {
            int nCount = -1;
            try
            {
                //建立命令
                SqlCommand command = new SqlCommand(sql, getConn());

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
                conn = null;
                nCount = -1;
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
                SqlDataAdapter adapter = new SqlDataAdapter(sql, getConn());
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

    }//end class MysqlOperator
}//end namespace
