using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;

namespace GeneralCode
{
    public class CSTR
    {
        //-------------CSTR.trim()--------------------------------------------------------
        static public String trim(String str)
        {
            if (null == str) return "";

            return str.Trim();
        }

        //-------------CSTR.ObjectTrim()把Object类型转换为Trim()的String-----------------------------
        static public String ObjectTrim(Object str)
        {
            if (null == str) return "";

            return str.ToString().Trim();
        }

        //-------------CSTR.isEmpty()判断字符串是否为空或null-----------------------------
        static public bool isEmpty(String str)
        {
            str = trim(str);
            return (str.Length <= 0) ? true : false;
        }

        //-------------CSTR.IsTableEmpty()判断表中是否没有数据-----------------------------
        static public bool IsTableEmpty(DataTable tbl)
        {
            if (null == tbl) return true;
            if (tbl.Rows.Count <= 0) return true;

            return false;
        }

        //-------------CSTR.IsRowArrEmpty()判断DaraRow数组中是否没有数据-----------------------------
        static public bool IsRowArrEmpty(DataRow[] rowArr)
        {
            if (null == rowArr) return true;
            if (rowArr.Length <= 0) return true;

            return false;
        }

        //-------------Dictionary中是否没有数据(适用:Dictionary<>)-----------------------------
        static public bool IsDictionaryEmpty(Dictionary<String,Object> map)
        {
            if (null == map) return true;
            if (map.Count <= 0) return true;
            return false;
        }
        //-------------List中是否没有数据(适用:ArrayList,List<>)-----------------------------
        static public bool IsListEmpty(ArrayList list)
        {
            if (null == list) return true;
            if (list.Count <= 0) return true;
            return false;
        }
        static public bool IsListEmpty(List<Object> list)
        {
            if (null == list) return true;
            if (list.Count <= 0) return true;
            return false;
        }

        static public bool IsListEmpty(List<String> list)
        {
            if (null == list) return true;
            if (list.Count <= 0) return true;
            return false;
        }

        //-------------Array数组中是否没有数据-----------------------------
        static public bool IsArrayEmpty(Array arr)
        {
            if (null == arr) return true;
            if (arr.Length <= 0) return true;

            return false;
        }

        //-------------NumberToChinese把"1234"转换为"一二三四"-----------------------------
        static public String NumberToChinese(String str)
        {
            String strRet = "";

            str = trim(str);

            //构建映射表
            Dictionary<char, String> mapTable = new Dictionary<char, string>();
            mapTable.Add('1', "一");
            mapTable.Add('2', "二");
            mapTable.Add('3', "三");
            mapTable.Add('4', "四");
            mapTable.Add('5', "五");
            mapTable.Add('6', "六");
            mapTable.Add('7', "七");
            mapTable.Add('8', "八");
            mapTable.Add('9', "九");
            mapTable.Add('0', "零");

            foreach (char ch in str)
            {
                String strConv = "";

                foreach (char key in mapTable.Keys)
                {
                    if (ch == key) strConv = trim(mapTable[ch]);
                }

                if (strConv.Length > 0)//映射成功
                {
                    strRet += strConv;
                }
                else
                {
                    strRet += ch.ToString();
                }
            }

            return strRet;
        }

        //-------------把字符串内部的空格也trim掉-----------------------------
        static public String innerTrim(String str)
        {
            str = trim(str);
            str = str.Replace(" ", "");

            return str;
        }

        //-------------把格式为'[301][302]'的字符串处理为String[] [301]/[302]-----------------------------
        static public String[] splitRooms(String str)
        {
            if (isEmpty(str)) return null;

            str = innerTrim(str);
            if (str.IndexOf("][") <= 0) return new String[] { str };

            str = str.Replace("][", "],[");

            return str.Split(',');
        }
        //-------------配合splitRooms,返回数组长度,null的长度为0-----------------------------
        static public int splitRoomsCount(String[] arr)
        {
            if (null == arr) return 0;
            if (arr.Length <= 0) return 0;

            return arr.Length;
        }

        //-------------转换字符串为INT类型-----------------------------
        static public int convertToInt(String str)
        {
            int nRet = -1;
            try
            {
                nRet = Convert.ToInt32(str);
            }
            catch (Exception exp)
            {
                nRet = -1;
                Console.WriteLine(exp.Message);
            }

            return nRet;
        }

        //名字脱敏,在第二个字上替换为"*"
        static public String decutePName(String str)
        {
            str = trim(str);
            if (str.Length <= 0) return "";


            //对于手工登记的姓名不做处理
            if (str.IndexOf("个检") >= 0)
            {
                return str;
            }
            else if (str.IndexOf("车检") >= 0)
            {
                return str;
            }
            else if (str.IndexOf("儿童入园") >= 0)
            {
                return str;
            }
            else if (str.IndexOf("特殊工种") >= 0)
            {
                return str;
            }
            else if (str.IndexOf("婚检") >= 0)
            {
                return str;
            }
            else if (str.IndexOf("职检") >= 0)
            {
                return str;
            }

            StringBuilder buf = new StringBuilder(str);

            //对于两个字的名字的处理
            if (buf.Length == 2)
            {
                String strRet = buf[0] + "*" + buf[1];
                return strRet;
            }
            else
            {
                if (buf.Length > 1) buf[1] = '*';
            }

            return buf.ToString();
        }		

    }//end class
}//end namespace
