using System;

using Quartz.Plugin.HttpApi;
using Quartz.Util;

namespace Quartz
{
    public static class HttpApiPluginConfigurationExtensions
    {
        public static T UseHttpApi<T>(this T schedulerBuilder, Action<HttpApiOptions>? configure = null) where T : IPropertyConfigurer
        {
            schedulerBuilder.SetProperty("quartz.plugin.httpApi.type", typeof(HttpApiPlugin).AssemblyQualifiedNameWithoutVersion());
            var options = new HttpApiOptions(schedulerBuilder);
            configure?.Invoke(options);
            return schedulerBuilder;
        }
    }
    
    public class HttpApiOptions : PropertiesHolder
    {
        protected internal HttpApiOptions(IPropertyConfigurer parent) : base(parent)
        {
        }

        /// <summary>
        /// Host to bind to.
        /// </summary>
        public string Host
        {
            set => SetProperty("quartz.plugin.httpApi.host", value.ToString());
        }
        
        /// <summary>
        /// Port to listen on.
        /// </summary>
        public int Port
        {
            set => SetProperty("quartz.plugin.httpApi.port", value.ToString());
        }
    }
}