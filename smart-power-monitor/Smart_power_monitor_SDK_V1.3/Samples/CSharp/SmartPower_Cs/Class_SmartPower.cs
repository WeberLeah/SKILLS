using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.InteropServices;

namespace SmartPower_Cs
{
    class Class_SmartPower
    {
        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SmartPower_DeviceNums();        

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SmartPower_DeviceNums_Ext(ref ushort wSN_Buf2);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_DeviceOpen(int iIndex);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_DeviceOpen_Ext(int iIndex, ref ushort wSN_Buf);        

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_DeviceClose();

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_OutputOn(double fVoltage, double fCurrent);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_OutputOn_Ext(double fVoltage, double fCurrent, double fOC_Time, double fOV_AddVoltage, double fOV_Time);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_OutputUpdate(double fVoltage, double fCurrent);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_OutputUpdate_Ext(double fVoltage, double fCurrent, double fOC_Time, double fOV_AddVoltage, double fOV_Time);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_GetStatus(ref double fBuff_ValueRead);

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_GetStatus_Ext(ref double fBuff_ValueRead, ref int bBuff_StatusRead);                

        [DllImport("SmartPower.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SmartPower_OutputOff(int iOutput_OffRes);
    }
}
