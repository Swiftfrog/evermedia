// Plugin.cs
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins; // BasePlugin<T>, IPlugin
using MediaBrowser.Model.Plugins; // BasePluginConfiguration
using MediaBrowser.Model.Serialization; // IXmlSerializer
using System;// --- 新增：用于服务注册 ---
using MediaBrowser.Common; // IPluginServiceRegistrator
using Microsoft.Extensions.DependencyInjection; // IServiceCollection
// --- 新增：引用你的服务和提供者 ---
using EmbyMedia.Plugin; // 假设你的其他类都在这个命名空间

namespace EmbyMedia.Plugin // 确保命名空间与项目文件一致
{
    /// <summary>
    /// 插件主类，定义插件的基本信息和配置。
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IPlugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">应用程序路径。</param>
        /// <param name="xmlSerializer">XML 序列化器。</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            // 构造函数主体通常为空，除非需要初始化插件级别的东西
        }

        /// <inheritdoc />
        public override string Name => "EmbyMedia Plugin";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("YOUR-UNIQUE-GUID-HERE"); // *** 请务必替换为一个全新的 GUID ***

        /// <inheritdoc />
        public override string Description => "Provides enhanced MediaInfo handling for STRM files and backups/restores metadata.";
    }

    /// <summary>
    /// 插件配置类 (如果需要的话)。
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        // 可以在这里添加插件的配置选项
        // 例如: public bool EnableAutomaticBackup { get; set; } = true;
    }

    /// <summary>
    /// 服务注册器，用于向 Emby 的依赖注入容器注册插件所需的服务。
    /// 实现 IPluginServiceRegistrator 接口。
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <summary>
        /// 向服务集合注册插件的服务。
        /// </summary>
        /// <param name="serviceCollection">服务集合。</param>
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            // 1. 注册你的核心服务 IMediaInfoService
            //    将 IMediaInfoService 映射到 MediaInfoService 实现
            //    使用 AddSingleton 确保在整个插件生命周期内只有一个实例
            serviceCollection.AddSingleton<IMediaInfoService, MediaInfoService>();

            // 2. 注册你的自定义元数据提供者 ICustomMetadataProvider<Video>
            //    Emby 会自动发现并使用它
            serviceCollection.AddSingleton<ICustomMetadataProvider<Video>, MediaInfoCustomMetadataProvider>();

            // 3. 注册你的计划任务 IScheduledTask
            //    Emby 会自动发现并使用它
            serviceCollection.AddSingleton<IScheduledTask, MediaInfoRestoreTask>();

            // --- 关于 IHasOrder ---
            // MediaInfoCustomMetadataProvider 实现了 IHasOrder。
            // 你不需要在这里特别注册 IHasOrder。
            // Emby 在解析 ICustomMetadataProvider<T> 时，如果发现它也实现了 IHasOrder，
            // 会自动调用 Order 属性来确定优先级。
        }
    }
}
