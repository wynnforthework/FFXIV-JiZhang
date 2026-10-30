using JiZhang.Core;
using Xunit;
namespace JiZhang.Tests;
public sealed class UiMotionTests
{
    [Fact] public void HoverConvergesEquallyAcrossFrameRates() {
        float Run(int fps) {var m=new UiMotion();m.Frame(0,0,false);m.Approach("hover",0);for(var i=1;i<=fps;i++){m.Frame((double)i/fps,1f/fps,false);m.Approach("hover",1,18);}return m.Approach("hover",1,18);}
        Assert.InRange(Run(30),.999f,1);Assert.InRange(Math.Abs(Run(30)-Run(144)),0,.0005f);
    }
    [Fact] public void DirectionCanReverseWithoutJumpingAndReducedMotionSettles() {
        var f=new FoldMotion();f.Toggle(0);var middle=f.Update(.12,false);Assert.InRange(middle,.1f,.9f);
        f.Toggle(.12);Assert.Equal(middle,f.Update(.12,false));Assert.InRange(f.Update(.15,false),0,middle);
        Assert.Equal(0,f.Update(.16,true));
        var m=new UiMotion();m.Frame(0,0,false);Assert.Equal(1,m.Reveal("page",0));m.Frame(.1,.016f,false);Assert.Equal(0,m.Reveal("page",1));
        m.Frame(.2,.016f,true);Assert.Equal(1,m.Reveal("page",1));Assert.Equal(2,m.Approach("nav",2));Assert.Equal(0,m.Wave(16));
    }
    [Fact] public void PauseDoesNotProduceAnAnimationLeapAndKeysAreIndependent() {
        var m=new UiMotion();m.Frame(0,0,false);m.Approach("a",0);m.Approach("b",8);
        m.Frame(100,100,false);Assert.InRange(m.Approach("a",1),.5f,.6f);Assert.Equal(8,m.Approach("b",8));
        m.Clear();Assert.Equal(3,m.Approach("a",3));
    }
}
