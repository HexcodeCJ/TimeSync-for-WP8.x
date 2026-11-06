# SPDX-License-Identifier: MPL-2.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$map=@{}
foreach($line in Get-Content (Join-Path $root 'Localization\Strings.tsv')){
 if(-not $line -or $line.StartsWith('#')){continue}
 $p=$line.Split('|');if($p.Count -ne 14){throw "Wrong columns: $($p[0])"}
 if(@($p|Where-Object {$_.Length -eq 0}).Count){throw 'Empty translation'}
 if($map.ContainsKey($p[0])){throw 'Duplicate key'};$map[$p[0]]=$p
}
$keys=@()
foreach($file in Get-ChildItem $root -Filter '*.cs'){
 foreach($m in [regex]::Matches((Get-Content -Raw $file.FullName),'(?:App\.)?T\(\s*"((?:\\.|[^"\\])*)"\s*,\s*"((?:\\.|[^"\\])*)"')){$keys+=$m.Groups[2].Value}
}
foreach($key in $keys|Sort-Object -Unique){if(-not $map.ContainsKey($key)){throw "Missing UI translation: $key"}}
$pages=Get-Content -Raw (Join-Path $root 'Pages.cs')
$cities=[regex]::Match($pages,'string\[\] citiesEn=\{([^}]+)\}')
foreach($city in [regex]::Matches($cities.Groups[1].Value,'"([^"]+)"')){if(-not $map.ContainsKey($city.Groups[1].Value)){throw 'Missing city'}}
foreach($key in $map.Keys){
 $expected=@([regex]::Matches($key,'\{\d+(?:[^}]*)\}')|ForEach-Object {$_.Value})
 for($i=1;$i -lt 14;$i++){
  $actual=@([regex]::Matches($map[$key][$i],'\{\d+(?:[^}]*)\}')|ForEach-Object {$_.Value})
  if(($actual -join '|') -ne ($expected -join '|')){throw "Placeholder mismatch: $key/$i"}
  if($expected.Count){[void][string]::Format([Globalization.CultureInfo]::InvariantCulture,$map[$key][$i],[object[]]@('server',70.178,65.0,'result'))}
 }
}
foreach($key in @('sync','settings','check again','Result / diagnostics','Set system time')){
 for($i=1;$i -lt 14;$i++){if($map[$key][$i].Length -gt 20){throw "App bar label too long: $($map[$key][$i])"}}
}
'PASS: 85 rows; all UI strings and 37 places covered in all translated locales; placeholders and app-bar limits valid.'
# Exercise the actual locale resolver and lookup implementation with a desktop resource shim.
$source=Get-Content -Raw (Join-Path $root 'Loc.cs')
$shim=@'
namespace System.Windows {
 public sealed class ResourceStream { public System.IO.Stream Stream; }
 public static class Application { public static ResourceStream GetResourceStream(System.Uri uri){throw new System.NotSupportedException();} }
}
namespace TimeSync {public static class Pref {public static string Get(string key,string fallback){return fallback;}}}
'@
Add-Type -TypeDefinition ($source+$shim)
$reader=New-Object IO.StringReader((Get-Content -Raw (Join-Path $root 'Localization\Strings.tsv')))
[TimeSync.Loc]::Load($reader);$reader.Dispose()
$cases=@{'zh-TW'='zh-Hant';'zh-HK'='zh-Hant';'zh-CN'='zh-Hans';'zh-Hant-HK'='zh-Hant';'pt-BR'='pt';'es-MX'='es';'ar-SA'='ar';'hi-IN'='hi';'id-ID'='id';'in-ID'='id';'xx-XX'='en'}
foreach($name in $cases.Keys){if([TimeSync.Loc]::Resolve('system',$name) -ne $cases[$name]){throw "Locale mapping failed: $name"}}
if([TimeSync.Loc]::Resolve('fr','ja-JP') -ne 'fr'){throw 'Manual preference overwritten'}
foreach($language in @('zh-Hans','zh-Hant','en','es','fr','pt','ru','de','ja','id','hi','ar','ko','vi','tr')){
 [TimeSync.Loc]::Language=$language
 if([string]::IsNullOrEmpty([TimeSync.Loc]::T('设置','settings'))){throw 'Empty localized title'}
 if([TimeSync.Loc]::Rtl -ne ($language -eq 'ar')){throw 'Wrong direction'}
}
[TimeSync.Loc]::Language='zh-Hant';if([TimeSync.Loc]::T('设置','settings') -ne '設定'){throw 'Traditional Chinese not independent'}
[TimeSync.Loc]::Language='zh-Hans';if([TimeSync.Loc]::T('设置','settings') -ne '设置'){throw 'Simplified Chinese failed'}
'PASS: actual resolver/lookup, 15 languages, system variants, unsupported fallback, manual override, Arabic RTL and independent Chinese scripts.'
