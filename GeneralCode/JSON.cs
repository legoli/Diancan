using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace GeneralCode
{
    //添加引用:右键你项目的Reference（引用），然后添加，[框架]选System.Web.Extensions
    public class JSON
    {
        //---------<<< 封装 >>>--------------------------------------------------------------------
        public String toJson(DataTable tbl)
        {
            String strRet = "";
            if (CSTR.IsTableEmpty(tbl)) return "";

            try
            {
                JavaScriptSerializer json = new JavaScriptSerializer();
                json.MaxJsonLength = Int32.MaxValue;//设置JSON串的最大值

                //封装列名
                List<String> columnList = new List<string>();
                foreach (DataColumn column in tbl.Columns)
                {
                    columnList.Add(CSTR.trim(column.ColumnName));
                }
                int columnCount = columnList.Count;

                //封装字段内容
                List<List<String>> rowList = new List<List<string>>();
                foreach (DataRow row in tbl.Rows)
                {
                    List<String> tempList = new List<string>();
                    for (int i = 0; i < columnCount; i++)
                    {
                        tempList.Add(CSTR.ObjectTrim(row[columnList[i]]));
                    }
                    rowList.Add(tempList);//添加一行记录
                }

                //总装
                Dictionary<String, Object> tblMap = new Dictionary<string, object>();
                tblMap.Add("column", columnList);
                tblMap.Add("data", rowList);
                strRet = json.Serialize(tblMap);  //返回一个json字符串
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                strRet = "";
            }

            return strRet;
        }

        //---------<<< 还原 >>>--------------------------------------------------------------------
        public DataTable revertDataTable(String strJson)
        {
            DataTable tblRet = null;
            strJson = CSTR.trim(strJson);
            if (strJson.Length <= 0) return null;

            try
            {
                JavaScriptSerializer json = new JavaScriptSerializer();
                json.MaxJsonLength = Int32.MaxValue;//设置JSON串的最大值

                //解析json字符串
                Dictionary<String, Object> tblMap = json.Deserialize<Dictionary<String, Object>>(strJson);
                if (CSTR.IsDictionaryEmpty(tblMap)) return null;

                //必须使用ArrayList,当用List<String>时返回为null(原因不明)
                ArrayList columnList = tblMap["column"] as ArrayList;
                if (CSTR.IsListEmpty(columnList)) return null;

                ArrayList rowList = tblMap["data"] as ArrayList;
                if (CSTR.IsListEmpty(rowList)) return null;

                //重建DataTable
                tblRet = new DataTable();
                //重建column
                foreach (String column in columnList)
                {
                    tblRet.Columns.Add(column);
                }

                //填充数据
                int nCloumnCount = columnList.Count;
                foreach (ArrayList list in rowList)
                {
                    DataRow newRow = tblRet.NewRow();
                    for (int i = 0; i < nCloumnCount; i++)
                    {
                        newRow[i] = list[i];
                    }
                    tblRet.Rows.Add(newRow);//循环添加行
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                tblRet = null;
            }

            return tblRet;
        }

    }//end class
}//end namespace
