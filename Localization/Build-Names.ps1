# SPDX-License-Identifier: MPL-2.0
# Resource-only ARM PE images: RT_STRING IDs 100 and 200.
# No executable code or entry point. The phone shell loads these as data files.
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public static class NameResources {
 static byte[] Table(int index,string text){using(var m=new MemoryStream()){using(var b=new BinaryWriter(m,Encoding.Unicode)){for(int i=0;i<16;i++){string s=i==index?text:"";b.Write((ushort)s.Length);b.Write(Encoding.Unicode.GetBytes(s));}return m.ToArray();}}}
 static void U32(BinaryWriter b,int offset,uint v){b.BaseStream.Position=offset;b.Write(v);}
 static void U16(BinaryWriter b,int offset,ushort v){b.BaseStream.Position=offset;b.Write(v);}
 public static void Write(string file,ushort language,string text){
  byte[] a=Table(4,text),c=Table(8,text);int second=(136+a.Length+3)&~3;int length=second+c.Length;int raw=(length+511)&~511;
  byte[] data=new byte[512+raw];using(var m=new MemoryStream(data)){using(var b=new BinaryWriter(m)){
   U16(b,0,0x5a4d);U32(b,60,128);U32(b,128,0x4550);U16(b,132,0x1c4);U16(b,134,1);U16(b,148,224);U16(b,150,0x2103);
   U16(b,152,0x10b);U32(b,160,(uint)raw);U32(b,176,0x1000);U32(b,180,0x10000000);U32(b,184,0x1000);U32(b,188,512);
   U16(b,192,6);U16(b,194,2);U16(b,200,6);U16(b,202,2);U32(b,208,0x2000);U32(b,212,512);U16(b,220,3);U16(b,222,0x100);
   U32(b,224,0x100000);U32(b,228,0x1000);U32(b,232,0x100000);U32(b,236,0x1000);U32(b,244,16);U32(b,264,0x1000);U32(b,268,(uint)length);
   m.Position=376;b.Write(Encoding.ASCII.GetBytes(".rsrc\0\0\0"));b.Write((uint)length);b.Write((uint)0x1000);b.Write((uint)raw);b.Write((uint)512);m.Position=412;b.Write((uint)0x40000040);
   const int r=512;U16(b,r+14,1);U32(b,r+16,6);U32(b,r+20,0x80000018);
   U16(b,r+38,2);U32(b,r+40,7);U32(b,r+44,0x80000038);U32(b,r+48,13);U32(b,r+52,0x80000050);
   U16(b,r+70,1);U32(b,r+72,language);U32(b,r+76,104);U16(b,r+94,1);U32(b,r+96,language);U32(b,r+100,120);
   U32(b,r+104,0x1000+136);U32(b,r+108,(uint)a.Length);U32(b,r+112,1200);
   U32(b,r+120,(uint)(0x1000+second));U32(b,r+124,(uint)c.Length);U32(b,r+128,1200);
   m.Position=r+136;b.Write(a);m.Position=r+second;b.Write(c);
  }}File.WriteAllBytes(file,data);
 }
}
'@
$root=Split-Path $PSScriptRoot -Parent
[NameResources]::Write((Join-Path $root 'AppResLib.dll'),0x0409,'Time Sync')
foreach($locale in @('0409','0804','0404','0c04','1004','1404')) {
 $name=if($locale -eq '0409'){'Time Sync'}else{[string][char]0x6821+[char]0x65f6}
 if($locale -in @('0404','0c04','1404')){$name=[string][char]0x6821+[char]0x6642}
 [NameResources]::Write((Join-Path $root "AppResLib.dll.$locale.mui"),[Convert]::ToUInt16($locale,16),$name)
}
