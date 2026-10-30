namespace JiZhang.Core;

/// <summary>Per-window motion state. Time and accessibility policy are supplied by the host.</summary>
public sealed class UiMotion
{
    private readonly Dictionary<string, float> values = [];
    private readonly Dictionary<string, (int State, double Start)> transitions = [];
    public double Time { get; private set; }
    public float Delta { get; private set; }
    public bool Reduced { get; private set; }
    public void Frame(double time, float delta, bool reduced) { Time=time;Delta=Math.Clamp(delta,0,.05f);Reduced=reduced; }
    public float Approach(string key,float target,float speed=16) {
        if(Reduced||!values.TryGetValue(key,out var current))return values[key]=target;
        var next=current+(target-current)*(1-MathF.Exp(-speed*Delta));
        return values[key]=MathF.Abs(next-target)<.0005f?target:next;
    }
    public float Reveal(string key,int state,float milliseconds=260) {
        if(!transitions.TryGetValue(key,out var prior)) {transitions[key]=(state,double.NegativeInfinity);return 1;}
        if(prior.State!=state)transitions[key]=prior=(state,Time);
        if(Reduced)return 1;
        var t=(float)Math.Clamp((Time-prior.Start)*1000/milliseconds,0,1);
        return 1-MathF.Pow(1-t,3);
    }
    public float Wave(double period) => Reduced?0:(float)Math.Sin(Time*2*Math.PI/period);
    public void Clear(){values.Clear();transitions.Clear();}
}

/// <summary>Reversible height transition; changing direction starts at the current position.</summary>
public sealed class FoldMotion
{
    private float from;
    private double started;
    private bool target;
    public float Value { get; private set; }
    public bool Target=>target;
    public bool Moving=>Math.Abs(Value-(target?1:0))>.0001f;
    public void Toggle(double now){from=Value;started=now;target=!target;}
    public float Update(double now,bool reduced) {
        var end=target?1f:0f;
        var distance=Math.Abs(end-from);
        if(reduced||distance<.0001f)return Value=end;
        var t=(float)Math.Clamp((now-started)/(.28*distance),0,1);
        var ease=t<.5f?4*t*t*t:1-MathF.Pow(-2*t+2,3)/2;
        return Value=from+(end-from)*ease;
    }
}
