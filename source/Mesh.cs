using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SkinLive
{
    internal struct Vec
    {
        public float X,Y,Z;
        public Vec(double x,double y,double z){X=(float)x;Y=(float)y;Z=(float)z;}
    }
    internal sealed class Limb
    {
        public float X,Y,Z,Rx,Ry,Rz;
        public Limb(float x,float y){X=x;Y=y;}
    }
    internal sealed class Pose
    {
        public readonly Limb[] Joints={new Limb(0,0),new Limb(0,0),new Limb(-5,2),
            new Limb(5,2),new Limb(-1.9f,12),new Limb(1.9f,12)};
        public double Lift;
        public const double JumpPeak=1.25220335;
        public static Pose Standard(double height,double swing,double age,bool holding=false)
        {
            Pose p=new Pose(); p.Lift=height;
            Limb arm=p.Joints[2], other=p.Joints[3];
            double twist=Math.Sin(Math.Sqrt(swing)*Math.PI*2)*.2;
            p.Joints[1].Ry=(float)twist;
            arm.X=(float)(-Math.Cos(twist)*5); arm.Z=(float)(Math.Sin(twist)*5);
            other.X=-arm.X; other.Z=-arm.Z;
            double strike=Math.Sin((1-Math.Pow(1-swing,4))*Math.PI);
            double reach=Math.Sin(swing*Math.PI);
            // Standing third-person ITEM pose pitches the holding arm forward by 18 degrees.
            arm.Rx=(float)((holding?-Math.PI/10:0)-strike*1.2-.525*reach);
            arm.Ry=(float)(3*twist); other.Ry=(float)twist; other.Rx=(float)twist;
            double idleRoll=.05+.05*Math.Cos(age*.09), idlePitch=.05*Math.Sin(age*.067);
            arm.Rz=(float)(-.4*reach+idleRoll); other.Rz=(float)-idleRoll;
            arm.Rx+=(float)idlePitch; other.Rx-=(float)idlePitch;
            return p;
        }
    }
    internal sealed class Face
    {
        public readonly int Cell;
        public readonly Vec[] V=new Vec[4];
        public double Opacity,Depth;
        public Face(int cell){Cell=cell;}
        public double Area {get{return (V[1].X-V[0].X)*(V[3].Y-V[0].Y)-(V[1].Y-V[0].Y)*(V[3].X-V[0].X);}}
    }
    internal static class Mesh
    {
        // Each row walks top-left, top-right, bottom-right, bottom-left of a UV face.
        // Vertex bits encode x=max, y=max, z=back for the vanilla cuboid.
        static readonly int[][] Faces={new[]{0,1,3,2},new[]{4,0,2,6},new[]{4,5,1,0},
            new[]{5,4,6,7},new[]{1,5,7,3},new[]{2,3,7,6}};
        public static readonly string[] FaceNames={"Front","Right","Top","Back","Left","Bottom"};
        const double Yaw=.23562, Tilt=.105, Zoom=11.55;
        static readonly double cy=Math.Cos(Yaw), sy=Math.Sin(Yaw), ct=Math.Cos(Tilt), st=Math.Sin(Tilt);
        internal struct Transform
        {
            public double cx,sx,cy,sy,cz,sz;
            public Transform(Limb p){cx=Math.Cos(p.Rx);sx=Math.Sin(p.Rx);cy=Math.Cos(p.Ry);sy=Math.Sin(p.Ry);cz=Math.Cos(p.Rz);sz=Math.Sin(p.Rz);}
            public Vec Project(Limb p, double x,double y,double z,double lift)
            {
                double a=y*cx-z*sx,b=y*sx+z*cx;
                double c=x*cy+b*sy,d=-x*sy+b*cy;
                double xx=p.X+c*cz-a*sz, yy=p.Y+c*sz+a*cz-lift*16, zz=p.Z+d;
                double depth=-Mesh.sy*xx-Mesh.cy*zz;
                return new Vec(224+Zoom*(Mesh.cy*xx-Mesh.sy*zz),345+Zoom*(ct*yy+st*depth),ct*depth-st*yy);
            }
        }
        public static Face[] CreateFaces()
        { Face[] faces=new Face[ItemMesh.First+ItemMesh.Count];for(int i=0;i<faces.Length;i++) faces[i]=new Face(i);return faces; }
        static void Finish(Face f,double opacity)
        {
            f.Depth=(f.V[0].Z+f.V[1].Z+f.V[2].Z+f.V[3].Z)*.25;
            f.Opacity=f.Area>.001 ? opacity:0;
        }
        public static void Evaluate(Face[] faces,Pose pose,double open,bool withItem)
        {
            Vec[] corners=new Vec[8];
            for(int part=0;part<6;part++) {
                Limb limb=pose.Joints[part]; Transform t=new Transform(limb);
                SkinPart def=SkinMap.Parts[part];
                double x=part==2?-3:part==3?-1:-def.W/2.0;
                double y=part==0?-8:(part==2||part==3)?-2:0;
                double z=-def.D/2.0;
                for(int layer=0;layer<2;layer++) {
                    double pad=layer*(part==0?.5:.25);
                    for(int j=0;j<8;j++) corners[j]=t.Project(limb,
                        (j&1)==0?x-pad:x+def.W+pad,(j&2)==0?y-pad:y+def.H+pad,
                        (j&4)==0?z-pad:z+def.D+pad,pose.Lift);
                    for(int n=0;n<6;n++) {
                        Face face=faces[part*12+layer*6+n];
                        for(int j=0;j<4;j++) face.V[j]=corners[Faces[n][j]];
                        Finish(face,1);
                    }
                }
                if(part==0) {
                    Face m=faces[SkinMap.Mouth]; double half=1.04+.13*open;
                    double upper=-2.18-.10*open, lower=upper+.035+1.04*open;
                    const double mz=-4.53;
                    m.V[0]=t.Project(limb,-half,upper,mz,pose.Lift);m.V[1]=t.Project(limb,half,upper,mz,pose.Lift);
                    m.V[2]=t.Project(limb,half,lower,mz,pose.Lift);m.V[3]=t.Project(limb,-half,lower,mz,pose.Lift);
                    Finish(m,open);
                }
            }
            if(withItem)ItemMesh.Evaluate(faces,pose);
        }
        // Offline authoring only. The encoder reads these exact native-player vertices.
        public static void WriteKeyShapes(string path,bool holding)
        {
            Face[] faces=CreateFaces();
            if(!holding)Array.Resize(ref faces,ItemMesh.First);
            var rows=new StringBuilder[faces.Length];
            for(int id=0;id<rows.Length;id++) {
                var r=SkinMap.Cell(id);
                string name=id>=ItemMesh.First?ItemMesh.Name(id-ItemMesh.First):
                    id==SkinMap.Mouth?"MouthInterior":SkinMap.Parts[id/12].Name+((id%12)<6?"Base":"Wear")+FaceNames[id%6];
                rows[id]=new StringBuilder(string.Join("\t",new[]{name,r.X.ToString(),r.Y.ToString(),r.Width.ToString(),r.Height.ToString()}));
            }
            SortedSet<double> keys=SwingKeys();
            foreach(double attack in keys) for(int jump=0;jump<=1;jump++) for(int mouth=0;mouth<=1;mouth++) {
                Evaluate(faces,Pose.Standard(jump*Pose.JumpPeak,attack,0,holding),mouth,holding);
                var sorted=(Face[])faces.Clone();Array.Sort(sorted,Compare);
                for(int order=0;order<sorted.Length;order++) {
                    Face face=sorted[order];StringBuilder row=rows[face.Cell];row.Append('\t');
                    foreach(Vec v in face.V) row.AppendFormat(CultureInfo.InvariantCulture,"{0:F4},{1:F4},",v.X,v.Y);
                    row.AppendFormat(CultureInfo.InvariantCulture,"{0:F3},{1}",face.Opacity,order+1);
                }
            }
            using(StreamWriter w=new StreamWriter(path,false,new UTF8Encoding(false))) {
                w.WriteLine("#swing\t"+string.Join(",",new List<double>(keys).ConvertAll(k=>k.ToString("R",CultureInfo.InvariantCulture)).ToArray()));
                foreach(var row in rows)w.WriteLine(row);
            }
        }
        public static int Compare(Face a,Face b){int c=a.Depth.CompareTo(b.Depth);return c!=0?c:a.Cell.CompareTo(b.Cell);}
        static SortedSet<double> SwingKeys()
        {
            // Densely sample the rapid turn at the start. Include all six game-tick
            // boundaries as exact keys for players using the exported Live2D asset.
            var keys=new SortedSet<double>();
            for(int i=0;i<=48;i++)keys.Add((i/48.0)*(i/48.0));
            for(int i=0;i<=6;i++)keys.Add(i/6.0);
            return keys;
        }
    }
}
