using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("方块皮肤 · OBS")]
[assembly: AssemblyDescription("PNG 皮肤转换、右手持物、鼠标转头与 OBS 直播窗口")]
[assembly: AssemblyProduct("SkinLive OBS")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: ComVisible(false)]

namespace SkinLive
{
    internal static class App
    {
        [STAThread]
        static int Main(string[] args)
        {
            if(args.Length>0 && args[0]=="--convert") {
                try {
                    if(args.Length<3 || args.Length>5)throw new ArgumentException("SkinLiveOBS.exe --convert INPUT.png NEW_DIRECTORY [auto|wide|slim] [none|minecraft:wooden_…]");
                    int layout=0;
                    if(args.Length>=4) {
                        layout=Array.IndexOf(new[]{"auto","wide","slim"},args[3].ToLowerInvariant());
                        if(layout<0)throw new ArgumentException("皮肤布局可选 auto、wide 或 slim。");
                    }
                    HandItem item=args.Length==5?VanillaItems.Find(args[4]):VanillaItems.Empty;
                    if(item==null)throw new ArgumentException("未知的右手物品 ID。");
                    bool slim;
                    using(Bitmap atlas=SkinMap.ConvertPng(args[1],layout,out slim)) {
                        if(item==VanillaItems.Empty)ModelFiles.Export(atlas,args[2],item);
                        else {
                            int[] pixels=SkinMap.Pixels(atlas);
                            ItemMesh.Paint(pixels,VanillaItems.Load(item));
                            using(Bitmap equipped=SkinMap.ToBitmap(pixels,SkinMap.Size,SkinMap.Size))
                                ModelFiles.Export(equipped,args[2],item);
                        }
                    }
                    return 0;
                }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using(Mutex m=new Mutex(false,@"Local\SkinLiveOBS.Studio.1",out first)) {
                if(!first){MessageBox.Show("控制台已打开。可把皮肤 PNG 拖入控制台。","方块皮肤");return 0;}
                Application.Run(new Studio(args.Length==1?args[0]:null));
            }
            return 0;
        }
    }
}
