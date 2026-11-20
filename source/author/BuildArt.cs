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
        static void CheckPoint(Vec actual, Vec expected)
        {
            if (Math.Abs(actual.X-expected.X) > .001 || Math.Abs(actual.Y-expected.Y) > .001 ||
                Math.Abs(actual.Z-expected.Z) > .001)
                throw new InvalidDataException("右手物品的网格顶点偏离 Java 原版握姿。");
        }

        static void CheckRightHand()
        {
            // Arm-local landmarks from the vanilla 4x4 matrix, in model pixels.
            // These fixed values catch reversed UVs, scale, grip offset and rotation order.
            var faces=Mesh.CreateFaces();
            foreach (var pose in new[] { Pose.Standard(0,0,0,true), Pose.Standard(0,1.0/3,19,true),
                                         Pose.Standard(Pose.JumpPeak,2.0/3,7,true),
                                         Pose.Standard(0,-1.0/3,19,true,0,0,1),
                                         Pose.Standard(Pose.JumpPeak,1.0/3,7,true,1,-1,1) }) {
                Mesh.Evaluate(faces,pose,0,true);
                Limb arm=pose.Joints[2];
                var project=new Mesh.Transform(arm);
                int first=ItemMesh.First;
                CheckPoint(faces[first].V[0],project.Project(arm,-.575,18.97055367,-4.33008587,pose.RenderLift));
                CheckPoint(faces[first+1].V[1],project.Project(arm,-.575,11.16991413,-15.47055367,pose.RenderLift));
                CheckPoint(faces[first].V[2],project.Project(arm,-.575,9.08252147,-3.63236158,pose.RenderLift));
                CheckPoint(faces[first+6].V[3],project.Project(arm,-1.425,7.83008587,3.47055367,pose.RenderLift));
                CheckPoint(faces[first+8].V[0],project.Project(arm,-1.425,18.97055367,-4.33008587,pose.RenderLift));
            }
        }
        public static void WriteGazePreview(string png,string destination)
        {
            bool slim;
            double[] lookX={-1,0,1,0}, lookY={0,0,0,1};
            string[] labels={"看向左侧","正视","看向右侧","低头"};
            const int card=212;
            using(Bitmap atlas=SkinMap.ConvertPng(png,0,out slim))
            using(Raster raster=new Raster())
            using(Bitmap sheet=new Bitmap(card*labels.Length,238))
            using(Graphics g=Graphics.FromImage(sheet))
            using(var pale=new SolidBrush(Color.FromArgb(237,241,231)))
            using(var green=new SolidBrush(Color.FromArgb(224,235,228)))
            using(var ink=new SolidBrush(Color.FromArgb(34,61,55)))
            using(var font=new Font("Microsoft YaHei UI",17,FontStyle.Bold,GraphicsUnit.Pixel)) {
                raster.SetAtlas(atlas);
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                for(int i=0;i<labels.Length;i++) {
                    int left=i*card;
                    g.FillRectangle(i%2==0?green:pale,left,0,card,sheet.Height);
                    g.DrawString(labels[i],font,ink,left+12,9);
                    raster.Draw(Pose.Standard(0,0,0,false,lookX[i],lookY[i]),0);
                    g.DrawImage(raster.Image,new Rectangle(left,36,card,202),new Rectangle(124,263,201,192),GraphicsUnit.Pixel);
                }
                sheet.Save(destination,ImageFormat.Png);
            }
        }
        public static void Write(string png,string dest)
        {
            CheckRightHand();
            Directory.CreateDirectory(dest);
            WriteGazePreview(png,Path.Combine(dest,"head-tracking.png"));
            Mesh.WriteKeyShapes(Path.Combine(dest,"avatar.rig"),false);
            Mesh.WriteKeyShapes(Path.Combine(dest,"right-hand.rig"),true);
            Mesh.WriteKeyShapes(Path.Combine(dest,"custom-hand.rig"),true,ItemMesh.Extended);
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
                    g.DrawImage(raster.Image,new Rectangle(53,42,320,444),new Rectangle(92,264,265,368),GraphicsUnit.Pixel);
                    raster.Draw(Pose.Standard(0,.34,0),.85);
                    g.DrawImage(raster.Image,new Rectangle(416,52,481,444),new Rectangle(87,265,278,257),GraphicsUnit.Pixel);
                    g.DrawString("全身 · 空手待机",font,Brushes.White,17,12);
                    g.DrawString("近景 · 开口与挥拳",font,Brushes.White,430,12);
                    sheet.Save(Path.Combine(dest,"preview.png"),ImageFormat.Png);
                }
            }
        }
    }
}
