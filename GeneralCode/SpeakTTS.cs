using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;
using System.Speech.Synthesis;

namespace GeneralCode
{
    public class SpeakTTS : ILightThreadable
    {
        //TTS线程控制变量
        private bool need_speak = false;//提醒线程有任务
        private String text_speak = "";//存放需要播报的内容

        //启动线程
        public void run()
        {
            worker_init();
        }

        //调用者写入需要播报的语音文本
        public void speak(String str)
        {
            str = CSTR.trim(str);
            if (CSTR.isEmpty(str)) return;

            text_speak = str;
            need_speak = true;
        }

        #region LightThread线程-------------------------------------------------------Start
        private LightThread worker_thread = null;
        private long worker_thread_loop_times = 0;
        private void worker_init()
        {
            //1秒循环一次,读取数据库的时间控制通过计数器
            worker_thread = new LightThread(this, 100);
            worker_thread.IsBlockInProcessReport = true;//Report时阻塞线程
            worker_thread.run();//启动线程
        }

        public Object worker_main(Object e)
        {
            worker_thread_loop_times++;
            if (worker_thread_loop_times > 60000) worker_thread_loop_times = 0;

            //构建返回参数
            Dictionary<String, String> map = new Dictionary<string, string>();

            //无语音文件时一直原地循环
            while (false == need_speak)
            {
                Thread.Sleep(100);
            }

            //Reset控制变量
            need_speak = false;
            map.Add("result", text_speak);

            //-------------------------------------------------
            try
            {
                if (null == tts_engine)
                {
                    tts_init();
                }

                if (null != tts_engine)
                {
                    tts_engine.Speak(text_speak);
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                tts_engine = null;
                //System.Windows.Forms.MessageBox.Show("tts fail");
            }
            //-------------------------------------------------

            return map;
        }
        public void worker_Report(Object dataObj)
        {
            if (null == dataObj) return;

            Dictionary<String, String> map = dataObj as Dictionary<String, String>;

            //System.Windows.Forms.MessageBox.Show(CSTR.ObjectTrim(map["result"]));
        }
        public void worker_Completed(Object dataObj)
        {

        }

        private SpeechSynthesizer tts_engine = null;
        private void tts_init()
        {
            try
            {
                tts_engine = new SpeechSynthesizer();
                tts_engine.Volume = 100;  //设置朗读音量 [范围 0 ~ 100] 
                tts_engine.Rate = -3;//设置朗读频率 [范围  (慢)-10 ~ 10(快)] 
                tts_engine.SelectVoice("VW Lily");
                //speak.SpeakAsyncCancelAll();  //取消朗读
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
        }
        #endregion LightThread线程-------------------------------------------------------End
    }//end class
}//end namespace
