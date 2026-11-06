// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO.IsolatedStorage;
using System.Globalization;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Navigation;
namespace TimeSync {
 public partial class App : Application {
  public static bool English;
  public static string RecoveryNote;
  public App() {
   InitializeComponent();if(Pref.Get("language","system")=="zh")Pref.Set("language","zh-Hans");Loc.Initialize();English=Loc.Language=="en";
   if(Pref.Get("diagnostic040","off")=="off"){Pref.Set("autoSet","off");Pref.Set("diagnostic040","on");}
   if(Pref.Get("nativeWritePending","off")=="on") {
    Pref.Set("autoSet","off");Pref.Set("nativeWritePending","off");
    RecoveryNote=T("上次校时写入未正常结束，已关闭自动校时，可手动重试。","The previous clock write did not finish. Automatic setting is now off; you can retry manually.");
   }
   var service = new PhoneApplicationService(); ApplicationLifetimeObjects.Add(service);
   var frame = new PhoneApplicationFrame(); RootVisual = frame;
   frame.NavigationFailed+=(s,e)=>{RecordError(e.Exception);e.Handled=true;MessageBox.Show(T("页面无法打开：","Could not open page: ")+e.Exception.GetType().Name+"\n"+e.Exception.Message);};
   UnhandledException+=(s,e)=>{RecordError(e.ExceptionObject);if(frame.Content is ServerPage){e.Handled=true;frame.Dispatcher.BeginInvoke(()=>{if(frame.CanGoBack)frame.GoBack();MessageBox.Show(T("服务器检测失败：","Server check failed: ")+e.ExceptionObject.GetType().Name+"\n"+e.ExceptionObject.Message);});}};
   service.Launching += (s,e) => frame.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
   service.Activated += (s,e) => { if (frame.Content == null) frame.Navigate(new Uri("/MainPage.xaml", UriKind.Relative)); };
  }
  public static string T(string zh, string en) { return Loc.T(zh,en); }
  public static void RecordError(Exception ex){try{Pref.Set("lastError",ex.ToString());}catch{}}
 }
 public static class Pref {
  public static string Get(string k, string fallback) { string v; return IsolatedStorageSettings.ApplicationSettings.TryGetValue(k,out v) ? v : fallback; }
  public static void Set(string k,string v) { IsolatedStorageSettings.ApplicationSettings[k]=v; IsolatedStorageSettings.ApplicationSettings.Save(); }
 }
}



