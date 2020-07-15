using Quartz.Util;

namespace Quartz.Plugin.HttpApi.Contract
{
    public class SchedulerThreadPoolDto
    {
        public SchedulerThreadPoolDto(SchedulerMetaData metaData)
        {
            Type = metaData.ThreadPoolType.AssemblyQualifiedNameWithoutVersion();
            Size = metaData.ThreadPoolSize;
        }

        public string Type { get; private set; }
        public int Size { get; private set; }
    }
}