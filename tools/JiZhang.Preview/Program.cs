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
    foreach(var scenario in new[]{("empty",false,0,1040,780,1f),("populated",true,0,1040,780,1f),("reference-size",true,0,936,768,1f),("expense-only",true,0,1040,780,1f),("income-only",true,0,1040,780,1f),("huge-income",true,0,1040,780,1f),("huge-expense",true,0,1040,780,1f),("transactions",true,1,1040,780,1f),("empty-transactions",false,1,1040,780,1f),("settings",false,2,1040,820,1f),("narrow",true,0,760,780,1f),("scaled",true,0,1040,780,1.6f)}) {
        var (name,data,tab,width,height,scale)=scenario;
        var model=new PreviewContext(data,name);model.Config.Scale=scale;var view=new LedgerView(model);view.SelectTab(tab);
        io.DisplaySize=new(width,height);var open=true;
        for(var frame=0;frame<3;frame++) {
            io.MousePos=new(470,480);ImGui.NewFrame();LedgerTheme.Push();
            ImGui.SetNextWindowPos(Vector2.Zero,ImGuiCond.Always);ImGui.SetNextWindowSize(new(width,height),ImGuiCond.Always);
            if(ImGui.Begin("金币账本###JiZhang",ref open,ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse))view.Draw();
            ImGui.End();LedgerTheme.Pop();ImGui.Render();
        }
        var dd=ImGui.GetDrawData();
        using var output=new BinaryWriter(File.Create(Path.Combine(root,name+".bin")));
        output.Write(width);output.Write(height);output.Write(dd.CmdListsCount);
        for(var l=0;l<dd.CmdListsCount;l++) {
            var list=new ImDrawListPtr(dd.CmdLists[l]);output.Write(list.VtxBuffer.Size);output.Write(list.IdxBuffer.Size);output.Write(list.CmdBuffer.Size);
            for(var v=0;v<list.VtxBuffer.Size;v++){var vert=list.VtxBuffer[v];output.Write(vert.Pos.X);output.Write(vert.Pos.Y);output.Write(vert.Uv.X);output.Write(vert.Uv.Y);output.Write(vert.Col);}
            for(var i=0;i<list.IdxBuffer.Size;i++)output.Write(list.IdxBuffer[i]);
            for(var c=0;c<list.CmdBuffer.Size;c++){var cmd=list.CmdBuffer[c];output.Write(cmd.ClipRect.X);output.Write(cmd.ClipRect.Y);output.Write(cmd.ClipRect.Z);output.Write(cmd.ClipRect.W);output.Write(cmd.TextureId.Handle);output.Write(cmd.VtxOffset);output.Write(cmd.IdxOffset);output.Write(cmd.ElemCount);}
        }
        Console.WriteLine($"Rendered {name}: {dd.TotalVtxCount} vertices, {dd.CmdListsCount} draw lists");
    }
    ImGui.DestroyContext();
}
namespace JiZhang {
    internal class Configuration { public float Scale{get;set;}=1; public bool ExcludeTransfers{get;set;} public bool EnableExperimentalClassification{get;set;} }
    internal sealed class PreviewContext : ILedgerUiContext {
        public static List<ImFontPtr> Fonts{get;}=[];
        private sealed class FontScope : IDisposable { public void Dispose()=>ImGui.PopFont(); }
        public IDisposable PushFont(float tier){ImGui.PushFont(Fonts[(Config.Scale>1.3f?3:0)+(tier>=1.8f?2:tier>=1.2f?1:0)]);return new FontScope();}
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
                list.Add(new(i.ToString(),1,"示例角色",DateTimeOffset.Now.AddDays(-i),balance-amount,balance,new(128,"利姆萨·罗敏萨"),amount>0?Category.QuestReward:i%2==0?Category.MarketPurchase:Category.Teleport));
            }Entries=list.ToArray();
        }
        public void SaveConfig(){}public Task<string> ExportAsync()=>Task.FromResult("预览");public Task<string> BackupAsync()=>Task.FromResult("预览");public void Restore(string path){}
    }
}
