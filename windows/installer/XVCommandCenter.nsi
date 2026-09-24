; XV Digital Access Control — Windows installer (NSIS)
; Build: makensis /DSRC=<publish folder> /DVERSION=1.0.0 XVCommandCenter.nsi
Unicode true
!include "MUI2.nsh"
!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!define APP "XV Command Center"
!define PUBLISHER "XV Digital Access Control"
!define UNINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\XVCommandCenter"

Name "${APP}"
OutFile "XV-CommandCenter-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\XV Access Control"
InstallDirRegKey HKLM "Software\XVCommandCenter" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${PUBLISHER}"

!define MUI_ICON "..\src\XV.CommandCenter\Assets\app.ico"
!define MUI_UNICON "..\src\XV.CommandCenter\Assets\app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\XVCommandCenter.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start XV Command Center now"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP} Setup"
VIAddVersionKey "FileVersion" "${VERSION}"

Section "Install"
  SetShellVarContext all
  nsExec::Exec 'taskkill /IM XVCommandCenter.exe /F'
  SetOutPath "$INSTDIR"
  File /r "${SRC}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\XV Access Control"
  CreateShortcut "$SMPROGRAMS\XV Access Control\XV Command Center.lnk" "$INSTDIR\XVCommandCenter.exe"
  CreateShortcut "$SMPROGRAMS\XV Access Control\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\XV Command Center.lnk" "$INSTDIR\XVCommandCenter.exe"
  ; Firewall: HTTPS API for terminals + LAN discovery
  nsExec::Exec 'netsh advfirewall firewall delete rule name="XV Command Center HTTPS"'
  nsExec::Exec 'netsh advfirewall firewall delete rule name="XV Command Center Discovery"'
  nsExec::Exec 'netsh advfirewall firewall add rule name="XV Command Center HTTPS" dir=in action=allow protocol=TCP localport=8443 program="$INSTDIR\XVCommandCenter.exe" enable=yes'
  nsExec::Exec 'netsh advfirewall firewall add rule name="XV Command Center Discovery" dir=in action=allow protocol=UDP localport=47913 program="$INSTDIR\XVCommandCenter.exe" enable=yes'
  WriteRegStr HKLM "Software\XVCommandCenter" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINST}" "DisplayName" "${APP}"
  WriteRegStr HKLM "${UNINST}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINST}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINST}" "DisplayIcon" "$INSTDIR\XVCommandCenter.exe"
  WriteRegStr HKLM "${UNINST}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINST}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  nsExec::Exec 'taskkill /IM XVCommandCenter.exe /F'
  nsExec::Exec 'netsh advfirewall firewall delete rule name="XV Command Center HTTPS"'
  nsExec::Exec 'netsh advfirewall firewall delete rule name="XV Command Center Discovery"'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "XVCommandCenter"
  Delete "$DESKTOP\XV Command Center.lnk"
  RMDir /r "$SMPROGRAMS\XV Access Control"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINST}"
  DeleteRegKey HKLM "Software\XVCommandCenter"
  ; Encrypted data in %ProgramData%\XVAccessControl is intentionally kept.
SectionEnd
