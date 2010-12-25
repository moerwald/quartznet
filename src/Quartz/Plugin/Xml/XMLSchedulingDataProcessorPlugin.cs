#region License
/* 
 * Copyright 2001-2009 Terracotta, Inc. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
#if !ClientProfile
using System.Web;
#endif

using Common.Logging;

using Quartz.Job;
using Quartz.Simpl;
using Quartz.Spi;
using Quartz.Util;
using Quartz.Xml;

namespace Quartz.Plugin.Xml
{
    /// <summary> 
    /// This plugin loads XML file(s) to add jobs and schedule them with triggers
    /// as the scheduler is initialized, and can optionally periodically scan the
    /// file for changes.
    ///</summary>
    /// <remarks>
    /// The periodically scanning of files for changes is not currently supported in a 
    /// clustered environment.
    /// </remarks> 
    /// <author>James House</author>
    /// <author>Pierre Awaragi</author>
    public class XMLSchedulingDataProcessorPlugin : ISchedulerPlugin, IFileScanListener
    {
        private readonly ILog log;
        private const int MaxJobTriggerNameLength = 80;
        private const string JobInitializationPluginName = "XMLSchedulingDataProcessorPlugin";
        private const char FileNameDelimiter = ',';

        private bool failOnFileNotFound = true;
        private string fileNames = XMLSchedulingDataProcessor.QuartzXmlFileName;

        // Populated by initialization
        private readonly List<KeyValuePair<string, JobFile>> jobFiles = new List<KeyValuePair<string, JobFile>>();

        private TimeSpan scanInterval = TimeSpan.Zero;

        private bool started;

        private ITypeLoadHelper typeLoadHelper;

        private readonly Collection.HashSet<string> jobTriggerNameSet = new Collection.HashSet<string>();
        private IScheduler scheduler;
        private string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="JobInitializationPlugin"/> class.
        /// </summary>
        public XMLSchedulingDataProcessorPlugin()
        {
            log = LogManager.GetLogger(typeof (JobInitializationPlugin));
            fileNames = JobSchedulingDataProcessor.QuartzXmlFileName;
        }


        /// <summary>
        /// Gets the log.
        /// </summary>
        /// <value>The log.</value>
        protected ILog Log
        {
            get { return log; }
        }


        public string Name
        {
            get { return name; }
        }

        public IScheduler Scheduler
        {
            get { return scheduler; }
        }

        protected ITypeLoadHelper TypeLoadHelper
        {
            get { return typeLoadHelper; }
        }

        /// <summary> 
        /// Comma separated list of file names (with paths) to the XML files that should be read.
        /// </summary>
        public virtual string FileNames
        {
            get { return fileNames; }
            set { fileNames = value; }
        }

        /// <summary> 
        /// The interval at which to scan for changes to the file.  
        /// If the file has been changed, it is re-loaded and parsed.   The default 
        /// value for the interval is 0, which disables scanning.
        /// </summary>
        [TimeSpanParseRule(TimeSpanParseRule.Seconds)]
        public virtual TimeSpan ScanInterval
        {
            get { return scanInterval; }
            set { scanInterval = value; }
        }

        /// <summary> 
        /// Whether or not initialization of the plugin should fail (throw an
        /// exception) if the file cannot be found. Default is <see langword="true" />.
        /// </summary>
        public virtual bool FailOnFileNotFound
        {
            get { return failOnFileNotFound; }
            set { failOnFileNotFound = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fName"></param>
        public virtual void FileUpdated(string fName)
        {
            if (started)
            {
                ProcessFile(fName);
            }
        }

        /// <summary>
        /// Called during creation of the <see cref="IScheduler"/> in order to give
        /// the <see cref="ISchedulerPlugin"/> a chance to initialize.
        /// </summary>
        /// <param name="pluginName">The name.</param>
        /// <param name="sched">The scheduler.</param>
        /// <throws>SchedulerConfigException </throws>
        public virtual void Initialize(string pluginName, IScheduler sched)
        {
            name = pluginName;
            scheduler = sched;
            typeLoadHelper = new SimpleTypeLoadHelper();
            typeLoadHelper.Initialize();

            Log.Info("Registering Quartz Job Initialization Plug-in.");

            // Create JobFile objects
            string[] tokens = fileNames.Split(FileNameDelimiter);

            foreach (string token in tokens)
            {
                JobFile jobFile = new JobFile(this, token);
                jobFiles.Add(new KeyValuePair<string, JobFile>(jobFile.FilePath, jobFile));
            }
        }

        /// <summary>
        /// Called when the associated <see cref="IScheduler"/> is started, in order
        /// to let the plug-in know it can now make calls into the scheduler if it
        /// needs to.
        /// </summary>
        public virtual void Start()
        {
            try
            {
                if (jobFiles.Count > 0)
                {
                    if (scanInterval > TimeSpan.Zero)
                    {
                        scheduler.Context.Put(JobInitializationPluginName + '_' + Name, this);
                    }

                    foreach (KeyValuePair<string, JobFile> pair in jobFiles)
                    {
                        JobFile jobFile = pair.Value;

                        if (scanInterval > TimeSpan.Zero)
                        {
                            string jobTriggerName = BuildJobTriggerName(jobFile.FileBasename);

                            SimpleTrigger trig = new SimpleTrigger(
                                jobTriggerName,
                                JobInitializationPluginName,
                                SystemTime.UtcNow(), null,
                                SimpleTrigger.RepeatIndefinitely, scanInterval);
                            trig.Volatile = true;

                            JobDetail job = new JobDetail(
                                jobTriggerName,
                                JobInitializationPluginName,
                                typeof(FileScanJob));

                            job.Volatile = true;
                            job.JobDataMap.Put(FileScanJob.FileName, jobFile.FilePath);
                            job.JobDataMap.Put(FileScanJob.FileScanListenerName, JobInitializationPluginName + '_' + Name);

                            scheduler.ScheduleJob(job, trig);
                            Log.DebugFormat("Scheduled file scan job for data file: {0}, at interval: {1}", jobFile.FileName, scanInterval);
                        }

                        ProcessFile(jobFile);
                    }
                }
            }
            catch (SchedulerException se)
            {
                Log.Error("Error starting background-task for watching jobs file.", se);
            }
            finally
            {
                started = true;
            }
        }

        /// <summary>
        /// Helper method for generating unique job/trigger name for the  
        /// file scanning jobs (one per FileJob).  The unique names are saved
        /// in jobTriggerNameSet.
        /// </summary>
        /// <param name="fileBasename"></param>
        /// <returns></returns>
        private string BuildJobTriggerName(string fileBasename)
        {
            // Name w/o collisions will be prefix + _ + filename (with '.' of filename replaced with '_')
            // For example: JobInitializationPlugin_jobInitializer_myjobs_xml
            string jobTriggerName = JobInitializationPluginName + '_' + Name + '_' + fileBasename.Replace('.', '_');

            // If name is too long (DB column is 80 chars), then truncate to max length
            if (jobTriggerName.Length > MaxJobTriggerNameLength)
            {
                jobTriggerName = jobTriggerName.Substring(0, MaxJobTriggerNameLength);
            }

            // Make sure this name is unique in case the same file name under different
            // directories is being checked, or had a naming collision due to length truncation.
            // If there is a conflict, keep incrementing a _# suffix on the name (being sure
            // not to get too long), until we find a unique name.
            int currentIndex = 1;
            while (jobTriggerNameSet.Add(jobTriggerName) == false)
            {
                // If not our first time through, then strip off old numeric suffix
                if (currentIndex > 1)
                {
                    jobTriggerName = jobTriggerName.Substring(0, jobTriggerName.LastIndexOf('_'));
                }

                string numericSuffix = "_" + currentIndex++;

                // If the numeric suffix would make the name too long, then make room for it.
                if (jobTriggerName.Length > (MaxJobTriggerNameLength - numericSuffix.Length))
                {
                    jobTriggerName = jobTriggerName.Substring(0, (MaxJobTriggerNameLength - numericSuffix.Length));
                }

                jobTriggerName += numericSuffix;
            }

            return jobTriggerName;
        }

        /// <summary>
        /// Called in order to inform the <see cref="ISchedulerPlugin" /> that it
        /// should free up all of it's resources because the scheduler is shutting
        /// down.
        /// </summary>
        public virtual void Shutdown()
        {
            // nothing to do
        }

        private void ProcessFile(JobFile jobFile)
        {
            if ((jobFile == null) || (jobFile.FileFound == false))
            {
                return;
            }

            try
            {
                XMLSchedulingDataProcessor processor = new XMLSchedulingDataProcessor(this.TypeLoadHelper);

                processor.AddJobGroupToNeverDelete(JobInitializationPluginName);
                processor.AddTriggerGroupToNeverDelete(JobInitializationPluginName);

                processor.ProcessFileAndScheduleJobs(
                        jobFile.FileName,
                        jobFile.FileName, // systemId 
                        scheduler);
            }
            catch (Exception e)
            {
                Log.Error("Error scheduling jobs: " + e.Message, e);
            }
        }

        public void ProcessFile(string filePath)
        {
            JobFile file = null;
            int idx = jobFiles.FindIndex(pair => pair.Key == filePath);
            if (idx >= 0)
            {
                file = jobFiles[idx].Value;
            }
            ProcessFile(file);
        }

    internal class JobFile
    {
        private readonly string fileName;

        // These are set by initialize()
        private string filePath;
        private string fileBasename;
        private bool fileFound;
        private readonly XMLSchedulingDataProcessorPlugin plugin;

        public JobFile(XMLSchedulingDataProcessorPlugin plugin, string fileName)
        {
            this.plugin = plugin;
            this.fileName = fileName;
            Initialize();
        }

        public string FileName
        {
            get { return fileName; }
        }

        public bool FileFound
        {
            get { return fileFound; }
        }

        public string FilePath
        {
            get { return filePath; }
        }

        public string FileBasename
        {
            get { return fileBasename; }
        }

        public void Initialize()
        {
            Stream f = null;
            try
            {
                string furl = null;

                string fName = FileName;
                
                // check for special lookup
                fName = FileUtil.ResolveFile(fName);

                FileInfo file = new FileInfo(fName); // files in filesystem
                if (!file.Exists)
                {
                    Uri url = plugin.typeLoadHelper.GetResource(FileName);
                    if (url != null)
                    {
#if !ClientProfile
                        furl = HttpUtility.UrlDecode(url.AbsolutePath);
#else
                        furl = url.AbsolutePath;
#endif
                        file = new FileInfo(furl);
                        try
                        {
                            f = WebRequest.Create(url).GetResponse().GetResponseStream();
                        }
                        catch (IOException)
                        {
                            // Swallow the exception
                        }
                    }
                }
                else
                {
                    try
                    {
                        f = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
                    }
                    catch (FileNotFoundException)
                    {
                        // ignore
                    }
                }

                if (f == null)
                {
                    if (plugin.FailOnFileNotFound)
                    {
                        throw new SchedulerException(
                            "File named '" + FileName + "' does not exist.");
                    }
                    else
                    {
                        plugin.Log.Warn(string.Format(CultureInfo.InvariantCulture, "File named '{0}' does not exist.", FileName));
                    }
                }
                else
                {
                    fileFound = true;
                    filePath = furl ?? file.FullName;
                    fileBasename = file.Name;
                }
            }
            finally
            {
                try
                {
                    if (f != null)
                    {
                        f.Close();
                    }
                }
                catch (IOException ioe)
                {
                    plugin.Log.Warn("Error closing jobs file " + FileName, ioe);
                }
            }
        }
    }

    }
}