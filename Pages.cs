// SPDX-License-Identifier: MPL-2.0
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
namespace TimeSync {
 public class PhonePage : PhoneApplicationPage {
  protected StackPanel Body=new StackPanel { Margin=new Thickness(24,0,24,24) };
  protected Grid LayoutRoot;
  protected ScrollViewer PageScroll;
  public PhonePage():this(""){}
  public PhonePage(string title) {
   FlowDirection=Loc.Rtl?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
   SystemTray.SetIsVisible(this,true);SystemTray.SetOpacity(this,1);
   SupportedOrientations=SupportedPageOrientation.Portrait;
   var root=LayoutRoot=new Grid();root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });root.RowDefinitions.Add(new RowDefinition());
   var head=new StackPanel { Margin=Loc.Rtl?new Thickness(0,16,24,28):new Thickness(24,16,0,28) };
   head.Children.Add(new TextBlock { Text=App.T("校时","TIME SYNC"),FontSize=20,TextAlignment=Loc.Rtl?TextAlignment.Right:TextAlignment.Left });
   head.Children.Add(new TextBlock { Text=title,FontSize=60,FontWeight=FontWeights.Light,TextWrapping=TextWrapping.Wrap,TextAlignment=Loc.Rtl?TextAlignment.Right:TextAlignment.Left });root.Children.Add(head);
   var scroll=PageScroll=new ScrollViewer{Content=Body};Grid.SetRow(scroll,1);root.Children.Add(scroll);Content=root;
  }
  protected TextBlock Text(string text,double size) {var t=new TextBlock {Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,TextAlignment=Loc.Rtl?TextAlignment.Right:TextAlignment.Left};Body.Children.Add(t);return t;}
  protected void Label(string title) {var t=Text(title,24);t.Foreground=(Brush)Application.Current.Resources["PhoneSubtleBrush"];t.Margin=new Thickness(0,24,0,8);}
 }
 public partial class MainPage : PhonePage {
  TextBlock date,time,serverDate,serverTime,status;DispatcherTimer timer;NtpResult sample;Stopwatch elapsed=new Stopwatch();bool busy;
  public MainPage():base(App.T("时间","time")) {
   InitializeComponent();Label(App.T("当前时间：","Current time:"));date=Text("",30);time=Text("",64);
   Label(App.T("服务器时间：","Server time:"));serverDate=Text("—",30);serverTime=Text("—",64);
   status=Text(App.RecoveryNote??App.T("尚未同步","Not synchronised"),22);status.Margin=new Thickness(0,24,0,0);
   ApplicationBar=new ApplicationBar();
   var refresh=new ApplicationBarIconButton(new Uri("/Assets/sync.png",UriKind.Relative)){Text=App.T("校时","sync")};refresh.Click+=async(s,e)=>await Sync(true);ApplicationBar.Buttons.Add(refresh);
   var settings=new ApplicationBarIconButton(new Uri("/Assets/settings.png",UriKind.Relative)){Text=App.T("设置","settings")};settings.Click+=(s,e)=>NavigationService.Navigate(new Uri("/SettingsPage.xaml",UriKind.Relative));ApplicationBar.Buttons.Add(settings);
   var write=new ApplicationBarMenuItem(App.T("校准系统时间","Set system time"));write.Click+=async(s,e)=>await Sync(true);ApplicationBar.MenuItems.Add(write);
   var info=new ApplicationBarMenuItem(App.T("校时结果 / 诊断","Result / diagnostics"));info.Click+=(s,e)=>{string message=lastWrite??App.T("尚未尝试写入系统时间。","No clock write attempted.");string error=Pref.Get("lastError","");if(error.Length>0)message+="\n\n"+App.T("应用错误记录：","Application error log: ")+error;MessageBox.Show(message);};ApplicationBar.MenuItems.Add(info);
   timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(s,e)=>Tick();
   foreach(var value in new[]{date,time,serverDate,serverTime}){value.FlowDirection=FlowDirection.LeftToRight;value.HorizontalAlignment=Loc.Rtl?HorizontalAlignment.Right:HorizontalAlignment.Left;}
  }
  protected override async void OnNavigatedTo(NavigationEventArgs e){base.OnNavigatedTo(e);Tick();timer.Start();if(sample==null||elapsed.Elapsed.TotalMinutes>15)await Sync();}
  protected override void OnNavigatedFrom(NavigationEventArgs e){timer.Stop();base.OnNavigatedFrom(e);}
  DateTime Display(DateTime utc) {return utc.AddMinutes(int.Parse(Pref.Get("zone","480"),CultureInfo.InvariantCulture));}
  void Tick(){var now=Display(DateTime.UtcNow);date.Text=now.ToString(Pref.Get("format","yyyy/MM/dd"),CultureInfo.InvariantCulture);time.Text=now.ToString("HH:mm:ss",CultureInfo.InvariantCulture);
   if(sample!=null){var n=Display(sample.Utc+elapsed.Elapsed);serverDate.Text=n.ToString(Pref.Get("format","yyyy/MM/dd"),CultureInfo.InvariantCulture);serverTime.Text=n.ToString("HH:mm:ss",CultureInfo.InvariantCulture);}
  }
  string lastWrite;
  async System.Threading.Tasks.Task Sync(bool forceWrite=false){if(busy)return;busy=true;status.Text=App.T("正在同步…","Synchronising…");
   try {
    string selected=Pref.Get("server",ServerCatalog.Auto);
    NtpResult best=await ServerCatalog.Client.Select(selected,System.Threading.CancellationToken.None,host=>status.Text=App.T("正在尝试：","Trying: ")+Loc.Ltr(host));
    // Re-anchor from the four-timestamp offset after all requests have completed.
    best.Utc=DateTime.UtcNow.AddMilliseconds(best.OffsetMs);sample=best;elapsed.Restart();Tick();
    bool attempt=forceWrite||Pref.Get("autoSet","on")=="on";
    string outcome=App.T("已获取服务器时间；未修改系统时间。","Server time received; system clock unchanged.");
    if(attempt) {
     Pref.Set("nativeWritePending","on");
     var result=ClockWriter.Apply(()=>sample.Utc+elapsed.Elapsed);
     Pref.Set("nativeWritePending","off");
     lastWrite=result.Message;outcome=result.Message;
     if(result.Success)Tick();
    }
    else if(App.RecoveryNote!=null)outcome+="\n"+App.RecoveryNote;
    status.Text=string.Format(CultureInfo.InvariantCulture,App.T("{0}\n校时前偏差 {1:+0.000;-0.000;0.000} 秒 · 往返 {2:0} ms\n{3}","{0}\nOffset before write {1:+0.000;-0.000;0.000} s · RTT {2:0} ms\n{3}"),Loc.Ltr(best.Host),best.OffsetMs/1000,best.DelayMs,outcome);
   }catch(Exception){status.Text=App.T("同步失败，请检查 Wi-Fi 或在设置中更换服务器。","Sync failed. Check Wi-Fi or select another server.")+(sample!=null?App.T("\n仍显示上次同步的推算时间。","\nShowing the previous sample extrapolated."):"");}finally{busy=false;}
  }
 }
 public partial class SettingsPage : PhonePage {
  public SettingsPage():base(App.T("设置","settings")) {
   InitializeComponent();
   Choice(App.T("进入程序后自动校时","Set time on opening"),"autoSet",new[]{App.T("开","On"),App.T("关","Off")},new[]{"on","off"},"on");
   Label(App.T("NTP 服务器","NTP server"));var serverButton=new Button{HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0)};Action showServer=()=>{string selected=Pref.Get("server",ServerCatalog.Auto);string title=selected==ServerCatalog.Auto?App.T("自动选择（预设顺序）","Automatic (preset order)"):selected+"\n"+ServerText.Status(ServerCatalog.Client.Get(selected));serverButton.Content=new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,FontSize=25};};showServer();Loaded+=(s,e)=>showServer();serverButton.Click+=(s,e)=>NavigationService.Navigate(new Uri("/ServerPage.xaml",UriKind.Relative));Body.Children.Add(serverButton);
   string[] citiesZh={"贝克岛","帕果帕果","檀香山","甘比尔群岛","皮特凯恩群岛","凤凰城","墨西哥城","波哥大","圣胡安（波多黎各）","圣约翰斯","布宜诺斯艾利斯","南乔治亚岛","佛得角","雷克雅未克","阿尔及尔","约翰内斯堡","内罗毕","德黑兰","迪拜","喀布尔","卡拉奇","新德里 / 科伦坡","加德满都","达卡","仰光","曼谷","北京 / 香港 / 台北","尤克拉","东京 / 首尔","达尔文","布里斯班","豪勋爵岛","努美阿","斐济","查塔姆群岛","萨摩亚","基里蒂马蒂岛"};
   string[] citiesEn={"Baker Island","Pago Pago","Honolulu","Gambier Islands","Pitcairn Islands","Phoenix","Mexico City","Bogota","San Juan, Puerto Rico","St. John's","Buenos Aires","South Georgia","Cape Verde","Reykjavik","Algiers","Johannesburg","Nairobi","Tehran","Dubai","Kabul","Karachi","New Delhi / Colombo","Kathmandu","Dhaka","Yangon","Bangkok","Beijing / Hong Kong / Taipei","Eucla","Tokyo / Seoul","Darwin","Brisbane","Lord Howe Island","Noumea","Fiji","Chatham Islands","Samoa","Kiritimati"};
   string[] offsets={"-720","-660","-600","-540","-480","-420","-360","-300","-240","-210","-180","-120","-60","0","60","120","180","210","240","270","300","330","345","360","390","420","480","525","540","570","600","630","660","720","765","780","840"};
   string[] names=new string[offsets.Length];for(int i=0;i<names.Length;i++){int minutes=int.Parse(offsets[i],CultureInfo.InvariantCulture);int absolute=Math.Abs(minutes);string prefix=string.Format(CultureInfo.InvariantCulture,"UTC{0}{1:00}:{2:00}",minutes<0?"−":"+",absolute/60,absolute%60);names[i]=Loc.Ltr(prefix)+"  "+App.T(citiesZh[i],citiesEn[i]);}
   Choice(App.T("所在时区","Time zone"),"zone",names,offsets,"480");
   Text(App.T("时区仅影响应用显示。地点按标准时列举；使用固定 UTC 偏移，夏令时需手动调整。","Time zone affects this app only. Places show standard-time offsets; adjust daylight saving manually."),20);
   Choice(App.T("日期格式","Date format"),"format",new[]{"YYYY/MM/DD","YYYY-MM-DD","DD/MM/YYYY","MM/DD/YYYY"},new[]{"yyyy/MM/dd","yyyy-MM-dd","dd/MM/yyyy","MM/dd/yyyy"},"yyyy/MM/dd");
   Choice(App.T("软件语言","App language"),"language",Loc.DisplayNames(),Loc.Codes,"system");
   Text(App.T("更换语言后，请退出并重新启动应用。","Close and restart the app after changing language."),22);
  }
  void Choice(string label,string key,string[] names,string[] values,string fallback){Label(label);var b=new Button {HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0),FontSize=25};Action update=()=>{int i=Array.IndexOf(values,Pref.Get(key,fallback));b.Content=new TextBlock{Text=names[Math.Max(0,i)],TextWrapping=TextWrapping.Wrap,FontSize=25};};update();b.Click+=(s,e)=>{ChoicePage.TitleText=label;ChoicePage.Names=names;ChoicePage.Values=values;ChoicePage.Key=key;NavigationService.Navigate(new Uri("/ChoicePage.xaml",UriKind.Relative));};Loaded+=(s,e)=>update();Body.Children.Add(b);}
 }
 public partial class ChoicePage : PhonePage {
  public static string TitleText,Key;public static string[] Names,Values;
  public ChoicePage():base(TitleText??""){
   if(Names==null)return;
   // The native ListBox owns scrolling and receives the finite viewport directly.
   PageScroll.Content=null;LayoutRoot.Children.Remove(PageScroll);
   var list=new ListBox{FontSize=28,Margin=new Thickness(24,0,24,24),HorizontalContentAlignment=HorizontalAlignment.Stretch};
   ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
   foreach(string name in Names)list.Items.Add(new TextBlock{Text=name,FontSize=28,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)});
   list.SelectedIndex=Array.IndexOf(Values,Pref.Get(Key,""));
   list.Loaded+=(s,e)=>{if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem);};
   list.SelectionChanged+=(s,e)=>{if(list.SelectedIndex<0)return;Pref.Set(Key,Values[list.SelectedIndex]);if(NavigationService.CanGoBack)NavigationService.GoBack();};
   Grid.SetRow(list,1);LayoutRoot.Children.Add(list);
  }
 }
}








