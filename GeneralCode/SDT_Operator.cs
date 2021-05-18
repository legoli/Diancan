using System;
using System.Collections.Generic;
using System.Text;
using GeneralCode;//引入LightThread
using System.ComponentModel;//引入BackgroundWorker
using System.Threading;

namespace GeneralCode
{
    public class SDT_Operator
    {
        private bool isPortOpened = false;//提示端口是否成功打开
        private String errorMessage = "";//出现异常时的提示信息

        public SDT_Operator()
        {
            //打开端口
            int rt = -1;
            try
            {
                rt = SDTReader.YW_USBHIDInitial();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }

            if (rt > 0)//打开设备OK
            {
                isPortOpened = true;
            }
            else
            {
                isPortOpened = false;
            }
        }

        ~SDT_Operator()
        {
            ClosePort();
        }

        public void ClosePort()
        {
            try
            {
                //关闭端口
                if (isPortOpened) SDTReader.YW_USBHIDFree();
                isPortOpened = false;
            }
            catch (Exception exp)
            {
                isPortOpened = false;
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }
        }

        //查询端口是否成功打开
        public bool IsPortOpened()
        {
            return isPortOpened;
        }

        public String ReadCardNo()
        {
            if (!isPortOpened) return "";

            //读取卡号
            int ReaderID = 0;
            int BlockID = 0;
            char KeySel = SDTReader.PASSWORD_A;//SDTReader.PASSWORD_B
            short CardType1 = 0;
            char CardMem1 = (char)0;
            byte[] Key = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            byte[] Data = new byte[16];
            byte[] SN = new byte[4];
            int CardNoLen1 = 0;

            String strRet = "";
            try
            {
                if (SDTReader.YW_AntennaStatus(ReaderID, true) >= 0)
                {
                    if (SDTReader.YW_RequestCard(ReaderID, SDTReader.REQUESTMODE_ALL, ref CardType1) > 0)
                    {
                        if (SDTReader.YW_AntiCollideAndSelect(ReaderID, (char)1, ref CardMem1, ref  CardNoLen1, ref SN[0]) > 0)
                        {
                            if (SDTReader.YW_KeyAuthorization(ReaderID, KeySel, BlockID, ref Key[0]) > 0)
                            {
                                if (SDTReader.YW_ReadaBlock(ReaderID, BlockID, 16, ref Data[0]) > 0)
                                {
                                    strRet = "";
                                    //for (int i = 0; i < 16; i++)
                                    for (int i = 0; i < 4; i++)//8位卡号,4个字节
                                    {
                                        strRet = strRet + Data[i].ToString("X2");
                                    }

                                    //MessageBox.Show("读取正确");
                                    //读取成功后,滴滴一声
                                    SDTReader.YW_Buzzer(ReaderID, 1, 1, 1);
                                    //绿灯闪一闪
                                    SDTReader.YW_Led(ReaderID, 1, 1, 1, 3);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                isPortOpened = false;
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }

            return strRet;
        }

        //Beep
        public void Beep()
        {
            int ReaderID = 0;

            if (!isPortOpened) return;

            try
            {
                //Beep
                SDTReader.YW_Buzzer(ReaderID, 1, 1, 1);
            }
            catch (Exception exp)
            {
                isPortOpened = false;
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }
        }

        //Beep 使用在空队列出现新检查时
        public void Beep_EmptyQueue_New_Arrival()
        {
            int ReaderID = 0;

            if (!isPortOpened) return;

            try
            {
                //Beep
                SDTReader.YW_Buzzer(ReaderID, 3, 2, 3);
            }
            catch (Exception exp)
            {
                isPortOpened = false;
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }
        }



        //Led flash
        public void LedFlash()
        {
            int ReaderID = 0;

            if (!isPortOpened) return;

            try
            {
                //绿灯闪一闪
                SDTReader.YW_Led(ReaderID, 1, 1, 1, 3);
            }
            catch (Exception exp)
            {
                isPortOpened = false;
                Console.WriteLine(exp.Message);
                errorMessage = exp.Message;
            }
        }

    }//end class
}//end namespace
