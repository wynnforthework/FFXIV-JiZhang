using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using JiZhang.Core;
using Lumina.Excel.Sheets;
namespace JiZhang;

public sealed partial class Plugin
{
    [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.IGameInteropProvider Interop {get;private set;}=null!;
    private unsafe delegate bool TeleportDelegate(Telepo* self,uint aetheryteId,byte subIndex);
    private Hook<TeleportDelegate>? teleportHook;
    private readonly TeleportTracker teleport=new();
    private unsafe void StartTeleportAdapter() {
        try {
            teleportHook=Interop.HookFromAddress<TeleportDelegate>((nint)Telepo.MemberFunctionPointers.Teleport,OnTeleport);
            teleportHook.Enable();
        } catch(Exception e){Log.Warning(e,"传送适配不可用，保留余额记录与未分类回退");}
    }
    private unsafe bool OnTeleport(Telepo* self,uint aetheryteId,byte subIndex) {
        // Read-only hook: preserve the game's return value and invoke its original exactly once.
        var accepted=teleportHook!.Original(self,aetheryteId,subIndex);
        if(!accepted){teleport.Reset();return false;}
        try {
            teleport.Reset();
            if(Current is not {} current||self==null)return accepted;
            foreach(var info in self->TeleportList) {
                if(info.AetheryteId!=aetheryteId||info.SubIndex!=subIndex)continue;
                var row=Data.GetExcelSheet<Aetheryte>().GetRowOrDefault(aetheryteId);
                var destination=row?.PlaceName.Value.Name.ToString()??$"以太之光 #{aetheryteId}";
                teleport.Accept(current.CharacterId,DateTimeOffset.UtcNow,info.TerritoryId,info.GilCost,destination,current.Location);
                break;
            }
        }catch(Exception e){teleport.Reset();Log.Warning(e,"传送请求证据读取失败");}
        return accepted;
    }
}
