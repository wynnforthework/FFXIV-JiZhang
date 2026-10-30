using Dalamud.Configuration;
namespace JiZhang;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ExcludeTransfers { get; set; }
    public float Scale { get; set; } = 1;
    public bool EnableExperimentalClassification { get; set; }
}
