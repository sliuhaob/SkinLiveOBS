@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "SKIN_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%SKIN_CSC%" set "SKIN_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%SKIN_CSC%" /nologo /noconfig /optimize+ /debug- /warn:4 /warnaserror+ /codepage:65001 /platform:anycpu /target:winexe /out:"..\SkinLiveOBS.exe" ^
  /win32icon:icon.ico /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  /resource:template\Avatar.moc3,SkinLive.Avatar.moc3 /resource:template\Avatar.model3.json,SkinLive.Avatar.model3.json /resource:template\Avatar.cdi3.json,SkinLive.Avatar.cdi3.json ^
  /resource:template\RightHand.moc3,SkinLive.RightHand.moc3 ^
  /resource:template\Motions\Jump.motion3.json,SkinLive.Motions.Jump.motion3.json /resource:template\Motions\Swing.motion3.json,SkinLive.Motions.Swing.motion3.json ^
  /resource:"..\ASSET-LICENSE.txt",SkinLive.ASSET-LICENSE.txt ^
  App.cs Input.cs Motion.cs HeadTracking.cs SkinMap.cs ItemMesh.cs VanillaItems.cs Mesh.cs Raster.cs ModelFiles.cs Stage.cs SkinControls.cs ItemControls.cs Studio.cs
set "SKIN_RESULT=%ERRORLEVEL%"
popd
exit /b %SKIN_RESULT%
