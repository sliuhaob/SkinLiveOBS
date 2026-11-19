using System;
namespace SkinLive
{
    internal sealed class Motion
    {
        double jumpStart=-10, swingStart=-10, last; bool gateOpen;
        const double TickRate=20, JumpSeconds=12/TickRate, SwingSeconds=6/TickRate;
        public double Mouth,Height,Swing;
        public static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
        public void ClearQueue(){jumpStart=swingStart=-10;}
        public void Jump(double now){if(now-jumpStart>=JumpSeconds)jumpStart=now;}
        public void Punch(double now){if(now-swingStart>=SwingSeconds/2)swingStart=now;}
        static double JumpHeight(double time)
        {
            double ticks=time*TickRate;
            if(ticks<0||ticks>=12)return 0;
            double y=0,velocity=.42;int full=(int)ticks;
            for(int i=0;i<full;i++){y+=velocity;velocity=(velocity-.08)*.98;}
            return y+(ticks-full)*(Math.Max(0,y+velocity)-y);
        }
        public void Tick(double now,double db,int threshold,bool spaceHeld)
        {
            double dt=Math.Max(.001,Math.Min(.1,now-last));last=now;
            if(spaceHeld && now-jumpStart>=JumpSeconds) jumpStart=now;
            double s=(now-swingStart)/SwingSeconds;Swing=s>=0&&s<1?s:0;Height=JumpHeight(now-jumpStart);
            gateOpen=db>threshold-(gateOpen?3:0);
            double target=gateOpen?Clamp((db-threshold+3)/23.0):0;
            Mouth+=(target-Mouth)*(1-Math.Exp(-(target>=Mouth?30:14)*dt));
        }
    }
}
