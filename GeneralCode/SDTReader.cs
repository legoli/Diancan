using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace GeneralCode
{
    public class SDTReader
    {

        public const char REQUESTMODE_ALL = (char)0x52;
        public const char REQUESTMODE_ACTIVE = (char)0x26;

        public const char SAM_BOUND_9600 = (char)0;
        public const char SAM_BOUND_38400 = (char)1;

        public const char PASSWORD_A = (char)0x60;
        public const char PASSWORD_B = (char)0x61;

        //打开端口
        [DllImport("SDT.dll")]
        public static extern int YW_USBHIDInitial();

        //关闭端口
        [DllImport("SDT.dll")]
        public static extern int YW_USBHIDFree();

        [DllImport("SDT.dll")]
        public static extern int YW_GetDLLVersion();

        //设置设备号
        [DllImport("SDT.dll")]
        public static extern int YW_SetReaderID(short OldID, short NewID);

        [DllImport("SDT.dll")]
        public static extern int YW_GetReaderID(int readerid);

        [DllImport("SDT.dll")]
        public static extern int YW_GetReaderVersion(int ReaderID);

        //beep function
        [DllImport("SDT.dll")]
        public static extern int YW_Buzzer(int ReaderID, int Time_ON, int Time_OFF, int Cycle);

        //led control
        [DllImport("SDT.dll")]
        public static extern int YW_Led(int ReaderID, int LEDIndex, int Time_ON, int Time_OFF, int Cycle);

        //天线状态
        [DllImport("SDT.dll")]
        public static extern int YW_AntennaStatus(int ReaderID, bool Status);

        //寻卡
        [DllImport("SDT.dll")]
        public static extern int YW_RequestCard(int ReaderID, char RequestMode, ref short CardType);

        //防重装锁定一个卡
        [DllImport("SDT.dll")]
        public static extern int YW_AntiCollideAndSelect(int ReaderID, char MultiReturnCode, ref char CardMem, ref int CardNoLen, ref Byte SN);

        //验证身份
        [DllImport("SDT.dll")]
        public static extern int YW_KeyAuthorization(int ReaderID, char KeyMode, int BlockAddr, ref Byte Key);

        //Reader special sector
        [DllImport("SDT.dll")]
        public static extern int YW_ReadaBlock(int ReaderID, int BlockAddr, int LenData, ref Byte Data);

        //write a sector
        [DllImport("SDT.dll")]
        public static extern int YW_WriteaBlock(int ReaderID, int BlockAddr, int LenData, ref Byte Data);

        [DllImport("SDT.dll")]
        public static extern int YW_Purse_Initial(int ReaderID, int BlockAddr, int IniMoney);

        [DllImport("SDT.dll")]
        public static extern int YW_Purse_Read(int ReaderID, int BlockAddr, ref int Money);

        [DllImport("SDT.dll")]
        public static extern int YW_Purse_Decrease(int ReaderID, int BlockAddr, int Decrement);

        [DllImport("SDT.dll")]
        public static extern int YW_Purse_Charge(int ReaderID, int BlockAddr, int Charge);

        [DllImport("SDT.dll")]
        public static extern int YW_CardHalt(int ReaderID);

        [DllImport("SDT.dll")]
        public static extern int YW_Restore(int ReaderID, int BlockAddr);

        [DllImport("SDT.dll")]
        public static extern int YW_Transfer(int ReaderID, int BlockAddr);


    }
}

