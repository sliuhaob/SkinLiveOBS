using System;
namespace SkinLive
{
    internal sealed class Motion
    {
        double jumpStart=-10, swingStart=-10, last; bool gateOpen;
        const double TickRate=20, JumpSeconds=12/TickRate, SwingSeconds=6/TickRate;
        int swingDirection=1;
        public double Mouth,Height,Swing,Crouch;
        public static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
        public void ClearQueue(){jumpStart=swingStart=-10;}
        public void Start(AvatarAction action,double now)
        {
            if(action==AvatarAction.Jump && now-jumpStart>=JumpSeconds)jumpStart=now;
            if((action==AvatarAction.RightSwing || action==AvatarAction.LeftSwing) && now-swingStart>=SwingSeconds/2) {
                swingStart=now;
                swingDirection=action==AvatarAction.RightSwing?1:-1;
            }
        }
        static double JumpHeight(double time)
        {
            double ticks=time*TickRate;
            if(ticks<0||ticks>=12)return 0;
            double y=0,velocity=.42;int full=(int)ticks;
            for(int i=0;i<full;i++){y+=velocity;velocity=(velocity-.08)*.98;}
            return y+(ticks-full)*(Math.Max(0,y+velocity)-y);
        }
        public void Tick(double now,double db,int threshold,bool jumpHeld,bool crouchHeld,bool repeatRightHeld=false)
        {
            double dt=Math.Max(.001,Math.Min(.1,now-last));last=now;
            if(jumpHeld && now-jumpStart>=JumpSeconds) jumpStart=now;
            double swingPhase = (now - swingStart) / SwingSeconds;
            if (repeatRightHeld && swingPhase >= 1) {
                // Keep the six-tick cadence even if a render frame arrives late.
                swingPhase = swingStart < 0 ? 0 : swingPhase - Math.Floor(swingPhase);
                swingStart = now - swingPhase * SwingSeconds;
                swingDirection = 1;
            }
            Swing = swingPhase >= 0 && swingPhase < 1 ? swingPhase * swingDirection : 0;
            Height=JumpHeight(now-jumpStart);Crouch=crouchHeld?1:0;
            gateOpen=db>threshold-(gateOpen?3:0);
            double target=gateOpen?Clamp((db-threshold+3)/23.0):0;
            Mouth+=(target-Mouth)*(1-Math.Exp(-(target>=Mouth?30:14)*dt));
        }
    }
}
