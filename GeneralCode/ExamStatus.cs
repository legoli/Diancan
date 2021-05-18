using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    public class ExamStatus
    {
        public enum Status { Normal, Active, Calling, ReadOnce, CheckOver, Pass };

        //取得检查的状态串
        public static String getStatusCluser(DataRow row)
        {
            if (null == row) return "";

            String strCluster = String.Format("{0}{1}{2}{3}",
                            CSTR.ObjectTrim(row["QueueActive"]),
                            CSTR.ObjectTrim(row["status"]),
                            CSTR.ObjectTrim(row["IsChecking"]),
                            CSTR.ObjectTrim(row["IsOver"]));

            return strCluster;
        }

        public static Dictionary<Status, bool> getStatus(DataRow row)
        {
            if (null == row) return null;

            bool isNormal = false;
            bool isActive = false;
            bool isCalling = false;
            bool isReadOnce = false;
            bool isCheckOver = false;
            bool isPass = false;

            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("0000"))
            {
                isNormal = true;
            }
            else if (statusCluster.Equals("1000"))
            {
                isActive = true;
            }
            else if (statusCluster.Equals("1110"))
            {
                isCalling = true;
            }
            else if (statusCluster.Equals("0100"))
            {
                isPass = true;
            }
            else if (statusCluster.Equals("0101"))
            {
                //如果EndTime为空是读卡一次的检查
                if (CSTR.isEmpty(CSTR.ObjectTrim(row["EndTime"])))
                {
                    isReadOnce = true;
                }
                else
                {
                    isCheckOver = true;
                }
            }

            Dictionary<Status, bool> map = new Dictionary<Status, bool>();
            map.Add(Status.Normal, isNormal);
            map.Add(Status.Active, isActive);
            map.Add(Status.Calling, isCalling);
            map.Add(Status.Pass, isPass);
            map.Add(Status.ReadOnce, isReadOnce);
            map.Add(Status.CheckOver, isCheckOver);

            return map;
        }

        public static bool is_normal_exam(DataRow row)
        {
            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("0000")) return true;

            return false;
        }
        public static bool is_active_exam(DataRow row)
        {
            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("1000")) return true;

            return false;
        }
        public static bool is_checking_exam(DataRow row)
        {
            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("1110")) return true;

            return false;
        }
        public static bool is_pass_exam(DataRow row)
        {
            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("0100")) return true;

            return false;
        }
        public static bool is_finished_exam(DataRow row)
        {
            String statusCluster = getStatusCluser(row);
            if (statusCluster.Equals("0101")) return true;

            return false;
        }
    }
}
