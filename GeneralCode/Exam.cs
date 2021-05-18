using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace GeneralCode
{
    public class Exam : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private String patID, accessionID, patName, examFrom;
        private DataRow examRow = null;//JiaohaoTable中的一行检查信息
        private bool isButtonChecked = false;

        public Exam() { }
        public Exam(String pid, String name, String examfrom)
        {
            examRow = null;
            patID = pid;
            accessionID = pid;
            patName = name;
            examFrom = examfrom;
        }

        public Exam(DataRow row)
        {
            examRow = row;
        }
        public DataRow getValueDataRow()
        {
            return examRow;
        }
        public void setValue(DataRow row)
        {
            examRow = row;
            patID = "";
            accessionID = "";
            patName = "";
            examFrom = "";
            //激发事件
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PatientIntraID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PatientNameChinese"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PreExamFrom"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("IndexID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("QueueID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("InsertTime"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PatientGender"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("ExamID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("OrderID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("ExamAccessionID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("ModalityID"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("IsReorder"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("ReorderReason"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("IsRecall"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("status"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("InCheckingTip"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("InReadyTip"));
                this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("RfidName"));
            }

        }
        public void setValue(String pid, String name, String examfrom)
        {
            examRow = null;
            PatientIntraID = pid;
            ExamAccessionID = pid;
            PatientNameChinese = name;
            PreExamFrom = examfrom;
        }

        #region 定义已DataRow作为控件的BindingData
        private bool isNull(DataRow row)
        {
            if (null == row) return true;
            return false;
        }
        public String IndexID { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["IndexID"]); } }
        public String QueueID { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["QueueID"]); } }
        public String InsertTime { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["InsertTime"]); } }
        public String PatientGender { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["PatientGender"]); } }
        public String ExamID { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["ExamID"]); } }
        public String OrderID { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["OrderID"]); } }
        public String ModalityID { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["ModalityID"]); } }
        public String IsReorder { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["IsReorder"]); } }
        public String ReorderReason { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["ReorderReason"]); } }
        public String IsRecall { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["IsRecall"]); } }
        public String status { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["status"]); } }
        public String RfidName { get { return isNull(examRow) ? "" : CSTR.ObjectTrim(examRow["RfidName"]); } }
        #endregion


        public bool IsButtonChecked
        {
            get { return isButtonChecked; }
            set
            {
                isButtonChecked = value;
                //激发事件
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("IsButtonChecked"));
                }
            }
        }

        public String PatientIntraID
        {
            get { return isNull(examRow) ? patID : examRow["PatientIntraID"].ToString(); }
            set
            {
                patID = value;
                //激发事件
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PatientIntraID"));
                }
            }
        }
        public String ExamAccessionID
        {
            get { return isNull(examRow) ? accessionID : examRow["ExamAccessionID"].ToString(); }
            set
            {
                accessionID = value;
                //激发事件
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("ExamAccessionID"));
                }
            }
        }
        public String PatientNameChinese
        {
            get { return isNull(examRow) ? patName : examRow["PatientNameChinese"].ToString(); }
            set
            {
                patName = value;
                //激发事件
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PatientNameChinese"));
                }
            }
        }
        public String PreExamFrom
        {
            get { return isNull(examRow) ? examFrom : examRow["PreExamFrom"].ToString(); }
            set
            {
                examFrom = value;
                //激发事件
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("PreExamFrom"));
                }
            }
        }

        //重载ToString()
        public override String ToString()
        {
            String str = "";
            if (isNull(examRow))
            {
                str = String.Format("{0} {1} {2}", patID, patName, examFrom);
            }
            else
            {
                str = String.Format("{0} {1} {2}", 
                    examRow["PatientIntraID"], examRow["PatientNameChinese"], examRow["PreExamFrom"]);
            }
            return str;
        }
    }

}
