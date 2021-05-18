using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using System.Runtime.InteropServices;

namespace GeneralCode
{
    /**
     * 设计目的: 本类是获取RIS服务器时间,并设置本机时间,以达到工作站和服务器时间同步
     * 
     * 1.访问RIS数据库,获取当前时间
     * 2.把DateTime格式转换成Win32API的SYSTEMTIME格式
     * 3.调用dll完成系统时间设置
     * **/
    //ExamQueue        public const String db_ip = "192.168.3.211", db_user = "root", db_password = "root", db_database = "test_tj";

    public class SyncServerTime
    {
        public void setSystemTime()
        {
            try
            {
                //获取RIS服务器时间
                DateTime dt = getRISServerTime();
                //转换成API系统时间格式
                SYSTEMTIME systime = new SYSTEMTIME();
                systime.FromDateTime(dt);
                //调用Win32API设置系统时间
                Win32API.SetLocalTime(ref systime);
            }
            catch(Exception exp)
            {
                Console.Out.WriteLine(exp.Message);
            }
        }

        //获取RISServer的当前时间
        private DateTime getRISServerTime()
        {
            DateTime dt = new DateTime();

            try
            {            
                //1.连接服务器,取得时间
                ExamQueue queue = new ExamQueue();
                String sql = "select date_format(now(),'%Y') as nYear," +
                            "date_format(now(),'%m') as nMonth," +
                            "date_format(now(),'%d') as nDay," +
                            "date_format(now(),'%H') as nHour," +
                            "date_format(now(),'%i') as nMinute," +
                            "date_format(now(),'%s') as nSecond";
                DataTable tbl = queue.getDB().query(sql);
                if (CSTR.IsTableEmpty(tbl)) return dt;

                //2.转换
                String strYear = CSTR.ObjectTrim(tbl.Rows[0]["nYear"]);
                String strMonth = CSTR.ObjectTrim(tbl.Rows[0]["nMonth"]);
                String strDay = CSTR.ObjectTrim(tbl.Rows[0]["nDay"]);
                String strHour = CSTR.ObjectTrim(tbl.Rows[0]["nHour"]);
                String strMinute = CSTR.ObjectTrim(tbl.Rows[0]["nMinute"]);
                String strSecond = CSTR.ObjectTrim(tbl.Rows[0]["nSecond"]);

                if (CSTR.isEmpty(strYear) || CSTR.isEmpty(strMonth) || CSTR.isEmpty(strDay) ||
                    CSTR.isEmpty(strHour) || CSTR.isEmpty(strMinute) || CSTR.isEmpty(strSecond)) return dt;

                //3.生成DateTime格式时间
                int nYear = Int16.Parse(strYear);
                int nMonth = Int16.Parse(strMonth);
                int nDay = Int16.Parse(strDay);
                int nHour = Int16.Parse(strHour);
                int nMinute = Int16.Parse(strMinute);
                int nSecond = Int16.Parse(strSecond);

                dt = new DateTime(nYear, nMonth, nDay, nHour, nMinute, nSecond);
            }
            catch (Exception exp)
            {
                Console.Out.WriteLine(exp.Message);
            }


            return dt;
        }

    }


    public class Win32API
    {
        [DllImport("Kernel32.dll")]
        public static extern bool SetLocalTime( ref SYSTEMTIME Time );
        [DllImport("Kernel32.dll")]
        public static extern void GetLocalTime(ref SYSTEMTIME Time);
    }

    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
 
        /// <summary>
        /// 从System.DateTime转换。
        /// </summary>
        /// <param name="time">System.DateTime类型的时间。</param>
        public void FromDateTime(DateTime time)
        {
            wYear = (ushort)time.Year;
            wMonth = (ushort)time.Month;
            wDayOfWeek = (ushort)time.DayOfWeek;
            wDay = (ushort)time.Day;
            wHour = (ushort)time.Hour;
            wMinute = (ushort)time.Minute;
            wSecond = (ushort)time.Second;
            wMilliseconds = (ushort)time.Millisecond;
        }
        /// <summary>
        /// 转换为System.DateTime类型。
        /// </summary>
        /// <returns></returns>
        public DateTime ToDateTime()
        {
            return new DateTime(wYear, wMonth, wDay, wHour, wMinute, wSecond, wMilliseconds);
        }
        /// <summary>
        /// 静态方法。转换为System.DateTime类型。
        /// </summary>
        /// <param name="time">SYSTEMTIME类型的时间。</param>
        /// <returns></returns>
        public static DateTime ToDateTime(SYSTEMTIME time)
        {
            return time.ToDateTime();
        }
    }
}

