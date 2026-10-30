using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiZhang;
using JiZhang.Core;
unsafe {
    var root=Path.GetFullPath(args.FirstOrDefault()??"artifacts/ui-preview");Directory.CreateDirectory(root);
    ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.LogFilename=null;io.DeltaTime=1f/60;
    io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/msyh.ttc",17,default,io.Fonts.GetGlyphRangesChineseFull());
    io.Fonts.Build();
    for(var t=0;t<io.Fonts.Textures.Size;t++) {
        byte* pixels=null;int w=0,h=0;io.Fonts.GetTexDataAsRGBA32(t,&pixels,&w,&h);
        io.Fonts.Textures.Data[t].TexID=new ImTextureID((ulong)t+1);
        File.WriteAllBytes(Path.Combine(root,$"atlas-{t}.rgba"),new ReadOnlySpan<byte>(pixels,w*h*4).ToArray());
        File.WriteAllText(Path.Combine(root,$"atlas-{t}.json"),JsonSerializer.Serialize(new{width=w,height=h}));
    }
    foreach(var scenario in new[]{("empty",false,0,1040,780,1f),("populated",true,0,1040,780,1f),("transactions",true,1,1040,780,1f),("empty-transactions",false,1,1040,780,1f),("settings",false,2,1040,820,1f),("narrow",true,0,760,780,1f),("scaled",true,0,1040,780,1.6f)}) {
        var (name,data,tab,width,height,scale)=scenario;
        var model=new PreviewContext(data);model.Config.Scale=scale;var view=new LedgerView(model);view.SelectTab(tab);
        io.DisplaySize=new(width,height);var open=true;
        for(var frame=0;frame<3;frame++) {
            io.MousePos=new(470,480);ImGui.NewFrame();LedgerTheme.Push();
            ImGui.SetNextWindowPos(Vector2.Zero,ImGuiCond.Always);ImGui.SetNextWindowSize(new(width,height),ImGuiCond.Always);
            if(ImGui.Begin("金币账本###JiZhang",ref open,ImGuiWindowFlags.NoCollapse))view.Draw();
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
        public Configuration Config{get;}=new();public LedgerEntry[] Entries{get;}
        public BalanceSample? Current{get;}=new(1,"示例角色",DateTimeOffset.Now,1518170,new(128,"利姆萨·罗敏萨"));
        public string Status=>"正在自动记录 · 离线示例";public string DataDirectory=>"本地账本目录";
        public PreviewContext(bool data) {
            if(!data){Entries=[];return;}
            var list=new List<LedgerEntry>();long balance=1518170;
            for(var i=0;i<14;i++) {
                var amount=i%3==0?12800+i*110:-580-i*80;
                list.Add(new(i.ToString(),1,"示例角色",DateTimeOffset.Now.AddDays(-i),balance-amount,balance,new(128,"利姆萨·罗敏萨"),amount>0?Category.QuestReward:i%2==0?Category.MarketPurchase:Category.Teleport));
            }Entries=list.ToArray();
        }
        public void SaveConfig(){}public Task<string> ExportAsync()=>Task.FromResult("预览");public Task<string> BackupAsync()=>Task.FromResult("预览");public void Restore(string path){}
    }
}
