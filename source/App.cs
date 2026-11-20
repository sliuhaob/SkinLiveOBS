using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("方块皮肤 · OBS")]
[assembly: AssemblyDescription("PNG 皮肤和右手物品导入、握点设置、自定义动作与 OBS 直播窗口")]
[assembly: AssemblyProduct("SkinLive OBS")]
[assembly: AssemblyVersion("1.4.1.0")]
[assembly: AssemblyFileVersion("1.4.1.0")]
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
                    if(args.Length<3 || args.Length>8 || args.Length==6)throw new ArgumentException("SkinLiveOBS.exe --convert 皮肤.png 新目录 [auto|wide|slim] [物品ID 或 物品.png [握点X 握点Y [角度]]]");
                    int layout=0;
                    if(args.Length>=4) {
                        layout=Array.IndexOf(new[]{"auto","wide","slim"},args[3].ToLowerInvariant());
                        if(layout<0)throw new ArgumentException("皮肤布局可选 auto、wide 或 slim。");
                    }
                    HandItem item=VanillaItems.Empty;
                    if(args.Length>=5 && args[4].EndsWith(".png",StringComparison.OrdinalIgnoreCase)) {
                        CustomItem design=CustomItem.FromPng(args[4]);
                        if(args.Length>=7) {
                            int x,y,angle=0;
                            if(!int.TryParse(args[5],out x) || !int.TryParse(args[6],out y) ||
                                (args.Length==8 && !int.TryParse(args[7],out angle)))
                                throw new ArgumentException("握点 X、Y 和角度须为整数。");
                            design=new CustomItem(design.Name,design.Pixels,x,y,angle);
                        }
                        item=new HandItem(design);
                    } else if(args.Length>=5) {
                        if(args.Length!=5)throw new ArgumentException("自定义握点用于导入的物品 PNG。");
                        item=VanillaItems.Find(args[4]);
                    }
                    if(item==null)throw new ArgumentException("未知的右手物品 ID。");
                    bool slim;
                    using(Bitmap atlas=SkinMap.ConvertPng(args[1],layout,out slim)) {
                        if(item==VanillaItems.Empty)ModelFiles.Export(atlas,args[2],item);
                        else {
                            int[] pixels=SkinMap.Pixels(atlas);
                            int[] sprite=item.Design==null?VanillaItems.Load(item):item.Design.Sprite();
                            ItemMesh.ForSprite(sprite).Paint(pixels,sprite);
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
