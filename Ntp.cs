// SPDX-License-Identifier: MPL-2.0
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
namespace TimeSync {
 public class NtpResult { public DateTime Utc; public double OffsetMs; public double DelayMs; public string Host; }
 public static class Ntp {
  static readonly DateTime Epoch = new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc);
  public static byte[] Request(DateTime utc) { var p=new byte[48]; p[0]=0x23; Write(p,40,utc); return p; }
  static void Write(byte[] p,int i,DateTime utc) { double n=(utc-Epoch).TotalSeconds; ulong value=((ulong)n<<32)+(ulong)((n-Math.Floor(n))*4294967296.0); for(int j=7;j>=0;j--){p[i+j]=(byte)value;value>>=8;} }
  static DateTime Read(byte[] p,int i) { ulong a=0,b=0;for(int j=0;j<4;j++){a=(a<<8)|p[i+j];b=(b<<8)|p[i+4+j];} return Epoch.AddSeconds(a+b/4294967296.0); }
  public static NtpResult Parse(byte[] p,int length,byte[] request,DateTime t1,DateTime t4,string host) {
   if(p==null||length<48||p.Length<length)throw new FormatException("Short NTP response");
   int version=(p[0]>>3)&7;
   if(length<48 || (p[0]&7)!=4 || (version!=3&&version!=4) || (p[0]>>6)==3 || p[1]==0 || p[1]>15) throw new FormatException("Invalid or unsynchronised NTP response");
   for(int j=0;j<8;j++) if(p[24+j]!=request[40+j]) throw new FormatException("NTP request mismatch");
   var t2=Read(p,32);var t3=Read(p,40);
   if(t2.Year<2020 || t3<t2) throw new FormatException("Invalid NTP timestamp");
   double offset=((t2-t1).TotalMilliseconds+(t3-t4).TotalMilliseconds)/2;
   double delay=(t4-t1).TotalMilliseconds-(t3-t2).TotalMilliseconds;
   if(delay < -5 || delay>5000) throw new FormatException("Invalid NTP delay");
   return new NtpResult { Utc=t4.AddMilliseconds(offset),OffsetMs=offset,DelayMs=Math.Max(0,delay),Host=host };
  }
  static Task<int> Op(Socket socket,SocketAsyncEventArgs e,Func<SocketAsyncEventArgs,bool> action) {
   var t=new TaskCompletionSource<int>(); EventHandler<SocketAsyncEventArgs> done=null;
   done=(s,a)=>{a.Completed-=done;if(a.SocketError==SocketError.Success)t.TrySetResult(a.BytesTransferred);else t.TrySetException(new Exception(a.SocketError.ToString()));};
   e.Completed+=done; try {if(!action(e))done(socket,e);}catch(Exception ex){e.Completed-=done;t.TrySetException(ex);}return t.Task;
  }
  public static Task<NtpResult> Query(string host){return Query(host,CancellationToken.None);}
  public static async Task<NtpResult> Query(string host,CancellationToken token) {
   token.ThrowIfCancellationRequested();
   using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)) {
    var operation=Exchange(socket,host);
    if(await Task.WhenAny(operation,Task.Delay(4000,token))!=operation){
     socket.Close();ObserveFailure(operation);token.ThrowIfCancellationRequested();throw new TimeoutException(host);
    }
    return await operation;
   }
  }
  static void ObserveFailure(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
  static async Task<NtpResult> Exchange(Socket socket,string host) {
   using(var c=new SocketAsyncEventArgs()){c.RemoteEndPoint=new DnsEndPoint(host,123);await Op(socket,c,socket.ConnectAsync);}
   var t1=DateTime.UtcNow;var watch=Stopwatch.StartNew();var request=Request(t1);
   using(var s=new SocketAsyncEventArgs()){s.SetBuffer(request,0,48);await Op(socket,s,socket.SendAsync);}
   var p=new byte[512];int n;
   using(var r=new SocketAsyncEventArgs()){r.SetBuffer(p,0,p.Length);n=await Op(socket,r,socket.ReceiveAsync);}
   var t4=t1.Add(watch.Elapsed); return Parse(p,n,request,t1,t4,host);
  }
 }
}
