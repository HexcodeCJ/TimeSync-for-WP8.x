// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeSync;
public static class ServerTests050 {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static Task<NtpResult> Good(string h,double delay){return Task.FromResult(new NtpResult{Host=h,Utc=DateTime.UtcNow,DelayMs=delay});}
 static Task<NtpResult> Bad(Exception e){var t=new TaskCompletionSource<NtpResult>();t.SetException(e);return t.Task;}
 public static async Task<string> Run(){
  var calls=new List<string>();var hosts=ServerCatalog.Hosts;
  var client=new ServerClient((h,c)=>{calls.Add(h);return Good(h,h==hosts[0]?500:1);},0);
  var result=await client.Select(ServerCatalog.Auto,CancellationToken.None);
  Check(result.Host==hosts[0]&&calls.Count==1,"Must use first valid response, not fastest");
  calls.Clear();client=new ServerClient((h,c)=>{calls.Add(h);if(h==hosts[0])return Bad(new TimeoutException());if(h==hosts[1])return Bad(new FormatException());return Good(h,100);},0);
  result=await client.Select(ServerCatalog.Auto,CancellationToken.None);
  Check(result.Host==hosts[2]&&calls.Count==3&&calls[0]==hosts[0]&&calls[1]==hosts[1],"Ordered failure fallback");
  Check(client.Get(hosts[0]).Reason=="timeout"&&client.Get(hosts[1]).Reason=="invalid"&&client.Get(hosts[2]).State==ServerState.Available,"Row status and reasons");
  calls.Clear();client=new ServerClient((h,c)=>{calls.Add(h);return Bad(new TimeoutException());},0);
  bool failed=false;try{await client.Select("time.google.com",CancellationToken.None);}catch(TimeoutException){failed=true;}
  Check(failed&&calls.Count==1&&calls[0]=="time.google.com","Manual mode must not fall back");
  calls.Clear();failed=false;try{await client.Select(ServerCatalog.Auto,CancellationToken.None);}catch(TimeoutException){failed=true;}
  Check(failed&&calls.Count==hosts.Length,"All hosts exhausted");
  var cts=new CancellationTokenSource();cts.Cancel();calls.Clear();try{await client.Select(ServerCatalog.Auto,cts.Token);}catch(OperationCanceledException){}
  Check(calls.Count==0,"Pre-cancel must not query");
  cts=new CancellationTokenSource();client=new ServerClient((h,c)=>{cts.Cancel();return Good(h,10);},0);bool cancelled=false;
  try{await client.Select(ServerCatalog.Auto,cts.Token);}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled&&client.Get(hosts[0]).State==ServerState.Unknown,"Cancellation is not unavailability");
  cts=new CancellationTokenSource();client=new ServerClient((h,c)=>{cts.Cancel();return Bad(new TimeoutException());},0);cancelled=false;
  try{await client.Select(ServerCatalog.Auto,cts.Token);}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled&&client.Get(hosts[0]).State==ServerState.Unknown,"Cancelled transport failure must not mark unavailable");
  int inFlight=0,max=0;client=new ServerClient(async(h,c)=>{inFlight++;max=Math.Max(max,inFlight);await Task.Delay(20);inFlight--;return await Good(h,20);},0);
  await Task.WhenAll(client.Probe(hosts[0],CancellationToken.None),client.Probe(hosts[1],CancellationToken.None));
  Check(max==1,"Queries must serialize across selection and scan");
  foreach(string h in new[]{"time.google.com","time.aws.com","time.nist.gov"})Check(Array.IndexOf(hosts,h)>=0,"Missing new host");
  return "PASS: preset order; first-valid rather than lowest RTT; timeout/invalid fallback; fixed manual host; all-failed; cancellation; serialized probes; new endpoints.";
 }
}
