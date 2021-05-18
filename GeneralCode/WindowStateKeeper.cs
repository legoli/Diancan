using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace GeneralCode
{
    /**
     * WindowStateKeeper类的作用是让应用程序保持上一次关闭时的窗口大小和位置,以及是否最大化等状态信息
     * 使用:
     * 定义一个本类的全局变量,在MainWindow中
     * 指定配置文件名称
     * 在窗体事件 FormClosing时调用本类的save()方法保存当前窗口信息
     * 在MainWindow的构造函数中,restore()方法恢复窗体状态
     * **/
    /*  --------使用示例1------------------------------------------------------------------------------
        //保存窗口位置的变量(全局)
        WindowStateKeeper windowKeeper = new WindowStateKeeper(@".\DocterBench.ini");
        public DocterBench()//Form构造函数
        {
            ...
            InitializeComponent();

            //恢复窗口上次关闭时的状态
            windowKeeper.restore(this);
            ...
        }
        private void DocterBench_FormClosing(object sender, FormClosingEventArgs e)
        {
            //保存窗口状态
            windowKeeper.save(this);
        }
        ------------------------------------------------------------------------------------------- */
    /*  --------使用示例2------------------------------------------------------------------------------
        //增加一个Item,如Is_Need_Voice=1
     * 
        WindowStateKeeper windowKeeper = new WindowStateKeeper(@".\DocterBench.ini");
     * 在WindowStateKeeper中增加一个函数
        public int workstation_is_need_local_voice()
        {
            String item_name = "need_local_voice";

            String str = ini_file_getParamValue(item_name);
            if (str.Length <= 0)
            {
                //如果原来的ini文件无此Item,则增加一个带默认值的选项
                INI_FILE.INIWriteValue(ini_file, "params", item_name, "0");
                return 0;
            }

            return strToInt(str);
        }
     * 外部调用方法:
     *      bool is_need_local_voice = false;
            if (windowKeeper.workstation_is_need_local_voice() == 1)
            {
                is_need_local_voice = true;
                local_voice_speak_thread = new SpeakTTS();
                local_voice_speak_thread.run();
                local_voice_speak_thread.speak("(医生)(工作站),准备完毕");
            }
        ------------------------------------------------------------------------------------------- */
    public class WindowStateKeeper
    {
        private String ini_file = @".\winstate.ini";//配置文件名称

        //构造函数
        public WindowStateKeeper(String iniFileName)
        {
            ini_file = iniFileName;
        }

        //如果没有ini文件则重新生成一个
        private void ini_file_exist_suarance()
        {
            try
            {
                //判断ini文件是否存在
                if (!System.IO.File.Exists(ini_file))
                {
                    //生成并写入默认值到ini文件
                    //写入默认值得 
                    INI_FILE.INIWriteValue(ini_file, "params", "top", "0");
                    INI_FILE.INIWriteValue(ini_file, "params", "left", "0");
                    INI_FILE.INIWriteValue(ini_file, "params", "width", "0");
                    INI_FILE.INIWriteValue(ini_file, "params", "height", "0");
                    INI_FILE.INIWriteValue(ini_file, "params", "is_maximized", "false");
                    INI_FILE.INIWriteValue(ini_file, "params", "font_family", "宋体");
                    INI_FILE.INIWriteValue(ini_file, "params", "font_size", "18");
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }

        private String ini_file_getParamValue(String strName)
        {
            strName = CSTR.trim(strName);
            if (strName.Length <= 0) return "";

            String strRet = "";

            try
            {
                //确保ini文件存在,没有则创建
                ini_file_exist_suarance();

                //读取ini文件,并更新g_version
                strRet = CSTR.trim(INI_FILE.INIGetStringValue(ini_file, "params", strName, null));
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                strRet = "";
            }

            return strRet;
        }

        //返回ini文件中保存的内容
        public int top()
        {
            return strToInt(ini_file_getParamValue("top"));
        }
        public int left()
        {
            return strToInt(ini_file_getParamValue("left"));
        }
        public int width()
        {
            return strToInt(ini_file_getParamValue("width"));
        }
        public int height()
        {
            return strToInt(ini_file_getParamValue("height"));
        }
        public bool isMaximized()
        {
            String str = ini_file_getParamValue("is_maximized");
            str = str.ToLower();
            return str.Equals("true") ? true : false;
        }

        public String font_family()
        {
            return ini_file_getParamValue("font_family");
        }
        public int font_size()
        {
            return strToInt(ini_file_getParamValue("font_size"));
        }

        //用于DocBench和TijianRing的附加配置,控制是否需要本地语音
        public int workstation_is_need_local_voice()
        {
            String item_name = "need_local_voice";

            String str = ini_file_getParamValue(item_name);
            if (str.Length <= 0)
            {
                //如果原来的ini文件无此Item,则增加一个带默认值的选项
                INI_FILE.INIWriteValue(ini_file, "params", item_name, "0");
                return 0;
            }

            return strToInt(str);
        }

        //用于QueryBench的附加配置,控制是否需要需要手环嘀卡实现放射科自动登记到检
        public int QueryBench_is_need_ris_auto_register()
        {
            String item_name = "need_ris_auto_register";

            String str = ini_file_getParamValue(item_name);
            if (str.Length <= 0)
            {
                //如果原来的ini文件无此Item,则增加一个带默认值的选项
                INI_FILE.INIWriteValue(ini_file, "params", item_name, "0");
                return 0;
            }

            return strToInt(str);
        }


        //转换字符到Int32
        private int strToInt(String str)
        {
            int nRet = -1;//返回-1表示错误

            try
            {
                nRet = Convert.ToInt32(str);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }

            return nRet;
        }

        //保存窗口状态值到ini文件
        public void save(Form wnd)
        {
            if (null == wnd) return;

            bool isMaximized = false;
            if (wnd.WindowState == FormWindowState.Maximized) isMaximized = true;

            //最大化时不保存大小和位置,而是保留上一次非最大化时的值
            try
            {
                if (isMaximized)
                {
                    INI_FILE.INIWriteValue(ini_file, "params", "is_maximized", isMaximized.ToString());
                }
                else
                {
                    //当前屏幕分辨率
                    int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                    int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                    //如果窗口位置超出屏幕的有效范围,则不予保存
                    if (wnd.Left < 0 || wnd.Left > screenWidth) return;
                    if (wnd.Top < 0 || wnd.Top > screenHeight) return;
                    if (wnd.Width <= 0 || wnd.Width > screenWidth) return;
                    if (wnd.Height <= 0 || wnd.Height > screenHeight) return;

                    INI_FILE.INIWriteValue(ini_file, "params", "top", wnd.Top.ToString());
                    INI_FILE.INIWriteValue(ini_file, "params", "left", wnd.Left.ToString());
                    INI_FILE.INIWriteValue(ini_file, "params", "width", wnd.Width.ToString());
                    INI_FILE.INIWriteValue(ini_file, "params", "height", wnd.Height.ToString());
                    INI_FILE.INIWriteValue(ini_file, "params", "is_maximized", isMaximized.ToString());

                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }

        //保存窗口字体到ini文件
        public void saveFont(Form wnd, String fontName, int fontSize)
        {
            if (null == wnd) return;
            fontName = CSTR.trim(fontName);
            if (fontName.Length <= 0) return;
            if (fontSize <= 3 || fontSize > 100) return;

            //最大化时不保存大小和位置,而是保留上一次非最大化时的值
            try
            {
                //wnd.Font = new System.Drawing.Font(fontName, fontSize);
                INI_FILE.INIWriteValue(ini_file, "params", "font_family", fontName);
                INI_FILE.INIWriteValue(ini_file, "params", "font_size", fontSize.ToString());
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }
        //恢复窗口字体
        public void restoreFont(Form wnd)
        {
            if (null == wnd) return;

            String fontName = CSTR.trim(font_family());
            if (fontName.Length <= 0) return;

            int fontSize = font_size();
            if (fontSize <= 3 || fontSize > 100) return;

            //最大化时不保存大小和位置,而是保留上一次非最大化时的值
            try
            {
                wnd.Font = new System.Drawing.Font(fontName, fontSize);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }

        //恢复window窗口状态
        public void restore(Form wnd)
        {
            if (null == wnd) return;

            if (width() > 0 && height() > 0)
            {
                if (isMaximized())
                {
                    wnd.WindowState = FormWindowState.Maximized;
                }

                //当前屏幕分辨率
                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                //设置非最大化时的大小
                int n_top = top();
                int n_left = left();
                int n_width = width();
                int n_height = height();

                //如果窗口位置超出屏幕的有效范围,则不予保存
                if (n_top < 0 || n_left < 0 || n_width <= 0 || n_height <= 0) return;
                if (n_width > screenWidth || n_height > screenHeight) return;

                wnd.StartPosition = FormStartPosition.Manual;
                wnd.Top = top();
                wnd.Left = left();
                wnd.Width = width();
                wnd.Height = height();
            }

        }

        private void ini_file_setVersion(String strVersion)
        {
            strVersion = CSTR.trim(strVersion);
            if (strVersion.Length <= 0) strVersion = "0";

            try
            {
                INI_FILE.INIWriteValue(ini_file, "params", "version", strVersion);
                INI_FILE.INIWriteValue(ini_file, "params", "update_date", DateTime.Now.ToString());
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }


    }
}
