# This is a SmartPower Python script samples. Use python-3.7.1
# Please use 32bit development environment
# by www.smartxbw.com


from ctypes import *
SmartPower =CDLL(".\\SmartPower.dll")

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# Dll function define
#
SmartPower.SmartPower_DeviceNums.argtypes=[]
SmartPower.SmartPower_DeviceNums.restype=c_int

SmartPower.SmartPower_DeviceOpen.argtypes=[c_int]
SmartPower.SmartPower_DeviceOpen.restype=c_bool

SmartPower.SmartPower_DeviceOpen_Ext.argtypes=[c_int,c_char_p]
SmartPower.SmartPower_DeviceOpen_Ext.restype=c_bool

SmartPower.SmartPower_DeviceClose.argtypes=[]
SmartPower.SmartPower_DeviceClose.restype=c_bool

#
SmartPower.SmartPower_OutputOn.argtypes=[c_double,c_double]
SmartPower.SmartPower_OutputOn.restype=c_bool

SmartPower.SmartPower_OutputOn_Ext.argtypes=[c_double,c_double,c_double,c_double,c_double]
SmartPower.SmartPower_OutputOn_Ext.restype=c_bool

#
SmartPower.SmartPower_OutputUpdate.argtypes=[c_double,c_double]
SmartPower.SmartPower_OutputUpdate.restype=c_bool

SmartPower.SmartPower_OutputUpdate_Ext.argtypes=[c_double,c_double,c_double,c_double,c_double]
SmartPower.SmartPower_OutputUpdate_Ext.restype=c_bool

#
SmartPower.SmartPower_GetStatus.argtypes= {POINTER(c_double)}
SmartPower.SmartPower_GetStatus.restype=c_bool

SmartPower.SmartPower_GetStatus_Ext.argtypes=[POINTER(c_double),POINTER(c_int)]
SmartPower.SmartPower_GetStatus_Ext.restype=c_bool

#
SmartPower.SmartPower_OutputOff.argtypes=[c_int]
SmartPower.SmartPower_OutputOff.restype=c_bool

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# demo code: var define
ArrNums=4
fArr_ValueRead=(c_double*ArrNums)()
bArr_StatusRead=(c_int*ArrNums)()

cStr_DeviceSN=(c_char*60)()

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# demo code : Normal func <<<<<<<<<<<<<<<<
iRet=SmartPower.SmartPower_DeviceNums()
bRet=SmartPower.SmartPower_DeviceOpen(0)

bRet=SmartPower.SmartPower_OutputOn(1,0.5)

bRet=SmartPower.SmartPower_GetStatus(fArr_ValueRead);
print("Voltage = ",fArr_ValueRead[0])
print("Current = ",fArr_ValueRead[1])
print("Bnc = ",fArr_ValueRead[2])

bRet=SmartPower.SmartPower_OutputUpdate(2,0.6)
bRet=SmartPower.SmartPower_GetStatus(fArr_ValueRead);

bRet=SmartPower.SmartPower_OutputOff(1)

bRet=SmartPower.SmartPower_GetStatus(fArr_ValueRead);

bRet=SmartPower.SmartPower_DeviceClose()

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# demo code : Ext func <<<<<<<<<<<<<<<<
iRet=SmartPower.SmartPower_DeviceNums()
bRet=SmartPower.SmartPower_DeviceOpen_Ext(0,cStr_DeviceSN)
# cStr_DeviceSN[0,2,4 ... ] is the Devide's SN, end at 0x00

bRet=SmartPower.SmartPower_OutputOn_Ext(1,0.5,50,0.55,50)

SmartPower.SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);
print("Voltage = ",fArr_ValueRead[0])
print("Current = ",fArr_ValueRead[1])
print("Bnc = ",fArr_ValueRead[2])

print("OV status = ",bArr_StatusRead[0])
print("OC status = ",bArr_StatusRead[1])
print("OT status = ",bArr_StatusRead[2])
print("DC20V status = ",bArr_StatusRead[3])

bRet=SmartPower.SmartPower_OutputOn_Ext(2,0.6,50,0.55,50)
SmartPower.SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);

bRet=SmartPower.SmartPower_OutputOff(1)

SmartPower.SmartPower_GetStatus_Ext(fArr_ValueRead,bArr_StatusRead);

bRet=SmartPower.SmartPower_DeviceClose()

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
print("Hello, www.smartxbw.com ")
