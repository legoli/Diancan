using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace GeneralCode
{
    /**
     * BackupTable类的作用是在相同字段的数据库表之间互相Copy数据,实现数据表的备份
     * 当前版本实现Mysql数据库表的备份,以后可以扩展
     *      //删除前先备份--使用示例
            sql = String.Format("Select * From JiaohaoTable Where RfidName='{0}'", ringNo);
            tbl = getDB().query(sql);
            bk = new BackupTable(getDB(), "Backup_JiaohaoTable");
            bk.insert(tbl);
     * 
     * **/
    public class BackupTable
    {
        private MysqlOperator mysql = null;
        private String target_table = "";

        public BackupTable(MysqlOperator connection,String targetTableName)
        {
            mysql = connection;//保存目标数据库链接
            target_table = targetTableName;//保存目标数据表名称
        }

        public int insert(DataTable tbl)
        {

            if (CSTR.IsTableEmpty(tbl)) return 0;

            //拼接字段名字
            String colNames = "";
            foreach (DataColumn column in tbl.Columns) colNames += column.ColumnName + ",";
            colNames = colNames.TrimEnd(',');//去掉最后一个逗号

            foreach (DataRow row in tbl.Rows)
            {
                String colValue = getRowDataCluster(row, tbl.Columns);
                String sql = String.Format("Insert Into {0} ({1}) VALUES ({2})", target_table, colNames, colValue);
                getDB().update(sql);
            }

            return 0;
        }

        //把指定数据行的数据用逗号串接起来
        private String getRowDataCluster(DataRow row, DataColumnCollection Columns)
        {
            String colValue="";

            int columnCount = Columns.Count;
            for (int i = 0; i < columnCount; i++)
            {
                //字段为null
                if (row[i].GetType() == typeof(DBNull))
                {
                    colValue += "NULL,";
                    continue;
                }

                //字段类型和相应的处理
                if (Columns[i].DataType == typeof(String))
                {
                    colValue += String.Format("'{0}',", row[i]);
                }
                else if (Columns[i].DataType == typeof(DateTime))
                {
                    colValue += String.Format("CAST('{0}' AS DATETIME),", row[i]);
                }
                else if (Columns[i].DataType == typeof(bool))
                {
                    colValue += String.Format("{0},", row[i].ToString());
                }
                else if (Columns[i].DataType == typeof(int) ||
                    Columns[i].DataType == typeof(float) ||
                    Columns[i].DataType == typeof(double))
                {
                    colValue += String.Format("{0},", row[i]);
                }
                else
                {
                    colValue += String.Format("'{0}',", row[i]);
                }
            }//end for

            colValue = colValue.TrimEnd(',');
            return colValue;
        }

        private MysqlOperator getDB()
        {
            return mysql;
        }

    }//end class
}//end namespace
