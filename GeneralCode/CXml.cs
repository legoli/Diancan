using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace GeneralCode
{
    public class CXml
    {
        private String xmlFileName = "";
        public CXml()
        {
            xmlFileName = @"abc.xml";

            //判断文件是否已经存在,如果没有则生成默认的文件
            if (!System.IO.File.Exists(this.xmlFileName))
            {
                generateConfigFile();
            }
        }
        public CXml(String filename)
        {
            xmlFileName = filename;
            //判断文件是否已经存在,如果没有则生成默认的文件
            if (!System.IO.File.Exists(this.xmlFileName))
            {
                generateConfigFile();
            }
        }
        ~CXml()
        {
        }

        public bool generateConfigFile()
        {
            //判断文件是否已经存在
            if (System.IO.File.Exists(this.xmlFileName))
            {
                Console.WriteLine(String.Format("文件{0}已经存在", this.xmlFileName));
                //return false;
            }

            XmlDocument xmldoc = new XmlDocument();
            //加入XML的声明段落,<?xml version="1.0" encoding="gb2312"?>
            XmlDeclaration decl = xmldoc.CreateXmlDeclaration("1.0", "gb2312", null);
            xmldoc.AppendChild(decl);

            XmlElement element = null;

            //加入一个根元素
            //element = xmldoc.CreateElement("prifix", "localName", "namespaceURI");
            element = xmldoc.CreateElement("root");
            xmldoc.AppendChild(element);

            //取得要加入的Parent节点
            XmlNode root = xmldoc.SelectSingleNode("root");

            //系统配置参数设置-------------------------------------------------------------
            XmlElement nParams = xmldoc.CreateElement("system_params");
            nParams.SetAttribute("type", "系统参数");

            XmlElement item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "tj_db");//设置item节点属性
            item.SetAttribute("value", "192.168.1.209");//设置item节点属性
            item.SetAttribute("tip", "体检服务器IP");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "form_location");//设置item节点属性
            item.SetAttribute("value", "full");//设置item节点属性
            item.SetAttribute("tip", "程序显示位置,可选项:left/right/full");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "room_id");//设置item节点属性
            item.SetAttribute("value", "[322]");//设置item节点属性
            item.SetAttribute("tip", "检查室编号:格式[322]");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "room_name");//设置item节点属性
            item.SetAttribute("value", "322室");//设置item节点属性
            item.SetAttribute("tip", "检查室名称,用于显示");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "site_id");//设置item节点属性
            item.SetAttribute("value", "三二二室 ");//设置item节点属性
            item.SetAttribute("tip", "进程ID,用于语音叫号的房间号");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "tts_ip");//设置item节点属性
            item.SetAttribute("value", "127.0.0.1");//设置item节点属性
            item.SetAttribute("tip", "语音等级台IP地址");//设置item节点属性
            nParams.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "apply_sheet_position");//设置item节点属性
            item.SetAttribute("value", "0");//设置item节点属性
            item.SetAttribute("tip", "申请单显示的默认页面位置,范围(0 ~ 10)");//设置item节点属性
            nParams.AppendChild(item);

            //加入到root
            root.AppendChild(nParams);

            //插队原因列表-----------------------------------------------------------------
            XmlElement queueOrderChangeReason = xmldoc.CreateElement("queue_order_change_reason");
            queueOrderChangeReason.SetAttribute("type", "插队原因");

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "发热");//设置item节点属性
            queueOrderChangeReason.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "急诊");//设置item节点属性
            queueOrderChangeReason.AppendChild(item);

            item = xmldoc.CreateElement("item");//创建一个item节点
            item.SetAttribute("name", "军人");//设置item节点属性
            queueOrderChangeReason.AppendChild(item);

            //加入到root
            root.AppendChild(queueOrderChangeReason);


            //保存为磁盘文件
            xmldoc.Save(this.xmlFileName);

            return true;
        }

        /// <summary>
        /// 获取system_params参数
        /// </summary>
        /// <returns></returns>
        public Dictionary<String, String> getSystemParam()
        {
            Dictionary<String, String> map = new Dictionary<string, string>();

            //读取配置文件
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(this.xmlFileName);

            //获取system_params的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode("root/system_params").ChildNodes;

            //遍历所有子节点,并比对有无符合条件的item
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                map.Add(item.GetAttribute("name"), item.GetAttribute("value"));
            }

            //没有找到匹配项,则返回空字符串
            return map;
        }

        /// <summary>
        /// 获取排队原因的List
        /// </summary>
        /// <returns></returns>
        public List<String> getQueueReason()
        {
            List<String> list = new List<string>();

            //读取配置文件
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(this.xmlFileName);

            //获取system_params的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode("root/queue_order_change_reason").ChildNodes;

            //遍历所有子节点,并比对有无符合条件的item
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                list.Add(item.GetAttribute("name"));
            }

            //没有找到匹配项,则返回空字符串
            return list;
        }

        /// <summary>
        /// 从字符串中读取固定格式的xml字符串
        /// </summary>
        /// <returns></returns>
        public Dictionary<String, String> parseStringXml(String str)
        {
            str = @"<?xml version=""1.0"" encoding=""utf-8""?>" + str;
            //<info>
            //<room>2号室</room>
            //<name>和三</name>
            //</info>
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(str);

            //获取info的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode("info").ChildNodes;

            if (null == itemList) return null;
            if (itemList.Count <= 0) return null;

            Dictionary<String, String> map = new Dictionary<string, string>();

            //遍历所有子节点,并比对有无符合条件的item
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                map.Add(item.Name, item.InnerText);
            }

            return map;
        }

        public bool test_generateXmlFile()
        {
            XmlDocument xmldoc = new XmlDocument();
            //加入XML的声明段落,<?xml version="1.0" encoding="gb2312"?>
            XmlDeclaration decl = xmldoc.CreateXmlDeclaration("1.0", "gb2312", null);
            xmldoc.AppendChild(decl);

            XmlElement element = null;

            //加入一个根元素
            //element = xmldoc.CreateElement("prifix", "localName", "namespaceURI");
            element = xmldoc.CreateElement("root");
            xmldoc.AppendChild(element);

            //加入其他的子元素
            for (int i = 0; i < 3; i++)
            {
                //取得要加入的Parent节点
                XmlNode root = xmldoc.SelectSingleNode("root");

                XmlElement item = xmldoc.CreateElement("item");//创建一个item节点
                item.SetAttribute("ip", "127.0.0.1");//设置item节点属性
                item.SetAttribute("port", "8080");//设置item节点属性

                XmlElement x1 = xmldoc.CreateElement("title");
                x1.InnerText = "WPF入门到精通2";
                item.AppendChild(x1);
                XmlElement x2 = xmldoc.CreateElement("type");
                x2.InnerText = "IT";
                item.AppendChild(x2);

                //把item加入到root中
                root.AppendChild(item);
            }

            //保存为磁盘文件
            xmldoc.Save(this.xmlFileName);

            return true;
        }

        public String getSystemParam_backup(String paramName)
        {
            String strRet = "";
            //系统参数必须要指定
            if (String.IsNullOrEmpty(paramName)) return strRet;

            //读取配置文件
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(this.xmlFileName);

            //获取system_params的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode("root/system_params").ChildNodes;

            //遍历所有子节点,并比对有无符合条件的item
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                if (item.GetAttribute("name") == paramName)
                {
                    //参数名称匹配上
                    return item.GetAttribute("value");
                }
            }

            //没有找到匹配项,则返回空字符串
            return strRet;
        }

        /**
         * 返回List
         * strNode格式: root/system_params
         * 
         * **/
        public List<Dictionary<String, String>> getConfigNode(String strNode)
        {
            List<Dictionary<String, String>> list = new List<Dictionary<string, string>>(); ;

            //系统参数必须要指定
            if (String.IsNullOrEmpty(strNode)) return list;

            //读取配置文件
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(this.xmlFileName);

            //获取system_params的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode(strNode).ChildNodes;

            //遍历所有子节点,生成Dictionary并添加到List
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                Dictionary<String, String> map = new Dictionary<string, string>();
                for (int i = 0; i < item.Attributes.Count; i++)
                {
                    //item的每个Attribute都加入到Dictionary
                    map.Add(item.Attributes[i].Name, item.Attributes[i].Value);
                }
                //List添加一条记录
                list.Add(map);
            }

            //没有找到匹配项,则返回空字符串
            return list;
        }

        public bool modifySystemParam(String paramName,String strValue)
        {
            //系统参数必须要指定
            if (String.IsNullOrEmpty(paramName)) return false;

            //读取配置文件
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(this.xmlFileName);

            //获取system_params的所有子节点
            XmlNodeList itemList = xmldoc.SelectSingleNode("root/system_params").ChildNodes;

            //遍历所有子节点,并比对有无符合条件的item
            foreach (XmlNode node in itemList)
            {
                XmlElement item = (XmlElement)node;//将子节点类型转换为XmlElement类型
                if (item.GetAttribute("name") == paramName)
                {
                    //参数名称匹配上
                    item.SetAttribute("value", strValue);//修改参数的value
                    xmldoc.Save(this.xmlFileName);//保存到配置文件
                    return true;
                }
            }

            //没有找到匹配项,则返回false
            return false;
        }

    }
}
