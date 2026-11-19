using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SkinLive
{
    // Offline asset authoring. References only drawing/model code, with no UI, input or game assembly.
    public static class BuildArt
    {
        public static void Write(string png,string dest)
        {
            Directory.CreateDirectory(dest);
            Mesh.WriteKeyShapes(Path.Combine(dest,"avatar.rig"),false);
            Mesh.WriteKeyShapes(Path.Combine(dest,"right-hand.rig"),true);
            bool slim;
            using(Bitmap atlas=SkinMap.ConvertPng(png,0,out slim)) using(Raster raster=new Raster()) {
                atlas.Save(Path.Combine(dest,"texture_00.png"),ImageFormat.Png);raster.SetAtlas(atlas);
                using(Bitmap sheet=new Bitmap(900,496)) using(Graphics g=Graphics.FromImage(sheet))
                using(var bg=new LinearGradientBrush(new Rectangle(0,0,900,496),Color.FromArgb(33,40,62),Color.FromArgb(38,65,74),50))
                using(var line=new Pen(Color.FromArgb(45,118,150,160)))
                using(var font=new Font("Microsoft YaHei UI",16,FontStyle.Bold,GraphicsUnit.Pixel)) {
                    g.FillRectangle(bg,0,0,900,496);
                    for(int x=0;x<900;x+=25)g.DrawLine(line,x,0,x,496);
                    for(int y=0;y<496;y+=25)g.DrawLine(line,0,y,900,y);
                    g.DrawLine(Pens.SlateGray,412,18,412,478);
                    g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                    raster.Draw(Pose.Standard(0,0,0),0);
                    g.DrawImage(raster.Image,new Rectangle(53,42,320,444),new Rectangle(77,222,294,408),GraphicsUnit.Pixel);
                    raster.Draw(Pose.Standard(0,.34,0),.85);
                    g.DrawImage(raster.Image,new Rectangle(416,52,481,444),new Rectangle(72,223,309,285),GraphicsUnit.Pixel);
                    g.DrawString("全身 · 空手待机",font,Brushes.White,17,12);
                    g.DrawString("近景 · 开口与挥拳",font,Brushes.White,430,12);
                    sheet.Save(Path.Combine(dest,"preview.png"),ImageFormat.Png);
                }
            }
        }
    }
}
