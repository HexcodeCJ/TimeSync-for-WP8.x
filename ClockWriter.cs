// SPDX-License-Identifier: MPL-2.0
using System;
using System.Runtime.InteropServices;
namespace TimeSync {
 public sealed class ClockWriteResult { public bool Success;public string Message; }
 public static class ClockWriter {
  [StructLayout(LayoutKind.Sequential)]
  public struct SystemTime {
   public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Milliseconds;
   public static SystemTime FromUtc(DateTime utc) {
    if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("UTC required");
    return new SystemTime{Year=(ushort)utc.Year,Month=(ushort)utc.Month,DayOfWeek=(ushort)utc.DayOfWeek,Day=(ushort)utc.Day,Hour=(ushort)utc.Hour,Minute=(ushort)utc.Minute,Second=(ushort)utc.Second,Milliseconds=(ushort)utc.Millisecond};
   }
  }
  public static ClockWriteResult Apply(Func<DateTime> targetUtc) {
   return Apply(targetUtc,WriteNative,()=>DateTime.UtcNow);
  }
  static int WriteNative(SystemTime value) {
   var bridge=new ClockNative.Clock();
   return bridge.SetUtc(value.Year,value.Month,value.Day,value.Hour,value.Minute,value.Second,value.Milliseconds);
  }
  public static ClockWriteResult Apply(Func<DateTime> targetUtc,Func<SystemTime,int> write,Func<DateTime> readUtc) {
   try {
    var target=targetUtc();var value=SystemTime.FromUtc(target);
    int error=write(value);
    if(error!=0) {
     string detail=error==5||error==1314?App.T("（系统拒绝写入或缺少校时权限）"," (access denied or clock privilege missing)"):error==126||error==127?App.T("（未找到系统校时模块或入口）"," (system module or entry point unavailable)"):"";
     string code=error < -1?"NTSTATUS 0x"+error.ToString("X8"):error.ToString();
     return new ClockWriteResult{Message=App.T("系统时间写入失败，错误码 ","System clock write failed, error ")+code+detail};
    }
    // Confirm the clock actually changed rather than trusting a success return alone.
    double remaining=(targetUtc()-readUtc()).TotalMilliseconds;
    if(Math.Abs(remaining)>1500)return new ClockWriteResult{Message=App.T("接口返回成功，但回读校验未通过。剩余偏差：","API returned success, but read-back verification failed. Remaining offset: ")+remaining.ToString("0")+" ms"};
    return new ClockWriteResult{Success=true,Message=App.T("系统时间已校准，回读校验通过。","System time set; read-back verified.")};
   }catch(Exception ex) {
    var com=ex as COMException;string code=com==null?"":" (0x"+com.ErrorCode.ToString("X8")+")";
    return new ClockWriteResult{Message=App.T("未能写入系统时间：","Could not write system time: ")+ex.GetType().Name+code+"\n"+ex.Message};
   }
  }
 }
}





