// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace TimeSync {
 public enum ServerState { Unknown, Checking, Available, Unavailable }
 public sealed class ServerStatus {
  public ServerState State;public double DelayMs;public string Reason;public Stopwatch Age=Stopwatch.StartNew();
 }
 public static class ServerCatalog {
  public const string Auto="auto";
  public static readonly string[] Hosts={"ntp.aliyun.com","ntp1.aliyun.com","ntp.tencent.com","time.windows.com","time.cloudflare.com","time.google.com","time.aws.com","time.nist.gov"};
  public static readonly ServerClient Client=new ServerClient((h,c)=>Ntp.Query(h,c));
 }
 public sealed class ServerClient {
  readonly Func<string,CancellationToken,Task<NtpResult>> query;
  readonly object gateLock=new object();Task queue=Task.FromResult(true);
  readonly Dictionary<string,ServerStatus> states=new Dictionary<string,ServerStatus>();
  readonly object stateLock=new object();readonly int minimumIntervalMs;
  public event Action<string> Changed;
  public ServerClient(Func<string,CancellationToken,Task<NtpResult>> query,int minimumIntervalMs=4000){this.query=query;this.minimumIntervalMs=minimumIntervalMs;}
  public ServerStatus Get(string host){lock(stateLock){ServerStatus s;return states.TryGetValue(host,out s)?s:new ServerStatus{State=ServerState.Unknown};}}
  void Put(string host,ServerStatus status){lock(stateLock){states[host]=status;}var handler=Changed;if(handler!=null)handler(host);}
  public async Task<NtpResult> Probe(string host,CancellationToken token){
   token.ThrowIfCancellationRequested();Task previousQuery;var released=new TaskCompletionSource<bool>();lock(gateLock){previousQuery=queue;queue=released.Task;}await previousQuery;var previous=Get(host);
   try{
    if(previous.State==ServerState.Available||previous.State==ServerState.Unavailable){int wait=minimumIntervalMs-(int)Math.Min(int.MaxValue,previous.Age.ElapsedMilliseconds);if(wait>0)await Task.Delay(wait,token);}
    token.ThrowIfCancellationRequested();Put(host,new ServerStatus{State=ServerState.Checking});
    var result=await query(host,token);token.ThrowIfCancellationRequested();
    if(result==null)throw new FormatException("Empty NTP result");
    Put(host,new ServerStatus{State=ServerState.Available,DelayMs=result.DelayMs});return result;
   }catch(OperationCanceledException){Put(host,previous);throw;}
   catch(Exception ex){if(token.IsCancellationRequested){Put(host,previous);throw new OperationCanceledException(token);}Put(host,new ServerStatus{State=ServerState.Unavailable,Reason=ex is TimeoutException?"timeout":ex is FormatException?"invalid":"network"});throw;}
   finally{released.TrySetResult(true);}
  }
  // Preset order is authoritative. Never compare or sort response delays.
  public async Task<NtpResult> Select(string selection,CancellationToken token,Action<string> trying=null){
   string[] hosts=selection==ServerCatalog.Auto?ServerCatalog.Hosts:new[]{selection};Exception last=null;
   foreach(string host in hosts){
    token.ThrowIfCancellationRequested();if(trying!=null)trying(host);
    try{return await Probe(host,token);}catch(OperationCanceledException){throw;}catch(Exception ex){last=ex;}
   }
   throw last??new InvalidOperationException("No NTP servers configured");
  }
 }
}
