# SPDX-License-Identifier: MPL-2.0
param([Parameter(Mandatory=$true)][string]$Clang,[Parameter(Mandatory=$true)][string]$Linker,[Parameter(Mandatory=$true)][string]$DllTool,[string]$Midlrt='C:\Program Files (x86)\Windows Kits\8.1\bin\x86\midlrt.exe',[string]$Preprocessor)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 if($Preprocessor){& $Midlrt /winrt /nomidl /cpp_cmd $Preprocessor /I 'C:\Program Files (x86)\Windows Phone Kits\8.0\Include' /metadata_dir 'C:\Program Files (x86)\Windows Phone Kits\8.0\Windows MetaData' /winmd ClockNative.winmd ClockNative.idl;if($LASTEXITCODE -ne 0){throw 'MIDLRT failed'}}
 foreach($name in @('kernel','strings','com')){& $DllTool dlltool -m arm -d "$name.def" -l "$name.lib";if($LASTEXITCODE -ne 0){throw 'Import library failed'}}
 & $Clang -target thumbv7-pc-windows-msvc -O2 -ffreestanding -fno-stack-protector -c ClockNative.c -o ClockNative.obj
 if($LASTEXITCODE -ne 0){throw 'Native compile failed'}
 & $Linker /dll /noentry /nodefaultlib /machine:arm '/subsystem:windows,6.02' /out:ClockNative.dll ClockNative.obj kernel.lib strings.lib com.lib
 if($LASTEXITCODE -ne 0){throw 'Native link failed'}
}finally{Pop-Location}
