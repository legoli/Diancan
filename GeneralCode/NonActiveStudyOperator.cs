using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    public class NonActiveStudyOperator
    {
        private ExamQueue queue = new ExamQueue();
        private DataTable tbl = null;//JiaohaoTable
        private List<String> prev_Non_Active_DJLSH = null;//上次检测发现的无活动检查的DJLSH列表

        public void Check()
        {
            //1.获取最新的JiaohaoTable表数据
            String sql = "Select * from JiaohaoTable;";
            tbl = queue.getDB().query(sql);
            if (CSTR.IsTableEmpty(tbl))
            {
                prev_Non_Active_DJLSH = null;
                return;
            }

            //2.Distinct(DJLSH)
            List<String> lst_distinct_DJLSH = getDistinctDJLSH();
            if (lst_distinct_DJLSH.Count <= 0) return;

            //2.对每个DJLSH,过滤出有未完成项目,而且无活动检查,保存到lst_NonActiveExam之中
            List<String> lst_NonActiveExam = new List<string>();
            foreach (String strDJLSH in lst_distinct_DJLSH)
            {
                //System.Windows.Forms.MessageBox.Show(strDJLSH);

                if (Study_State_Check(strDJLSH) == 1)
                {
                    //System.Windows.Forms.MessageBox.Show(String.Format("{0}:Need Activat", strDJLSH));
                    //queue.TJ_Queue_Arrange_Search_And_Activate_One_Item_By_DJLSH(strDJLSH);
                    lst_NonActiveExam.Add(strDJLSH);
                }
                //else
                    //System.Windows.Forms.MessageBox.Show(String.Format("{0}:无需处理", strDJLSH));
            }

            //3.把本次获取的无活动列表与上次记录的无活动列表做比对,如果两次都出现,则做Activate
            int nowCount = lst_NonActiveExam.Count;
            int prevCount = 0;
            if (prev_Non_Active_DJLSH != null) prevCount = prev_Non_Active_DJLSH.Count;
            if (prevCount == 0 || nowCount == 0)
            {
                prev_Non_Active_DJLSH = lst_NonActiveExam;
                return;
            }

            List<String> lst_remain = new List<string>();
            for (int i = 0; i < nowCount; i++)
            {
                bool is_activated = false;
                for (int j = 0; j < prevCount; j++)
                {
                    if (lst_NonActiveExam[i].Equals(prev_Non_Active_DJLSH[j]))
                    {
                        is_activated = true;
                    }
                }

                if (is_activated)
                {
                    //启动Activate
                    queue.TJ_Queue_Arrange_Search_And_Activate_One_Item_By_DJLSH(lst_NonActiveExam[i]);
                }
                else
                {
                    lst_remain.Add(lst_NonActiveExam[i]);
                }
            }

            //刷新prev_Non_Active_DJLSH
            prev_Non_Active_DJLSH = lst_remain;
        }

        //获取JiaohaoTable中的Distinct(DJLSH)
        private List<String> getDistinctDJLSH()
        {
            List<String> list = new List<string>();

            if (CSTR.IsTableEmpty(tbl)) return list;

            DataView view = tbl.DefaultView;
            DataTable tbl_DJLSH = view.ToTable(true, "DJLSH");
            if (CSTR.IsTableEmpty(tbl_DJLSH)) return list;

            foreach (DataRow row in tbl_DJLSH.Rows)
            {
                list.Add(CSTR.ObjectTrim(row["DJLSH"]));
            }

            return list;
        }

        //获取指定DJLSH的所有记录数组
        private DataRow[] getStudy(String strDJLSH)
        {
            if (CSTR.IsTableEmpty(tbl)) return null;
            String sql = String.Format("DJLSH='{0}'", strDJLSH);
            return tbl.Select(sql);
        }

        //检测Study的状态
        //Return: 0:无需处理
        //1:需要Activate
        private int Study_State_Check(String strDJLSH)
        {
            DataRow[] rows = getStudy(strDJLSH);
            if (CSTR.IsArrayEmpty(rows)) return 0;//当成所有检查已完成

            bool is_has_active_exam = false;
            bool is_has_unchecked_exam = false;
            bool is_has_checking_exam = false;
            bool is_has_skip_exam = false;
            foreach (DataRow row in rows)
            {
                String IndexID = CSTR.ObjectTrim(row["IndexID"]);
                String QueueActive = CSTR.ObjectTrim(row["QueueActive"]);
                String status = CSTR.ObjectTrim(row["status"]);
                String IsChecking = CSTR.ObjectTrim(row["IsChecking"]);
                String IsOver = CSTR.ObjectTrim(row["IsOver"]);

                String status_cluster = String.Format("{0}{1}{2}{3}",
                    QueueActive, status, IsChecking, IsOver);

                if (status_cluster.Equals("0000"))
                {
                    is_has_unchecked_exam = true;//有未完成检查
                }
                else if (status_cluster.Equals("1000"))
                {
                    is_has_active_exam = true;//有活动检查
                    is_has_unchecked_exam = true;//有未完成检查
                }
                else if (status_cluster.Equals("1110"))
                {
                    //呼叫后,在CallTime中保存呼叫时间
                    is_has_active_exam = true;//有活动检查
                    is_has_unchecked_exam = true;//有未完成检查
                    is_has_checking_exam = true;//有IsChecking检查
                }
                else if (status_cluster.Equals("0100"))
                {
                    //设为过号后,有SkipTime时间
                    String SkipTime = CSTR.ObjectTrim(row["SkipTime"]);
                    try
                    {
                        DateTime skip = Convert.ToDateTime(SkipTime);
                        if ((DateTime.Now - skip).TotalSeconds > 120)
                        {
                            is_has_unchecked_exam = true;//有未完成检查
                            is_has_skip_exam = true;
                        }
                        else
                        {
                            is_has_active_exam = true;//有活动检查
                            is_has_unchecked_exam = true;//有未完成检查
                        }
                    }
                    catch
                    {

                    }


                }
                else if (status_cluster.Equals("0101"))
                {
                    //首次嘀卡,CardReadTime1有时间,而CardReadTime2/EndTime都为空
                    //第二次嘀卡CardReadTime2/EndTime都有时间
                    //如果没有第二次嘀卡而是直接完成,则CardReadTime2/EndTime为NULL,但是有EndTime时间
                    String EndTime = CSTR.ObjectTrim(row["EndTime"]);
                    String IsNeedQueue = CSTR.ObjectTrim(row["IsNeedQueue"]);

                    //如果只嘀卡一次并且是需要排队的检查,则认为当前检查未完成,属于活动检查
                    if (IsNeedQueue.Equals("1") && CSTR.isEmpty(EndTime))
                    {
                        is_has_active_exam = true;//有活动检查
                    }

                }
                else
                {
                    //非法状态,需要强行恢复
                    if (IsOver.Equals("1"))
                    {
                        String sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=1,IsChecking=0,IsOver=1,"+
                            "EndTime=NOW() Where IndexID={0}", IndexID);
                        queue.getDB().update(sql);
                    }
                    else
                    {
                        String sql = String.Format("Update JiaohaoTable Set QueueActive=0,status=0,IsChecking=0,IsOver=0," +
                             "CardReadTime1=null,CardReadTime2=null,EndTime=null Where IndexID={0}", IndexID);
                        queue.getDB().update(sql);

                        is_has_unchecked_exam = true;//有未完成检查
                    }
                }
            }

            //如果有未完成检查,而有没有活动检查,表示需要Activate
            if (is_has_unchecked_exam && is_has_active_exam == false) return 1;
            //如果存在超时的过号检查,表示需要Activate
            if (is_has_skip_exam) return 1;

            if (is_has_checking_exam) return 0;

            //如果全部检查已完成,无需处理
            if (is_has_unchecked_exam == false) return 0;

            return 0;
        }//end Study_State_Check()

    }//end class
}//end namespace
