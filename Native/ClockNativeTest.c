// SPDX-License-Identifier: MPL-2.0
/* Desktop ABI/logic tests. All operating-system time and COM calls are mocked. */
#define __declspec(x)
#include "ClockNative.c"
static Guid allocation;
static int moduleFound=1,methodFound=1,writeSucceeds=1,writeCalls=0,tokenState=0,openFails=0,infoFails=0,lookupFails=0,apiMissing=0,closeCalls=0,lookupMissing=0;
static SystemTime captured;
static U32 mockError=5;static I32 ntStatus=0;static int ntCalls=0,ntEntryMissing=0,convertMissing=0,convertFails=0,ntModuleMissing=0;static U32 fileLow=0,fileHigh=0;
I32 __stdcall WindowsCreateString(const U16*s,U32 n,HString*out){(void)n;*out=(void*)s;return 0;}
const U16* __stdcall WindowsGetStringRawBuffer(HString s,U32*n){const U16*p=(const U16*)s;*n=0;while(p[*n])(*n)++;return p;}
void* __stdcall CoTaskMemAlloc(U32 n){(void)n;return &allocation;}
void* __stdcall GetModuleHandleW(const U16*s){if(ntModuleMissing&&s[0]=='n')return 0;return moduleFound?(void*)1:0;}
static int __stdcall fakeSet(const SystemTime*s){writeCalls++;captured=*s;return writeSucceeds;}
static int same(const char*a,const char*b){while(*a&&*a==*b){a++;b++;}return *a==*b;}
static int __stdcall fakeOpen(void*p,U32 access,void**out){if(openFails||p!=(void*)(__INTPTR_TYPE__)-1||access!=8)return 0;*out=(void*)1;return 1;}
static int __stdcall fakeInfo(void*t,U32 kind,void*out,U32 capacity,U32*length){(void)t;if(infoFails||kind!=3||capacity<28)return 0;U32*p=(U32*)out;p[0]=tokenState==0?1:2;p[1]=9;p[2]=0;p[3]=0;p[4]=12;p[5]=0;p[6]=tokenState==2?2:tokenState==3?4:0;*length=4+12*p[0];return 1;}
static int __stdcall fakeLookup(const U16*system,const U16*name,void*out){(void)system;(void)name;if(lookupFails)return 0;U32*p=(U32*)out;p[0]=12;p[1]=0;return 1;}
static int __stdcall fakeClose(void*t){(void)t;closeCalls++;return 1;}
static int __stdcall fakeConvert(const SystemTime*st,void*out){captured=*st;if(convertFails)return 0;U32*p=(U32*)out;p[0]=1234;p[1]=5678;return 1;}
static I32 __stdcall fakeNtSet(const void*time,void*old){if(old||((__UINTPTR_TYPE__)time&7))return (I32)0xc000000d;const U32*p=(const U32*)time;fileLow=p[0];fileHigh=p[1];ntCalls++;return ntStatus;}
static U32 __stdcall fakeMap(I32 status){(void)status;return 1314;}
void* __stdcall GetProcAddress(void*m,const char*s){(void)m;if(same(s,"SetSystemTime"))return methodFound?(void*)&fakeSet:0;if(same(s,"SystemTimeToFileTime"))return convertMissing?0:(void*)&fakeConvert;if(same(s,"NtSetSystemTime"))return ntEntryMissing?0:(void*)&fakeNtSet;if(same(s,"RtlNtStatusToDosError"))return (void*)&fakeMap;if(apiMissing)return 0;if(same(s,"OpenProcessToken"))return (void*)&fakeOpen;if(same(s,"GetTokenInformation"))return (void*)&fakeInfo;if(same(s,"LookupPrivilegeValueW"))return lookupMissing?0:(void*)&fakeLookup;if(same(s,"CloseHandle"))return (void*)&fakeClose;return 0;}
U32 __stdcall GetLastError(void){return mockError;}
#define CHECK(x,n) if(!(x))return n
__attribute__((dllexport)) int __stdcall RunTests(void){
 CHECK(sizeof(SystemTime)==16,1);
 void *f=0,*c=0,*q=0;I32 result=-9;
 CHECK(DllGetActivationFactory((HString)L"ClockNative.Clock",&f)==0&&f==&factory,2);
 CHECK(factoryTable.base.query(f,&activation,&q)==0&&q==f,3);
 CHECK(factoryTable.activate(f,&c)==0&&c==&clock,4);
 CHECK(clockTable.base.query(c,&clockid,&q)==0&&q==c,5);
 CHECK(clockTable.base.query(c,&agile,&q)==0,6);
 CHECK(DllGetActivationFactory((HString)L"Wrong.Class",&q)==(I32)0x80040111&&q==0,7);
 CHECK(clockTable.set(c,2026,11,5,15,40,29,123,&result)==0&&result==0,8);
 CHECK(captured.year==2026&&captured.month==11&&captured.day==5&&captured.hour==15&&captured.minute==40&&captured.second==29&&captured.millisecond==123,9);
 int before=writeCalls;clockTable.set(c,2026,11,5,24,0,0,0,&result);CHECK(result==87&&writeCalls==before,10);
 writeSucceeds=0;clockTable.set(c,2026,11,5,15,40,29,0,&result);CHECK(result==5,11);
 methodFound=0;clockTable.set(c,2026,11,5,15,40,29,0,&result);CHECK(result==127,12);
 moduleFound=0;clockTable.set(c,2026,11,5,15,40,29,0,&result);CHECK(result==126,13);
 U32 count=0;Guid*ids=0;CHECK(clockTable.base.iids(c,&count,&ids)==0&&count==1&&equal(ids,&clockid),14);
 HString s=0;CHECK(clockTable.base.name(c,&s)==0,15);
 I32 level=-1;CHECK(clockTable.base.trust(c,&level)==0&&level==0,16);
 moduleFound=1;before=writeCalls;int closed=closeCalls;
 tokenState=0;CHECK(clockTable.permission(c,&result)==0&&result==0,17);
 tokenState=1;CHECK(clockTable.permission(c,&result)==0&&result==1,18);
 tokenState=2;CHECK(clockTable.permission(c,&result)==0&&result==2,19);
 tokenState=3;CHECK(clockTable.permission(c,&result)==0&&result==0,20);
 CHECK(closeCalls==closed+4&&writeCalls==before,21);
 openFails=1;clockTable.permission(c,&result);CHECK(result==100005,22);openFails=0;
 infoFails=1;clockTable.permission(c,&result);CHECK(result==200005,23);infoFails=0;
 lookupFails=1;clockTable.permission(c,&result);CHECK(result==300005,24);lookupFails=0;
 apiMissing=1;clockTable.permission(c,&result);CHECK(result==400001,25);
 CHECK(writeCalls==before,26);
 apiMissing=0;lookupMissing=1;
 tokenState=2;clockTable.permission(c,&result);CHECK(result==2,27);
 tokenState=1;clockTable.permission(c,&result);CHECK(result==1,28);
 tokenState=0;clockTable.permission(c,&result);CHECK(result==0,29);
 CHECK(writeCalls==before,30);
 methodFound=1;writeSucceeds=0;mockError=1314;
 clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==0&&ntCalls==1,31);
 CHECK(captured.hour==0&&captured.second==50&&fileLow==1234&&fileHigh==5678,32);
 ntStatus=(I32)0xc0000061;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==1314&&ntCalls==2,33);
 ntEntryMissing=1;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==127&&ntCalls==2,34);ntEntryMissing=0;
 convertMissing=1;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==127&&ntCalls==2,35);convertMissing=0;
 ntModuleMissing=1;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==126&&ntCalls==2,36);ntModuleMissing=0;
 mockError=5;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==5&&ntCalls==2,37);
 writeSucceeds=1;clockTable.set(c,2026,11,6,0,33,50,123,&result);CHECK(result==0&&ntCalls==2,38);
 return 0;
}
