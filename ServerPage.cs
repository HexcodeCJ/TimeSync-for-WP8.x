// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Shell;
namespace TimeSync {
 public static class ServerText {
  public static string Status(ServerStatus s){
   if(s.State==ServerState.Checking)return App.T("检测中…","Checking…");
   if(s.State==ServerState.Available)return string.Format(CultureInfo.InvariantCulture,App.T("✓ 可用 · {0:0} ms","✓ Available · {0:0} ms"),s.DelayMs);
   if(s.State==ServerState.Unavailable){string reason=s.Reason=="timeout"?App.T("超时","timeout"):s.Reason=="invalid"?App.T("响应无效","invalid response"):App.T("网络或 DNS 错误","network or DNS error");return App.T("× 不可用 · ","× Unavailable · ")+reason;}
   return App.T("— 未检测","— Not checked");
  }
 }
 public partial class ServerPage : PhonePage {
  readonly Dictionary<string,TextBlock> labels=new Dictionary<string,TextBlock>();
  readonly ListBox list=new ListBox();CancellationTokenSource scan;bool active,running;
  ApplicationBarIconButton refresh;
  public ServerPage():base(App.T("服务器","servers")){
   InitializeComponent();PageScroll.Content=null;LayoutRoot.Children.Remove(PageScroll);
   list.Margin=new Thickness(24,0,24,24);list.HorizontalContentAlignment=HorizontalAlignment.Stretch;
   ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
   list.Items.Add(new TextBlock{Text=App.T("自动选择","Automatic")+"\n"+App.T("按预设顺序采用首个有效响应。列表显示最近检测结果。","Use the first valid response in preset order. The list shows the last check."),FontSize=25,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)});
   foreach(string host in ServerCatalog.Hosts){var row=new TextBlock{Text=Loc.Ltr(host)+"\n"+App.T("— 未检测","— Not checked"),FontSize=26,TextWrapping=TextWrapping.Wrap,FlowDirection=FlowDirection.LeftToRight,Margin=new Thickness(0,12,0,12)};labels.Add(host,row);list.Items.Add(row);}
   string selected=Pref.Get("server",ServerCatalog.Auto);int index=Array.IndexOf(ServerCatalog.Hosts,selected);list.SelectedIndex=selected==ServerCatalog.Auto?0:index<0?0:index+1;
   list.Tap+=(s,e)=>{if(list.SelectedIndex<0)return;string value=list.SelectedIndex==0?ServerCatalog.Auto:ServerCatalog.Hosts[list.SelectedIndex-1];Pref.Set("server",value);if(NavigationService.CanGoBack)NavigationService.GoBack();};
   Grid.SetRow(list,1);LayoutRoot.Children.Add(list);
   ApplicationBar=new ApplicationBar();refresh=new ApplicationBarIconButton(new Uri("/Assets/sync.png",UriKind.Relative)){Text=App.T("重新检测","check again")};refresh.Click+=async(s,e)=>await Scan(true);ApplicationBar.Buttons.Add(refresh);
   Loaded+=async(s,e)=>{active=true;ServerCatalog.Client.Changed-=Changed;ServerCatalog.Client.Changed+=Changed;if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem);await Scan(false);};
  }
  void Update(string host){TextBlock row;if(labels.TryGetValue(host,out row))row.Text=Loc.Ltr(host)+"\n"+ServerText.Status(ServerCatalog.Client.Get(host));}
  void Changed(string host){Dispatcher.BeginInvoke(()=>{if(active){try{Update(host);}catch(Exception ex){App.RecordError(ex);}}});}
  protected override void OnNavigatedFrom(NavigationEventArgs e){active=false;ServerCatalog.Client.Changed-=Changed;if(scan!=null)scan.Cancel();base.OnNavigatedFrom(e);}
  async Task Scan(bool force){
   if(running||!active)return;running=true;refresh.IsEnabled=false;var source=new CancellationTokenSource();scan=source;var token=source.Token;
   try{foreach(string host in ServerCatalog.Hosts){token.ThrowIfCancellationRequested();var state=ServerCatalog.Client.Get(host);Update(host);if(!force&&(state.State==ServerState.Available||state.State==ServerState.Unavailable)&&state.Age.Elapsed.TotalSeconds<60)continue;try{await ServerCatalog.Client.Probe(host,token);}catch(OperationCanceledException){throw;}catch(Exception){Update(host);}}}
   catch(OperationCanceledException){}
   catch(Exception ex){App.RecordError(ex);if(active)MessageBox.Show(App.T("服务器检测失败：","Server check failed: ")+ex.GetType().Name+"\n"+ex.Message);}
   finally{running=false;refresh.IsEnabled=true;source.Dispose();if(scan==source)scan=null;}
  }
 }
}
