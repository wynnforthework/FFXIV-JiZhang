using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
namespace JiZhang;

// Bake at the requested pixel sizes instead of magnifying small glyph textures.
internal sealed class LedgerFonts : IDisposable
{
    private readonly IUiBuilder builder;
    private readonly Configuration config;
    private readonly IFontHandle[] handles;
    private float size;
    public LedgerFonts(IUiBuilder builder, Configuration config) {
        this.builder=builder;this.config=config;
        size=PixelSize();
        handles=new[]{1f,1.3f,2f}.Select(tier=>builder.FontAtlas.NewDelegateFontHandle(e=>e.OnPreBuild(tk=>tk.AddDalamudDefaultFont(size*tier)))).ToArray();
        builder.DefaultFontChanged+=Refresh;
    }
    private float PixelSize() => builder.DefaultFontSpec.SizePx*Math.Clamp(config.Scale,.8f,1.6f);
    public IDisposable Push(float tier) => handles[tier>=1.8f?2:tier>=1.2f?1:0].Push();
    public void Refresh() {
        var next=PixelSize();if(Math.Abs(next-size)<.01f)return;
        size=next;_ = builder.FontAtlas.BuildFontsAsync();
    }
    public void Dispose(){builder.DefaultFontChanged-=Refresh;foreach(var handle in handles)handle.Dispose();}
}
