using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using JiZhang.Core;
using Lumina.Excel.Sheets;
namespace JiZhang;

public sealed partial class Plugin
{
    [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.IGameInteropProvider Interop {get;private set;}=null!;
    private unsafe delegate bool TeleportDelegate(Telepo* self,uint aetheryteId,byte subIndex);
    private unsafe delegate bool TicketTeleportDelegate(Telepo.SelectUseTicketInvoker* self,uint aetheryteId,byte subIndex);
    private Hook<TeleportDelegate>? teleportHook;
    private Hook<TicketTeleportDelegate>? ticketTeleportHook;
    private readonly TeleportTracker teleport=new();
    private int teleportDepth;
    private sealed record TeleportSnapshot(ulong Character,DateTimeOffset Time,uint Territory,long Cost,string Destination,Location Departure);
    private unsafe void StartTeleportAdapter() {
        try {
            teleportHook=Interop.HookFromAddress<TeleportDelegate>((nint)Telepo.MemberFunctionPointers.Teleport,OnTeleport);
            teleportHook.Enable();
        } catch(Exception e){Log.Warning(e,"传送适配不可用，保留余额记录与未分类回退");}
        try {
            ticketTeleportHook=Interop.HookFromAddress<TicketTeleportDelegate>((nint)Telepo.SelectUseTicketInvoker.MemberFunctionPointers.TeleportWithTickets,OnTicketTeleport);
            ticketTeleportHook.Enable();
        } catch(Exception e){Log.Warning(e,"传送券请求适配不可用，保留未分类回退");}
    }
    private unsafe TeleportSnapshot? SnapshotTeleport(Telepo* self,uint id,byte subIndex) {
        try {
            if(Current is not {} current||self==null)return null;
            foreach(var info in self->TeleportList) {
                if(info.AetheryteId!=id||info.SubIndex!=subIndex)continue;
                var row=Data.GetExcelSheet<Aetheryte>().GetRowOrDefault(id);
                return new(current.CharacterId,DateTimeOffset.UtcNow,info.TerritoryId,info.GilCost,
                    row?.PlaceName.Value.Name.ToString()??$"以太之光 #{id}",ReadLocation());
            }
            Log.Debug("传送请求未找到费用快照：以太之光 {Id} / 子区域 {SubIndex}",id,subIndex);
        }catch(Exception e){Log.Warning(e,"传送请求证据读取失败");}
        return null;
    }
    private void AcceptTeleport(bool accepted,TeleportSnapshot? snapshot) {
        teleport.Reset();
        if(!accepted||snapshot is not {} s)return;
        teleport.Accept(s.Character,s.Time,s.Territory,s.Cost,s.Destination,s.Departure);
        Log.Debug("传送请求已捕获：区域 {Territory}，预计费用 {Cost}",s.Territory,s.Cost);
    }
    private unsafe bool OnTeleport(Telepo* self,uint id,byte subIndex) {
        var outer=teleportDepth++==0;
        try {
            var snapshot=outer?SnapshotTeleport(self,id,subIndex):null;
            var accepted=teleportHook!.Original(self,id,subIndex);
            if(outer)AcceptTeleport(accepted,snapshot);
            return accepted;
        }finally{teleportDepth--;}
    }
    private unsafe bool OnTicketTeleport(Telepo.SelectUseTicketInvoker* self,uint id,byte subIndex) {
        var outer=teleportDepth++==0;
        try {
            var snapshot=outer?SnapshotTeleport(self==null?null:self->Telepo,id,subIndex):null;
            var accepted=ticketTeleportHook!.Original(self,id,subIndex);
            if(outer)AcceptTeleport(accepted,snapshot);
            return accepted;
        }finally{teleportDepth--;}
    }
}
