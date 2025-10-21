public class EverMediaPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public EverMediaPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) 
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }
    
    public static EverMediaPlugin Instance { get; private set; }
    
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "evermedia",
                EmbeddedResourcePath = "evermedia.ConfigurationPage.html"
            }
        };
    }
}
