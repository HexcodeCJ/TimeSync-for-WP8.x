// SPDX-License-Identifier: MPL-2.0
/* Windows Runtime ABI bridge. No CRT, no C# P/Invoke. Stateless singleton COM objects. */
typedef unsigned int U32;typedef int I32;typedef unsigned short U16;typedef unsigned char U8;
typedef struct {U32 a;U16 b,c;U8 d[8];} Guid;
typedef struct {U16 year,month,weekday,day,hour,minute,second,millisecond;} SystemTime;
typedef void *HString;
__declspec(dllimport) I32 __stdcall WindowsCreateString(const U16*,U32,HString*);
__declspec(dllimport) const U16* __stdcall WindowsGetStringRawBuffer(HString,U32*);
__declspec(dllimport) void* __stdcall CoTaskMemAlloc(U32);
__declspec(dllimport) void* __stdcall GetModuleHandleW(const U16*);
__declspec(dllimport) void* __stdcall GetProcAddress(void*,const char*);
__declspec(dllimport) U32 __stdcall GetLastError(void);
static const Guid unknown={0,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const Guid inspectable={0xaf86e2e0,0xb12d,0x4c6a,{0x9c,0x5a,0xd7,0xaa,0x65,0x10,0x1e,0x90}};
static const Guid activation={0x35,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const Guid agile={0x94ea2b94,0xe9cc,0x49e0,{0xc0,0xff,0xee,0x64,0xca,0x8f,0x5b,0x90}};
static const Guid clockid={0x07cfb9b4,0x0fd6,0x4d04,{0x9f,0xb9,0x74,0x6f,0xa1,0x81,0xfe,0x5b}};
static int equal(const Guid*a,const Guid*b){const U8*x=(const U8*)a,*y=(const U8*)b;for(int i=0;i<16;i++)if(x[i]!=y[i])return 0;return 1;}
typedef struct {
 I32 (__stdcall *query)(void*,const Guid*,void**);
 U32 (__stdcall *add)(void*);U32 (__stdcall *release)(void*);
 I32 (__stdcall *iids)(void*,U32*,Guid**);
 I32 (__stdcall *name)(void*,HString*);I32 (__stdcall *trust)(void*,I32*);
} Inspect;
typedef struct {Inspect base;I32 (__stdcall *activate)(void*,void**);} FactoryTable;
typedef struct {Inspect base;I32 (__stdcall *set)(void*,I32,I32,I32,I32,I32,I32,I32,I32*);I32 (__stdcall *permission)(void*,I32*);} ClockTable;
static FactoryTable factoryTable;static ClockTable clockTable;
static FactoryTable *factory=&factoryTable;static ClockTable *clock=&clockTable;
static I32 __stdcall query(void*self,const Guid*id,void**out){
 if(!out)return (I32)0x80004003;*out=0;
 if(equal(id,&unknown)||equal(id,&inspectable)||equal(id,&agile)||(self==&factory?equal(id,&activation):equal(id,&clockid))){*out=self;return 0;}return (I32)0x80004002;
}
static U32 __stdcall add(void*self){(void)self;return 2;}
static U32 __stdcall release(void*self){(void)self;return 1;}
static I32 __stdcall iids(void*self,U32*count,Guid**ids){if(!count||!ids)return (I32)0x80004003;*count=0;*ids=(Guid*)CoTaskMemAlloc(sizeof(Guid));if(!*ids)return (I32)0x8007000e;**ids=self==&factory?activation:clockid;*count=1;return 0;}
static I32 __stdcall name(void*self,HString*out){(void)self;return WindowsCreateString((const U16*)L"ClockNative.Clock",17,out);}
static I32 __stdcall trust(void*self,I32*out){(void)self;if(!out)return (I32)0x80004003;*out=0;return 0;}
static I32 __stdcall activate(void*self,void**out){(void)self;if(!out)return (I32)0x80004003;*out=&clock;return 0;}
/* A rooted kernel may allow the native syscall while the Win32 wrapper rejects the token first. */
static I32 set_native(const SystemTime*st){
 typedef struct __declspec(align(8)){U32 low,high;} FileTime;
 typedef int (__stdcall *ConvertFn)(const SystemTime*,FileTime*);
 typedef I32 (__stdcall *NtSetFn)(const FileTime*,FileTime*);
 typedef U32 (__stdcall *ErrorFn)(I32);
 void*k=GetModuleHandleW((const U16*)L"kernelbase.dll");void*n=GetModuleHandleW((const U16*)L"ntdll.dll");
 if(!k||!n)return 126;
 ConvertFn convert=(ConvertFn)GetProcAddress(k,"SystemTimeToFileTime");NtSetFn ntset=(NtSetFn)GetProcAddress(n,"NtSetSystemTime");
 if(!convert||!ntset)return 127;
 FileTime time;if(!convert(st,&time)){U32 e=GetLastError();return e?(I32)e:-1;}
 I32 status=ntset(&time,0);if(status>=0)return 0;
 ErrorFn error=(ErrorFn)GetProcAddress(n,"RtlNtStatusToDosError");if(error)return (I32)error(status);
 return status;
}
static I32 __stdcall set(void*self,I32 year,I32 month,I32 day,I32 hour,I32 minute,I32 second,I32 ms,I32*result){
 (void)self;if(!result)return (I32)0x80004003;
 if(year<2020||year>2099||month<1||month>12||day<1||day>31||hour<0||hour>23||minute<0||minute>59||second<0||second>59||ms<0||ms>999){*result=87;return 0;}
 typedef int (__stdcall *SetFn)(const SystemTime*);
 void*module=GetModuleHandleW((const U16*)L"kernelbase.dll");if(!module)module=GetModuleHandleW((const U16*)L"kernel32.dll");
 if(!module){*result=126;return 0;}SetFn fn=(SetFn)GetProcAddress(module,"SetSystemTime");if(!fn){*result=127;return 0;}
 SystemTime st={(U16)year,(U16)month,0,(U16)day,(U16)hour,(U16)minute,(U16)second,(U16)ms};
 if(fn(&st)){*result=0;return 0;}U32 error=GetLastError();
 *result=error==1314?set_native(&st):(error?(I32)error:-1);return 0;
}
static void* find_api(const char*symbol){const U16*modules[]={(const U16*)L"kernelbase.dll",(const U16*)L"advapi32.dll",(const U16*)L"sechost.dll",(const U16*)L"api-ms-win-security-base-l1-1-0.dll"};for(int i=0;i<4;i++){void*m=GetModuleHandleW(modules[i]);if(m){void*p=GetProcAddress(m,symbol);if(p)return p;}}return 0;}
/* Read-only: opens the current process token with TOKEN_QUERY, never adjusts it. */
static I32 __stdcall permission(void*self,I32*result){
 (void)self;if(!result)return (I32)0x80004003;
 typedef struct{U32 low;I32 high;} Luid;
 typedef int (__stdcall *OpenFn)(void*,U32,void**);
 typedef int (__stdcall *InfoFn)(void*,U32,void*,U32,U32*);
 typedef int (__stdcall *LookupFn)(const U16*,const U16*,Luid*);
 typedef int (__stdcall *CloseFn)(void*);
 OpenFn open=(OpenFn)find_api("OpenProcessToken");InfoFn info=(InfoFn)find_api("GetTokenInformation");
 LookupFn lookup=(LookupFn)find_api("LookupPrivilegeValueW");CloseFn close=(CloseFn)find_api("CloseHandle");
 if(!open||!info||!close){*result=400000+(!open?1:!info?2:4);return 0;}
 /* NT well-known SE_SYSTEMTIME_PRIVILEGE LUID = {12,0}; use it when the name lookup API is absent. */
 Luid luid={12,0};if(lookup&&!lookup(0,(const U16*)L"SeSystemtimePrivilege",&luid)){*result=300000+(I32)GetLastError();return 0;}
 void*token=0;if(!open((void*)(__INTPTR_TYPE__)-1,8,&token)){*result=100000+(I32)GetLastError();return 0;}
 U32 buffer[385],length=0;
 if(!info(token,3,buffer,sizeof(buffer),&length)){*result=200000+(I32)GetLastError();close(token);return 0;}
 close(token);if(length<4||buffer[0]>128||length<4+buffer[0]*12){*result=500001;return 0;}
 *result=0;for(U32 i=0;i<buffer[0];i++){U32*j=&buffer[1+3*i];if(j[0]==luid.low&&(I32)j[1]==luid.high){*result=(j[2]&4)?0:((j[2]&2)?2:1);break;}}return 0;
}
static FactoryTable factoryTable={{query,add,release,iids,name,trust},activate};
static ClockTable clockTable={{query,add,release,iids,name,trust},set,permission};
__declspec(dllexport) I32 __stdcall DllGetActivationFactory(HString className,void**out){
 if(!out)return (I32)0x80004003;*out=0;U32 len=0;const U16*s=WindowsGetStringRawBuffer(className,&len);const U16*expected=(const U16*)L"ClockNative.Clock";
 if(len!=17)return (I32)0x80040111;for(U32 i=0;i<len;i++)if(s[i]!=expected[i])return (I32)0x80040111;*out=&factory;return 0;
}
__declspec(dllexport) I32 __stdcall DllCanUnloadNow(void){return 1;}
