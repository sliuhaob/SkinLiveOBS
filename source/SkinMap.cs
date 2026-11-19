using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SkinLive
{
    internal sealed class SkinPart
    {
        public readonly string Name;
        public readonly int W, H, D, U, V, U2, V2;
        public SkinPart(string name, int w, int h, int d, int u, int v, int u2, int v2)
        { Name=name; W=w; H=h; D=d; U=u; V=v; U2=u2; V2=v2; }
    }

    internal static class SkinMap
    {
        public static readonly SkinPart[] Parts = {
            new SkinPart("Head",8,8,8,0,0,32,0), new SkinPart("Body",8,12,4,16,16,16,32),
            new SkinPart("RightArm",4,12,4,40,16,40,32), new SkinPart("LeftArm",4,12,4,32,48,48,48),
            new SkinPart("RightLeg",4,12,4,0,16,0,32), new SkinPart("LeftLeg",4,12,4,16,48,0,48)
        };
        internal const int Size=1024, Mouth=72;
        // Front, player's right, top, back, player's left, bottom in the Minecraft net.
        public static Rectangle Source(int u, int v, int w, int h, int d, int face)
        {
            switch (face) {
                case 0: return new Rectangle(u+d,v+d,w,h);
                case 1: return new Rectangle(u,v+d,d,h);
                case 2: return new Rectangle(u+d,v,w,d);
                case 3: return new Rectangle(u+2*d+w,v+d,w,h);
                case 4: return new Rectangle(u+d+w,v+d,d,h);
                default: return new Rectangle(u+d+w,v,w,d);
            }
        }
        public static Rectangle Cell(int id)
        {
            int x=(id%9)*112+7, y=(id/9)*112+7;
            if (id==Mouth) return new Rectangle(x,y,24,12);
            SkinPart p=Parts[id/12];
            Rectangle r=Source(0,0,p.W,p.H,p.D,id%6);
            return new Rectangle(x,y,r.Width*8,r.Height*8);
        }
        public static int[] Pixels(Bitmap im)
        {
            if(im.PixelFormat!=PixelFormat.Format32bppArgb) {
                using(Bitmap normalized=new Bitmap(im.Width,im.Height,PixelFormat.Format32bppArgb)) {
                    using(Graphics g=Graphics.FromImage(normalized)) {
                        g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(im,0,0);
                    }
                    return Pixels(normalized);
                }
            }
            BitmapData data=im.LockBits(new Rectangle(0,0,im.Width,im.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                int[] result=new int[im.Width*im.Height];
                for(int y=0;y<im.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),result,y*im.Width,im.Width);
                return result;
            } finally { im.UnlockBits(data); }
        }
        public static Bitmap ToBitmap(int[] pixels,int w,int h)
        {
            Bitmap im=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            BitmapData data=im.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<h;y++) Marshal.Copy(pixels,y*w,IntPtr.Add(data.Scan0,y*data.Stride),w); }
            finally { im.UnlockBits(data); }
            return im;
        }
        static int Read32(byte[] h,int offset)
        { return (h[offset]<<24)|(h[offset+1]<<16)|(h[offset+2]<<8)|h[offset+3]; }
        static bool Slim(int[] source,int width,int factor)
        {
            foreach(Rectangle zone in new[]{new Rectangle(54,20,2,12),new Rectangle(46,52,2,12)}) {
                for(int y=zone.Top*factor;y<zone.Bottom*factor;y++)
                    for(int x=zone.Left*factor;x<zone.Right*factor;x++)
                        if ((uint)source[y*width+x]>>24 > 32) return false;
            }
            return true;
        }
        static int Shade(int color,int percent)
        {
            return (color & unchecked((int)0xff000000)) |
                Math.Min(255,((color>>16)&255)*percent/100)<<16 |
                Math.Min(255,((color>>8)&255)*percent/100)<<8 | Math.Min(255,(color&255)*percent/100);
        }
        static bool LegacyHatHasAlpha(int[] pixels,int width,int height)
        {
            for(int y=0;y<height;y++) for(int x=width/2;x<width;x++)
                if(((uint)pixels[y*width+x]>>24)<128)return true;
            return false;
        }
        public static Bitmap ConvertPng(string path,int layout,out bool slim)
        {
            using (FileStream stream=File.OpenRead(path)) {
                byte[] header=new byte[24];
                if(stream.Read(header,0,24)!=24 || header[0]!=137 || header[1]!=80 || header[2]!=78 || header[3]!=71 ||
                   header[4]!=13 || header[5]!=10 || header[6]!=26 || header[7]!=10 || Read32(header,12)!=0x49484452)
                    throw new InvalidDataException("请选择 Minecraft 的 PNG 展开皮肤图。");
                int w=Read32(header,16), h=Read32(header,20);
                if(w<64 || w>1024 || w%64!=0 || (h!=w && h*2!=w))
                    throw new InvalidDataException("支持 64×64、128×128 等 64 的整数倍方形展开图，以及旧版 2:1 皮肤，最大宽度 1024。");
                stream.Position=0;
                using (Bitmap input=new Bitmap(stream)) {
                    int k=w/64;
                    int[] src=Pixels(input), target=new int[Size*Size];
                    slim=h==w && (layout==2 || (layout==0 && Slim(src,w,k)));
                    bool drawHat=h==w || LegacyHatHasAlpha(src,w,h);
                    int[] shade={100,84,106,86,91,72};
                    for(int id=0;id<Mouth;id++) {
                        int part=id/12, layer=(id%12)/6, face=id%6;
                        SkinPart p=Parts[part];
                        if(h!=w && layer==1 && (part!=0 || !drawHat)) continue;
                        bool mirror=h!=w && (part==3 || part==5);
                        if(mirror) p=Parts[part-1];
                        int actualFace=mirror && (face==1||face==4) ? 5-face : face;
                        int sw=slim && (part==2||part==3) ? 3:p.W;
                        Rectangle s=Source(layer==0?p.U:p.U2,layer==0?p.V:p.V2,sw,p.H,p.D,actualFace);
                        Rectangle d=Cell(id);
                        for(int dy=-2;dy<d.Height+2;dy++) {
                            int sy=s.Y*k+Math.Min(d.Height-1,Math.Max(0,dy))*s.Height*k/d.Height;
                            for(int dx=-2;dx<d.Width+2;dx++) {
                                int tx=Math.Min(d.Width-1,Math.Max(0,dx));
                                if(mirror) tx=d.Width-1-tx;
                                int sx=s.X*k+tx*s.Width*k/d.Width;
                                target[(d.Y+dy)*Size+d.X+dx]=Shade(src[sy*w+sx],shade[face]);
                            }
                        }
                    }
                    Rectangle m=Cell(Mouth);
                    for(int y=0;y<m.Height;y++) for(int x=0;x<m.Width;x++) {
                        bool corner=(x<2||x>=m.Width-2)&&(y<2||y>=m.Height-2);
                        if(!corner) target[(m.Y+y)*Size+m.X+x]=unchecked((int)0xff000000) |
                            (y<2?0x533238:y>=8 && x>=4 && x<21?0xb66e71:0x43282f);
                    }
                    return ToBitmap(target,Size,Size);
                }
            }
        }
    }
}
