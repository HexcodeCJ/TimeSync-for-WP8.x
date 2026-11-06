// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
namespace TimeSync {
 public static class Loc {
  public static readonly string[] Codes={"system","zh-Hans","zh-Hant","en","es","fr","pt","ru","de","ja","id","hi","ar","ko","vi","tr"};
  public static readonly string[] Names={"跟随系统 / System","简体中文","繁體中文","English","Español","Français","Português","Русский","Deutsch","日本語","Bahasa Indonesia","हिन्दी","العربية","한국어","Tiếng Việt","Türkçe"};
  public static readonly string[] TranslationCodes={"zh-Hant","es","fr","pt","ru","de","ja","id","hi","ar","ko","vi","tr"};
  static readonly Dictionary<string,string[]> rows=new Dictionary<string,string[]>();
  public static string Language="en";
  public static bool Rtl { get{return Language=="ar";} }
  public static string Resolve(string preference,string system){
   string value=preference==null||preference=="system"?system:preference;
   if(value=="zh")return "zh-Hans";
   if(string.IsNullOrEmpty(value))return "en";
   string lower=value.ToLowerInvariant();
   if(lower.StartsWith("zh"))return lower.Contains("hant")||lower.EndsWith("-tw")||lower.EndsWith("-hk")||lower.EndsWith("-mo")?"zh-Hant":"zh-Hans";
   string code=lower.Split('-')[0];if(code=="in")code="id";
   return Array.IndexOf(Codes,code)>=0?code:"en";
  }
  public static void Initialize(){
   Language=Resolve(Pref.Get("language","system"),CultureInfo.CurrentUICulture.Name);
   var resource=Application.GetResourceStream(new Uri("/TimeSync;component/Localization/Strings.tsv",UriKind.Relative));
   if(resource==null)throw new InvalidOperationException("Missing localization resource");
   using(var reader=new StreamReader(resource.Stream,Encoding.UTF8))Load(reader);
  }
  public static void Load(TextReader reader){
   rows.Clear();string line;
   while((line=reader.ReadLine())!=null){if(line.Length==0||line.StartsWith("#"))continue;string[] parts=line.Split('|');if(parts.Length!=TranslationCodes.Length+1)throw new FormatException("Invalid localization column count: "+parts[0]);for(int i=0;i<parts.Length;i++)parts[i]=parts[i].Replace("\\n","\n");if(rows.ContainsKey(parts[0]))throw new FormatException("Duplicate localization key: "+parts[0]);rows.Add(parts[0],parts);}
  }
  public static string T(string zh,string en){
   if(Language=="zh-Hans")return zh;if(Language=="en")return en;
   int i=Array.IndexOf(TranslationCodes,Language);string[] parts;
   string value=i>=0&&rows.TryGetValue(en,out parts)&&!string.IsNullOrEmpty(parts[i+1])?parts[i+1]:en;
   if(en.EndsWith(" ")&&!value.EndsWith(" "))value+=" ";if(en.StartsWith(" ")&&!value.StartsWith(" "))value=" "+value;return value;
  }
  public static string Ltr(string text){return Rtl?"\u200e"+text+"\u200e":text;}
  public static string[] DisplayNames(){var result=(string[])Names.Clone();result[0]=T("跟随系统","Follow system");return result;}
 }
}
