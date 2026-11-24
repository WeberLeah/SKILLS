// SmartPower_Vc.cpp : 定义控制台应用程序的入口点。
//

#include "stdafx.h"

#include "smartpower.h"

int _tmain(int argc, _TCHAR* argv[])
{
	bool bValue;

	wchar_t wSN_Buf[30];

	double fArr_ValueRead[4];	
	int bArr_StatusRead[4];

	int iDevice_Nums;

	//V1.3 Add
	wchar_t wSN_Buf_Arr[50][30];
	
	//<<<<<<<<<<<<<<<<<<<<<<<<
	//Use Normal Func
	iDevice_Nums=SmartPower_DeviceNums();
	if(iDevice_Nums<1)
		return(0);

	bValue=SmartPower_DeviceOpen(0);

	bValue=SmartPower_OutputOn(1,0.5);

	bValue=SmartPower_GetStatus(fArr_ValueRead);

	bValue=SmartPower_OutputUpdate(2,0.6);

	bValue=SmartPower_GetStatus(fArr_ValueRead);

	bValue=SmartPower_OutputOff(1);

	bValue=SmartPower_GetStatus(fArr_ValueRead);

	bValue=SmartPower_DeviceClose();

	//<<<<<<<<<<<<<<<<<<<<<<<<
	//Use _Ext Func
	iDevice_Nums=SmartPower_DeviceNums();
	if(iDevice_Nums<1)
		return(0);

	//V1.3  Add
	iDevice_Nums=SmartPower_DeviceNums_Ext(wSN_Buf_Arr);

	bValue=SmartPower_DeviceOpen_Ext(0,wSN_Buf);

	bValue=SmartPower_OutputOn_Ext(1,0.5,50,0.55,50);

	bValue=SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);

	bValue=SmartPower_OutputUpdate_Ext(2,0.6,50,0.55,50);

	bValue=SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);

	bValue=SmartPower_OutputOff(1);

	bValue=SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);

	bValue=SmartPower_DeviceClose();

	getchar();
	return 0;
}

