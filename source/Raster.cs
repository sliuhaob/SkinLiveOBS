using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SkinLive
{
    internal sealed class Raster : IDisposable
    {
        public const int Width=448,Height=640;
        public readonly Bitmap Image=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);
        readonly int[] pixels=new int[Width*Height];
        readonly float[] depth=new float[Width*Height];
        readonly Face[] faces=Mesh.CreateFaces(), order=new Face[ItemMesh.First+ItemMesh.Count];
        readonly Rectangle[] tiles=new Rectangle[ItemMesh.First+ItemMesh.Count];
        static readonly System.Collections.Generic.IComparer<Face> byDepth=
            System.Collections.Generic.Comparer<Face>.Create(Mesh.Compare);
        bool hasItem;
        int[] texture;
        public double LiftPixels {get;private set;}
        public Raster(){for(int i=0;i<tiles.Length;i++)tiles[i]=SkinMap.Cell(i);}
        public void SetAtlas(Bitmap atlas)
        {
            if(atlas.Width!=SkinMap.Size || atlas.Height!=SkinMap.Size)throw new ArgumentException("模型贴图必须是 1024×1024。");
            texture=SkinMap.Pixels(atlas);
            hasItem=false;
        }
        public void SetItem(int[] sprite)
        {
            ItemMesh.Paint(texture,sprite);
            hasItem=sprite!=null;
        }
        public Bitmap CopyAtlas(){return SkinMap.ToBitmap(texture,SkinMap.Size,SkinMap.Size);}
        public void Draw(Pose p,double mouth)
        {
            LiftPixels=Mesh.Zoom*16*p.Lift;
            if(texture==null)return;
            Array.Clear(pixels,0,pixels.Length);
            for(int i=0;i<depth.Length;i++)depth[i]=float.MinValue;
            Mesh.Evaluate(faces,p,mouth,hasItem);
            int count=hasItem?faces.Length:ItemMesh.First;
            Array.Copy(faces,order,count); Array.Sort(order,0,count,byDepth);
            for(int i=0;i<count;i++) if(order[i].Opacity>.001 && Math.Abs(order[i].Area)>.001) DrawFace(order[i]);
            BitmapData data=Image.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels,0,data.Scan0,pixels.Length); } finally { Image.UnlockBits(data); }
        }
        void DrawFace(Face f)
        {
            Vec a=f.V[0], b=f.V[1], c=f.V[3], d=f.V[2];
            float hx=b.X-a.X,hy=b.Y-a.Y,vx=c.X-a.X,vy=c.Y-a.Y;
            float inv=1/(hx*vy-hy*vx), ux=vy*inv, uy=-vx*inv, tx=-hy*inv, ty=hx*inv;
            int x0=Math.Max(0,(int)Math.Floor(Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X))));
            int x1=Math.Min(Width,(int)Math.Ceiling(Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X))));
            int y0=Math.Max(0,(int)Math.Floor(Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))));
            int y1=Math.Min(Height,(int)Math.Ceiling(Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))));
            Rectangle box=tiles[f.Cell];float dzU=b.Z-a.Z,dzV=c.Z-a.Z;
            for(int y=y0;y<y1;y++) {
                float u=(x0+.5f-a.X)*ux+(y+.5f-a.Y)*uy;
                float v=(x0+.5f-a.X)*tx+(y+.5f-a.Y)*ty;
                int i=y*Width+x0;
                for(int x=x0;x<x1;x++,i++,u+=ux,v+=tx) {
                    if(u<0 || u>=1 || v<0 || v>=1)continue;
                    float z=a.Z+dzU*u+dzV*v;if(z<depth[i])continue;
                    int col=texture[(box.Y+(int)(v*box.Height))*SkinMap.Size+box.X+(int)(u*box.Width)];
                    int alpha=(int)(((uint)col>>24)*f.Opacity+.5);if(alpha<1)continue;
                    if(alpha==255)pixels[i]=col;
                    else {
                        int dst=pixels[i],da=(int)((uint)dst>>24);
                        int back=(255-alpha)*da/255, sum=alpha+back;
                        int r=(((col>>16)&255)*alpha+((dst>>16)&255)*back)/sum;
                        int g=(((col>>8)&255)*alpha+((dst>>8)&255)*back)/sum;
                        int bl=((col&255)*alpha+(dst&255)*back)/sum;
                        pixels[i]=sum<<24|r<<16|g<<8|bl;
                    }
                    depth[i]=z;
                }
            }
        }
        public void Dispose(){Image.Dispose();}
    }
}
