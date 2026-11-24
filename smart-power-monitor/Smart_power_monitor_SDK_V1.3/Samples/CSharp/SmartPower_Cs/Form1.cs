using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SmartPower_Cs
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void LogOut_NewLine()
        {
            LogOut("");
        }


        private void LogOut(string sTxt1,string sTxt2)
        {
            LogOut(sTxt1 +" "+ sTxt2);
        }        

        private void LogOut(string sTxt)
        {
            richTextBox_Log.AppendText(Environment.NewLine);
            richTextBox_Log.AppendText(sTxt);
        }

        private void button_Device_Nums_Click(object sender, EventArgs e)
        {
            int iDeviceNums = Class_SmartPower.SmartPower_DeviceNums();

            LogOut_NewLine();
            LogOut("Device_Nums : ", iDeviceNums.ToString());
        }


        private string wchar2string(ushort[] wchar_buf2)
        {
            int iBufLen = wchar_buf2.Length;

            for (int i = 0; i < iBufLen; i++)
            {
                if (wchar_buf2[i] == 0x00)
                {
                    iBufLen = i;
                    break;
                }
            }

            byte[] bsn_Buf = new byte[iBufLen];

            for (int i = 0; i < bsn_Buf.Length; i++)
            {
                bsn_Buf[i] = (byte)wchar_buf2[i];
            }

            string sStr = Encoding.ASCII.GetString(bsn_Buf);

            return (sStr);
        }//end func                

        private void button_Output_On_Click(object sender, EventArgs e)
        {
            double fSetVoltage=(double)numericUpDown_SetVoltage.Value;
            double fSetCurrent=(double)numericUpDown_SetCurrent.Value;

            if (Class_SmartPower.SmartPower_OutputOn(fSetVoltage, fSetCurrent) == true)
            {
                LogOut_NewLine();

                LogOut("Output_On : Success");
                LogOut("Set Voltage (V) = " + fSetVoltage.ToString("F3"));
                LogOut("Set Current (A) = " + fSetCurrent.ToString("F3"));
            }
        }

        private void button_Output_On_Ext_Click(object sender, EventArgs e)
        {
            double fSetVoltage = (double)numericUpDown_SetVoltage.Value;
            double fSetCurrent = (double)numericUpDown_SetCurrent.Value;

            double fOC_Time=50;
            double fOV_AddVoltage = 0.55; // Must between 0.3~0.8
            double fOV_Time = 50;

            if (Class_SmartPower.SmartPower_OutputOn_Ext(fSetVoltage, fSetCurrent, fOC_Time, fOV_AddVoltage, fOV_Time) == true)
            {
                LogOut_NewLine();

                LogOut("Output_On_Ext : Success");
                LogOut("Set Voltage (V) = " + fSetVoltage.ToString("F3"));
                LogOut("Set Current (A) = " + fSetCurrent.ToString("F3"));
            }
        }

        private void button_Output_Update_Ext_Click(object sender, EventArgs e)
        {
            double fSetVoltage = (double)numericUpDown_SetVoltage.Value;
            double fSetCurrent = (double)numericUpDown_SetCurrent.Value;

            double fOC_Time = 50;
            double fOV_AddVoltage = 0.55; // Must between 0.3~0.8
            double fOV_Time = 50;

            if (Class_SmartPower.SmartPower_OutputUpdate_Ext(fSetVoltage, fSetCurrent, fOC_Time, fOV_AddVoltage, fOV_Time) == true)
            {
                LogOut_NewLine();

                LogOut("Output_Update_Ext : Success");
                LogOut("Set Voltage (V) = " + fSetVoltage.ToString("F3"));
                LogOut("Set Current (A) = " + fSetCurrent.ToString("F3"));
            }
        }

        private void button_Output_Update_Click(object sender, EventArgs e)
        {
            double fSetVoltage = (double)numericUpDown_SetVoltage.Value;
            double fSetCurrent = (double)numericUpDown_SetCurrent.Value;

            if (Class_SmartPower.SmartPower_OutputUpdate(fSetVoltage, fSetCurrent) == true)
            {
                LogOut_NewLine();

                LogOut("Output_Update : Success");
                LogOut("Set Voltage (V) = " + fSetVoltage.ToString("F3"));
                LogOut("Set Current (A) = " + fSetCurrent.ToString("F3"));
            }
        }

        private void button_Output_Off_Click(object sender, EventArgs e)
        {
            int iOutput_OffRes=1 ; //0--HiZ ;  1--20K to GND

            if (Class_SmartPower.SmartPower_OutputOff(iOutput_OffRes) == true)
            {
                LogOut("Output_Off : Success");
            }
        }

        private void button_Get_Status_Click(object sender, EventArgs e)
        {
            double[] fBuff_ValueRead = new double[4];
            if (Class_SmartPower.SmartPower_GetStatus(ref fBuff_ValueRead[0]) == true)
            {
                LogOut_NewLine();

                LogOut("Get_Status : Success");
                LogOut("Voltage(V) = " + fBuff_ValueRead[0].ToString("F4"));
                LogOut("Current(V) = " + fBuff_ValueRead[1].ToString());
                LogOut("Bnc(V) = " + fBuff_ValueRead[2].ToString("F4"));

                LogOut("Reserve = " + fBuff_ValueRead[3].ToString("F4"));
            }
        }

        private void button_Get_Status_Ext_Click(object sender, EventArgs e)
        {
            double[] fBuff_ValueRead = new double[4];
            int[] bBuff_StatusRead = new int[4];

            if (Class_SmartPower.SmartPower_GetStatus_Ext(ref fBuff_ValueRead[0], ref bBuff_StatusRead[0]) == true)
            {
                LogOut_NewLine();

                //
                LogOut("Get_Status_Ext : Success");
                LogOut("Voltage(V) = " + fBuff_ValueRead[0].ToString("F4"));
                LogOut("Current(V) = " + fBuff_ValueRead[1].ToString());
                LogOut("Bnc(V) = " + fBuff_ValueRead[2].ToString("F4"));

                LogOut("Reserve = " + fBuff_ValueRead[3].ToString("F4"));

                //
                LogOut("OV = " + bBuff_StatusRead[0].ToString());
                LogOut("OC = " + bBuff_StatusRead[1].ToString());
                LogOut("OT = " + bBuff_StatusRead[2].ToString());
                LogOut("DC_20V = " + bBuff_StatusRead[3].ToString());
            }
        }

        private void button_Device_Close_Click(object sender, EventArgs e)
        {
            if (Class_SmartPower.SmartPower_DeviceClose() == true)
            {
                LogOut_NewLine();
                LogOut("Device_Close : Success");
            }
        }

        private void button_Device_Open_Click(object sender, EventArgs e)
        {
            int iDevice_Index = (int)numericUpDown_Device_Open_Index.Value;

            bool bValue = Class_SmartPower.SmartPower_DeviceOpen(iDevice_Index);

            if (bValue == true)
            {
                LogOut_NewLine();

                LogOut("Device_Open_Index : Success");
            }
        }

        private void button_Device_Open_Ext_Click(object sender, EventArgs e)
        {
            int iDevice_Index = (int)numericUpDown_Device_Open_Index.Value;
            ushort[] DeviceSN = new ushort[30];

            bool bValue = Class_SmartPower.SmartPower_DeviceOpen_Ext(iDevice_Index, ref DeviceSN[0]);

            if (bValue == true)
            {
                LogOut_NewLine();

                string sDeviceSN = wchar2string(DeviceSN);
                LogOut("Device_Open_Index : Success");

                LogOut("Device_SN : ", sDeviceSN);

            }
        }

        //231220 Add
        private string wchar2string_ext(ushort[,] wchar_buf2, int iPos)
        {
            int iBufLen = wchar_buf2.GetLength(1);

            for (int i = 0; i < iBufLen; i++)
            {
                if (wchar_buf2[iPos, i] == 0x00)
                {
                    iBufLen = i;
                    break;
                }
            }

            byte[] bsn_Buf = new byte[iBufLen];

            for (int i = 0; i < bsn_Buf.Length; i++)
            {
                bsn_Buf[i] = (byte)wchar_buf2[iPos, i];
            }

            string sStr = Encoding.ASCII.GetString(bsn_Buf);
            //string sStr = System.Text.Encoding.Default.GetString(bsn_Buf);

            return (sStr);
        }//end func

        private void button_Device_Nums_Ext_Click(object sender, EventArgs e)
        {
            int iDevice_Nums = 0;
            ushort[,] wsn_Buf2 = new ushort[50, 30];

            try
            {
                iDevice_Nums = Class_SmartPower.SmartPower_DeviceNums_Ext(ref wsn_Buf2[0, 0]);
            }
            catch
            {
                iDevice_Nums = 0;
            }


            if (iDevice_Nums < 1)
                return;

            //
            string[] sDevieSnListArr = new string[iDevice_Nums];
            for (int i = 0; i < sDevieSnListArr.Length; i++)
                sDevieSnListArr[i] = wchar2string_ext(wsn_Buf2,i);
            
            //
            LogOut_NewLine();
            for (int i = 0; i < sDevieSnListArr.Length; i++)
            {
                LogOut("Device "+i.ToString()+ " : " + sDevieSnListArr[i]);
            }
            
        }//end func


    }//==============
}
