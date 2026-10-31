using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiZhang;
using JiZhang.Core;
unsafe {
    var root=Path.GetFullPath(args.FirstOrDefault()??"artifacts/ui-preview");Directory.CreateDirectory(root);
    ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.LogFilename=null;io.DeltaTime=1f/60;
    var glyphText=string.Join("",Directory.GetFiles("src","*.cs",SearchOption.AllDirectories).Where(p=>!p.Contains("obj")).Select(File.ReadAllText))+"示例角色利姆萨罗敏萨本地账本目录离线预览";
    var ranges=Enumerable.Range(32,95).Select(i=>(char)i).Concat(glyphText.Where(c=>c>=127)).Distinct().Order().SelectMany(c=>new[]{(ushort)c,(ushort)c}).Append((ushort)0).ToArray();
    var rangeHandle=GCHandle.Alloc(ranges,GCHandleType.Pinned);
    foreach(var scale in new[]{1f,1.6f})
        foreach(var tier in new[]{1f,1.3f,2f})
            PreviewContext.Fonts.Add(io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/msyh.ttc",MathF.Round(17*tier*scale),default,(ushort*)rangeHandle.AddrOfPinnedObject()));
    io.Fonts.Build();
    rangeHandle.Free();
    for(var t=0;t<io.Fonts.Textures.Size;t++) {
        byte* pixels=null;int w=0,h=0;io.Fonts.GetTexDataAsRGBA32(t,&pixels,&w,&h);
        io.Fonts.Textures.Data[t].TexID=new ImTextureID((ulong)t+1);
        File.WriteAllBytes(Path.Combine(root,$"atlas-{t}.rgba"),new ReadOnlySpan<byte>(pixels,w*h*4).ToArray());
        File.WriteAllText(Path.Combine(root,$"atlas-{t}.json"),JsonSerializer.Serialize(new{width=w,height=h}));
    }
    foreach(var scenario in (args.Contains("--motion")?Array.Empty<(string,bool,int,int,int,float)>():new[]{("empty",false,0,1040,780,1f),("populated",true,0,1040,780,1f),("marketing-overview",true,0,1040,1000,1f),("mixed-categories",true,0,1040,780,1f),("reference-size",true,0,936,768,1f),("scrolled",true,0,936,768,1f),("year",true,0,1040,780,1f),("all",true,0,1040,780,1f),("expense-only",true,0,1040,780,1f),("income-only",true,0,1040,780,1f),("huge-income",true,0,1040,780,1f),("huge-expense",true,0,1040,780,1f),("transactions",true,1,1040,780,1f),("empty-transactions",false,1,1040,780,1f),("settings",false,2,1040,960,1f),("narrow",true,0,760,780,1f),("scaled",true,0,1040,780,1.6f)})) {
        var (name,data,tab,width,height,scale)=scenario;
        var model=new PreviewContext(data,name);model.Config.Scale=scale;var view=new LedgerView(model);view.SelectTab(tab);
        io.DisplaySize=new(width,height);var open=true;
        for(var frame=0;frame<6;frame++) {
            model.UiTime=frame/60d;io.MousePos=new(470,480);if(name=="scrolled"&&frame==3)io.AddMouseWheelEvent(0,-5);
            if((name=="year"||name=="all")&&frame is 3 or 4) {var r=view.HitRegions["range-"+(name=="year"?1:2)];io.AddMousePosEvent((r.X+r.Z)/2,(r.Y+r.W)/2);io.AddMouseButtonEvent(0,frame==3);}
            ImGui.NewFrame();LedgerTheme.Push();
            ImGui.SetNextWindowPos(Vector2.Zero,ImGuiCond.Always);ImGui.SetNextWindowSize(new(width,height),ImGuiCond.Always);
            if(ImGui.Begin("金币账本###JiZhang",ref open,ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse))view.Draw();
            ImGui.End();LedgerTheme.Pop();ImGui.Render();
        }
        if(name=="scrolled") {
            if(view.ContentScrollForPreview<=0||view.PanelScrollForPreview.Values.Any(v=>v!=0))throw new Exception("Wheel input must scroll the outer viewport only");
            var card=view.HitRegions["panel-recent"];var title=view.HitRegions["recent-title"];
            if(title.Y<card.Y||title.W>card.W)throw new Exception("Recent header escaped card bounds");
            Console.WriteLine("Scroll: outer viewport moves, all cards remain at zero inner offset; recent header stays inside card.");
        }
        if(name=="year"&&view.ActiveRange!=1||name=="all"&&view.ActiveRange!=2)throw new Exception("Period click failed");
        SaveDraw(name,width,height);
    }
    void SaveDraw(string name,int width,int height) {
        var dd=ImGui.GetDrawData();
        using var output=new BinaryWriter(File.Create(Path.Combine(root,name+".bin")));
        output.Write(width);output.Write(height);output.Write(dd.CmdListsCount);
        for(var l=0;l<dd.CmdListsCount;l++) {
            var list=new ImDrawListPtr(dd.CmdLists[l]);output.Write(list.VtxBuffer.Size);output.Write(list.IdxBuffer.Size);output.Write(list.CmdBuffer.Size);
            for(var v=0;v<list.VtxBuffer.Size;v++){var vert=list.VtxBuffer[v];output.Write(vert.Pos.X);output.Write(vert.Pos.Y);output.Write(vert.Uv.X);output.Write(vert.Uv.Y);output.Write(vert.Col);}
            for(var i=0;i<list.IdxBuffer.Size;i++)output.Write(list.IdxBuffer[i]);
            for(var c=0;c<list.CmdBuffer.Size;c++){var cmd=list.CmdBuffer[c];output.Write(cmd.ClipRect.X);output.Write(cmd.ClipRect.Y);output.Write(cmd.ClipRect.Z);output.Write(cmd.ClipRect.W);output.Write(cmd.TextureId.Handle);output.Write(cmd.VtxOffset);output.Write(cmd.IdxOffset);output.Write(cmd.ElemCount);}
        }
    }
    if(args.Contains("--motion")) {
        var model=new PreviewContext(true,"motion");var view=new LedgerView(model);var rows=new List<object>();
        io.DisplaySize=new(936,768);io.ConfigInputTrickleEventQueue=false;
        var samples=new HashSet<int>{3,6,10,18,28,30,34,38,48,52,58,64,70,80,84,88,95,105,112,118,128,133,140};
        for(var frame=0;frame<=212;frame++) {
            model.UiTime=frame/60d;io.DeltaTime=1f/60;
            if(frame==180)model.ReducedMotion=true;
            var mouse=new Vector2(-100,-100);var down=false;
            // Actual pointer press and release; each target is held for two frames.
            if(frame is >=4 and <=6){mouse=Target("nav-1");down=frame<6;} // transactions
            Vector2 Target(string id){var r=view.HitRegions[id];return new((r.X+r.Z)/2,(r.Y+r.W)/2);}
            if(frame is >=26 and <=28){mouse=Target("direction");down=frame<28;} // direction dropdown
            if(frame is >=36 and <=38){mouse=Target("direction-option-2");down=frame<38;} // popup item
            if(frame is >=46 and <=48){mouse=Target("nav-2");down=frame<48;} // settings
            if(frame is >=62 and <=64 && view.HitRegions.ContainsKey("transfers")){mouse=Target("transfers");down=frame<64;} // switch
            if(frame is >=78 and <=80){mouse=new(868,25);down=frame<80;} // fold
            if(frame is >=86 and <=88){mouse=new(868,25);down=frame<88;} // reverse before settled
            if(frame is >=110 and <=112){mouse=Target("nav-0");down=frame<112;} // overview
            if(frame is >=126 and <=128){mouse=Target("range-2");down=frame<128;} // 30 days
            if(frame is >=146 and <=148){mouse=new(868,25);down=frame<148;}
            if(frame is >=182 and <=184){mouse=new(868,25);down=frame<184;}
            if(frame is >=194 and <=196){mouse=Target("nav-2");down=frame<196;}
            if(frame is >=208 and <=210){mouse=new(906,25);down=frame<210;}
            io.AddMousePosEvent(mouse.X,mouse.Y);io.AddMouseButtonEvent(0,down);
            ImGui.NewFrame();LedgerTheme.Push();
            ImGui.SetNextWindowPos(Vector2.Zero,ImGuiCond.Always);
            if(frame==0)ImGui.SetNextWindowSize(new(936,768),ImGuiCond.Always);
            var open=true;
            ImGui.Begin("金币账本###JiZhang",ref open,ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse);
            view.Draw();var size=ImGui.GetWindowSize();
            rows.Add(new{frame,time=model.UiTime,tab=view.ActiveTab,nav=view.NavPosition,range=view.ActiveRange,rangePosition=view.RangePosition,reveal=view.PageProgress,fold=view.FoldAmount,height=size.Y,reduced=model.ReducedMotion,closed=view.CloseRequested,direction=view.DirectionForPreview,transfers=model.Config.ExcludeTransfers});
            ImGui.End();LedgerTheme.Pop();ImGui.Render();
            if(samples.Contains(frame)||frame is 160 or 174 or 184 or 196 or 212)SaveDraw($"motion-{frame:000}",936,768);
        }
        File.WriteAllText(Path.Combine(root,"motion-state.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        if(view.ActiveTab!=2||view.ActiveRange!=2||!model.Config.ExcludeTransfers||!view.CloseRequested)throw new Exception("Native click sequence failed");
        // Reduced-motion state must settle immediately, including after a mid-transition toggle.
        model.ReducedMotion=true;view.ToggleFold();view.PrepareFrame();
        if(view.FoldAmount!=1)throw new Exception("Reduced-motion fold did not settle");
        Console.WriteLine("Motion input sequence exported.");
    }
    ImGui.DestroyContext();
}
namespace JiZhang {
    internal class Configuration { public float Scale{get;set;}=1; public bool ExcludeTransfers{get;set;} public bool EnableExperimentalClassification{get;set;} }
    internal sealed class PreviewContext : ILedgerUiContext {
        public static List<ImFontPtr> Fonts{get;}=[];
        private sealed class FontScope : IDisposable { public void Dispose()=>ImGui.PopFont(); }
        public IDisposable PushFont(float tier){ImGui.PushFont(Fonts[(Config.Scale>1.3f?3:0)+(tier>=1.8f?2:tier>=1.2f?1:0)]);return new FontScope();}
        public bool ReducedMotion{get;set;}
        public double UiTime{get;set;}
        public Configuration Config{get;}=new();public LedgerEntry[] Entries{get;}
        public BalanceSample? Current{get;}=new(1,"示例角色",DateTimeOffset.Now,1518170,new(128,"利姆萨·罗敏萨"));
        public string Status=>"正在自动记录 · 离线示例";public string DataDirectory=>"本地账本目录";
        public PreviewContext(bool data,string scenario) {
            if(!data){Entries=[];return;}
            var list=new List<LedgerEntry>();long balance=1518170;
            for(var i=0;i<14;i++) {
                long amount=scenario switch {
                    "expense-only"=>-580-i*80,
                    "income-only"=>12800+i*110,
                    "huge-income"=>i%3==0?12000000+i*110:-580-i*80,
                    "huge-expense"=>i%3==0?580+i*80:-12000000-i*110,
                    _=>i%3==0?2800+i*110:-1580-i*80
                };
                list.Add(new(i.ToString(),1,"示例角色",DateTimeOffset.Now.AddDays(-i),balance-amount,balance,new(128,"利姆萨·罗敏萨"),amount>0?Category.QuestReward:scenario=="mixed-categories"?new[]{Category.Teleport,Category.Repair,Category.NpcPurchase,Category.MarketPurchase,Category.PlayerTrade,Category.RetainerTransfer}[i%6]:i%2==0?Category.MarketPurchase:Category.Teleport));
            }Entries=list.ToArray();
        }
        public void SaveConfig(){}public Task<string> ExportAsync()=>Task.FromResult("预览");public Task<string> BackupAsync()=>Task.FromResult("预览");public Task<string> ReclassifyAsync()=>Task.FromResult("预览");public void Restore(string path){}
    }
}
