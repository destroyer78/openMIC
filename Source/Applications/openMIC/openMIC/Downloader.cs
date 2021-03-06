﻿//******************************************************************************************************
//  Downloader.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  12/08/2015 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotRas;
using GSF;
using GSF.Configuration;
using GSF.Data;
using GSF.Data.Model;
using GSF.Diagnostics;
using GSF.IO;
using GSF.Net.Ftp;
using GSF.Net.Smtp;
using GSF.Parsing;
using GSF.Scheduling;
using GSF.Threading;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;
using GSF.TimeSeries.Statistics;
using GSF.Units;
using openMIC.Model;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable MemberCanBePrivate.Local
namespace openMIC
{
    /// <summary>
    /// Adapter that implements remote file download capabilities.
    /// </summary>
    [Description("Downloader: Implements remote file download capabilities")]
    [EditorBrowsable(EditorBrowsableState.Advanced)] // Normally defined as an input device protocol
    public class Downloader : InputAdapterBase
    {
        #region [ Members ]

        // Nested Types

        // Defines connection profile task settings
        private class ConnectionProfileTaskSettings
        {
            private string m_fileExtensions;
            private string[] m_fileSpecs;

            public ConnectionProfileTaskSettings(string name, int id)
            {
                Name = name;
                ID = id;
            }

            public string Name
            {
                get;
            }

            public int ID
            {
                get;
            }

            [ConnectionStringParameter,
            Description("Defines file names or patterns to download."),
            DefaultValue("*.*")]
            public string FileExtensions
            {
                get
                {
                    return m_fileExtensions;
                }
                set
                {
                    m_fileExtensions = value;
                    m_fileSpecs = null;
                }
            }

            public string[] FileSpecs
            {
                get
                {
                    return m_fileSpecs ?? (m_fileSpecs = (m_fileExtensions ?? "*.*").Split(',').Select(pattern => pattern.Trim()).ToArray());
                }
            }

            [ConnectionStringParameter,
            Description("Defines remote path to download files from ."),
            DefaultValue("/")]
            public string RemotePath
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines local path to download files to."),
            DefaultValue("")]
            public string LocalPath
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if remote folders should scanned for matching downloads - file structure will be replicated locally."),
            DefaultValue(false)]
            public bool RecursiveDownload
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if remote files should be deleted after download."),
            DefaultValue(false)]
            public bool DeleteRemoteFilesAfterDownload
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if total remote files to download should be limited by age."),
            DefaultValue(false)]
            public bool LimitRemoteFileDownloadByAge
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if old local files should be deleted."),
            DefaultValue(false)]
            public bool DeleteOldLocalFiles
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if download should be skipped if local file already exists and matches remote."),
            DefaultValue(false)]
            public bool SkipDownloadIfUnchanged
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if existing local files should be overwritten."),
            DefaultValue(false)]
            public bool OverwriteExistingLocalFiles
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if existing local files should be archived before new ones are downloaded."),
            DefaultValue(false)]
            public bool ArchiveExistingFilesBeforeDownload
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if downloaded file timestamps should be synchronized to remote file timestamps."),
            DefaultValue(true)]
            public bool SynchronizeTimestamps
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines external operation application."),
            DefaultValue("")]
            public string ExternalOperation
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines maximum file size to download."),
            DefaultValue(1000)]
            public int MaximumFileSize
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines maximum file count to download."),
            DefaultValue(-1)]
            public int MaximumFileCount
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines directory naming expression."),
            DefaultValue("<YYYY><MM>\\<DeviceFolderName>")]
            public string DirectoryNamingExpression
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines directory authentication user name."),
            DefaultValue("")]
            public string DirectoryAuthUserName
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines directory authentication password."),
            DefaultValue("")]
            public string DirectoryAuthPassword
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Determines if an e-mail should be sent if the downloaded files have been updated."),
            DefaultValue(false)]
            public bool EmailOnFileUpdate
            {
                get;
                set;
            }

            [ConnectionStringParameter,
            Description("Defines the recipient e-mail addresses to use when sending e-mails on file updates."),
            DefaultValue("")]
            public string EmailRecipients
            {
                get;
                set;
            }
        }

        // Define a IDevice implementation for to provide daily reports
        private class DeviceProxy : IDevice
        {
            private readonly Downloader m_parent;

            public DeviceProxy(Downloader parent)
            {
                m_parent = parent;
            }

            // Gets or sets total data quality errors of this <see cref="IDevice"/>.
            public long DataQualityErrors
            {
                get;
                set;
            }

            // Gets or sets total time quality errors of this <see cref="IDevice"/>.
            public long TimeQualityErrors
            {
                get;
                set;
            }

            // Gets or sets total device errors of this <see cref="IDevice"/>.
            public long DeviceErrors
            {
                get;
                set;
            }

            // Gets or sets total measurements received for this <see cref="IDevice"/> - in local context "successful connections" per day.
            public long MeasurementsReceived
            {
                get
                {
                    return m_parent.SuccessfulConnections;
                }
                set
                {
                    // Ignoring updates
                }
            }

            // Gets or sets total measurements expected to have been received for this <see cref="IDevice"/> - in local context "attempted connections" per day.
            public long MeasurementsExpected
            {
                get
                {
                    return m_parent.AttemptedConnections;
                }
                set
                {
                    // Ignoring updates
                }
            }

            // Gets or sets the number of measurements received while this <see cref="IDevice"/> was reporting errors.
            public long MeasurementsWithError
            {
                get;
                set;
            }

            // Gets or sets the number of measurements (per frame) defined for this <see cref="IDevice"/>.
            public long MeasurementsDefined
            {
                get;
                set;
            }
        }

        // Constants
        private const int NormalPriorty = 1;
        private const int HighPriority = 2;

        // Fields
        private readonly RasDialer m_rasDialer;
        private readonly DeviceProxy m_deviceProxy;
        private readonly object m_connectionProfileLock;
        private Device m_deviceRecord;
        private ConnectionProfile m_connectionProfile;
        private ConnectionProfileTaskSettings[] m_connectionProfileTaskSettings;
        private LogicalThreadOperation m_dialUpOperation;
        private LogicalThreadOperation m_ftpOperation;
        private LongSynchronizedOperation m_executeTasks;
        private readonly ICancellationToken m_cancellationToken;
        private int m_overallTasksCompleted;
        private int m_overallTasksCount;
        private long m_startDialUpTime;
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        public Downloader()
        {
            m_rasDialer = new RasDialer();
            m_rasDialer.Error += m_rasDialer_Error;
            m_deviceProxy = new DeviceProxy(this);
            m_cancellationToken = new GSF.Threading.CancellationToken();
            m_connectionProfileLock = new object();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets connection host name or IP for transport.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines connection host name or IP for transport.")]
        public string ConnectionHostName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets connection host user name for transport.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines connection host user name for transport."),
        DefaultValue("anonymous")]
        public string ConnectionUserName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets connection password for transport.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines connection password for transport."),
        DefaultValue("anonymous")]
        public string ConnectionPassword
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets connection timeout for transport.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines connection timeout for transport."),
        DefaultValue(30000)]
        public int ConnectionTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets mode of FTP connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines mode of FTP connection."),
        DefaultValue(true)]
        public bool PassiveFtp
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets mode of FTP connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines IP address to send in FTP PORT command."),
        DefaultValue("")]
        public string ActiveFtpAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets mode of FTP connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines minimum port in active FTP port range."),
        DefaultValue(0)]
        public int MinActiveFtpPort
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets mode of FTP connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines maximum port in active FTP port range."),
        DefaultValue(0)]
        public int MaxActiveFtpPort
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets flag that determines if connection messages should be logged.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines flag that determines if connection messages should be logged."),
        DefaultValue(false)]
        public bool LogConnectionMessages
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets connection profile record ID.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines connection profile record ID."),
        DefaultValue(0)]
        public int ConnectionProfileID
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets download schedule.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines download schedule."),
        DefaultValue("* * * * *")]
        public string Schedule
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets flag that determines if this connection will use dial-up.
        /// </summary>
        [ConnectionStringParameter,
        Description("Determines if this connection will use dial-up."),
        DefaultValue(false)]
        public bool UseDialUp
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets flag that determines if this connection will use logical threads scheduler.
        /// </summary>
        public bool UseLogicalThread => s_ftpThreadCount > 0;

        /// <summary>
        /// Gets or sets dial-up entry name.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines dial-up entry name."),
        DefaultValue("")]
        public string DialUpEntryName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets dial-up phone number.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines dial-up phone number."),
        DefaultValue("")]
        public string DialUpNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets dial-up user name.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines dial-up user name."),
        DefaultValue("")]
        public string DialUpUserName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets dial-up password.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines dial-up password."),
        DefaultValue("")]
        public string DialUpPassword
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets maximum retries for a dial-up connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines maximum retries for a dial-up connection."),
        DefaultValue(3)]
        public int DialUpRetries
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets timeout for a dial-up connection.
        /// </summary>
        [ConnectionStringParameter,
        Description("Defines timeout for a dial-up connection."),
        DefaultValue(90)]
        public int DialUpTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of attempted connections.
        /// </summary>
        public long AttemptedConnections
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of successful connections.
        /// </summary>
        public long SuccessfulConnections
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of failed connections.
        /// </summary>
        public long FailedConnections
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of processed files.
        /// </summary>
        public long TotalProcessedFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of attempted dial-ups.
        /// </summary>
        public long AttemptedDialUps
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of successful dial-ups.
        /// </summary>
        public long SuccessfulDialUps
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of failed dial-ups.
        /// </summary>
        public long FailedDialUps
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of files downloaded.
        /// </summary>
        public long FilesDownloaded
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total number of bytes downloaded.
        /// </summary>
        public long BytesDownloaded
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total connected time, in ticks.
        /// </summary>
        public long TotalConnectedTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets total dial-up time, in ticks.
        /// </summary>
        public long TotalDialUpTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets <see cref="DataSet" /> based data source available to this <see cref="AdapterBase" />.
        /// </summary>
        public override DataSet DataSource
        {
            get
            {
                return base.DataSource;
            }

            set
            {
                base.DataSource = value;

                // ReloadConfig was requested, take this opportunity to reload connection profile tasks...
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        LoadTasks();
                    }
                    catch (Exception ex)
                    {
                        OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to reload connection profile tasks: {ex.Message}", ex));
                    }
                });
            }
        }

        /// <summary>
        /// Gets flag that determines if the data input connects asynchronously.
        /// </summary>
        /// <remarks>
        /// Derived classes should return true when data input source is connects asynchronously, otherwise return false.
        /// </remarks>
        protected override bool UseAsyncConnect => false;

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        public override bool SupportsTemporalProcessing => false;

        /// <summary>
        /// Returns the detailed status of the data input source.
        /// </summary>
        public override string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();

                status.Append(base.Status);
                status.AppendFormat("      Connection host name: {0}", ConnectionHostName.ToNonNullNorWhiteSpace("undefined"));
                status.AppendLine();
                status.AppendFormat("      Connection user name: {0} - with {1} password", ConnectionUserName.ToNonNullNorWhiteSpace("undefined"), string.IsNullOrWhiteSpace(ConnectionPassword) ? "no" : "a");
                status.AppendLine();
                status.AppendFormat("     Connection profile ID: {0} - {1}", ConnectionProfileID, m_connectionProfile?.Name ?? "undefined");
                status.AppendLine();
                status.AppendFormat("         Download schedule: {0}", Schedule);
                status.AppendLine();
                status.AppendFormat("   Log connection messages: {0}", LogConnectionMessages);
                status.AppendLine();
                status.AppendFormat("     Attempted connections: {0}", AttemptedConnections);
                status.AppendLine();
                status.AppendFormat("    Successful connections: {0}", SuccessfulConnections);
                status.AppendLine();
                status.AppendFormat("        Failed connections: {0}", FailedConnections);
                status.AppendLine();
                status.AppendFormat("     Total processed files: {0}", TotalProcessedFiles);
                status.AppendLine();
                status.AppendFormat("      Total connected time: {0}", new Ticks(TotalConnectedTime).ToElapsedTimeString(3));
                status.AppendLine();
                status.AppendFormat("               Use dial-up: {0}", UseDialUp);
                status.AppendLine();

                if (UseDialUp)
                {
                    status.AppendFormat("        Dial-up entry name: {0}", DialUpEntryName);
                    status.AppendLine();
                    status.AppendFormat("            Dial-up number: {0}", DialUpNumber);
                    status.AppendLine();
                    status.AppendFormat("         Dial-up user name: {0} - with {1} password", DialUpUserName.ToNonNullNorWhiteSpace("undefined"), string.IsNullOrWhiteSpace(DialUpPassword) ? "no" : "a");
                    status.AppendLine();
                    status.AppendFormat("           Dial-up retries: {0}", DialUpRetries);
                    status.AppendLine();
                    status.AppendFormat("          Dial-up time-out: {0}", DialUpTimeout);
                    status.AppendLine();
                    status.AppendFormat("        Attempted dial-ups: {0}", AttemptedDialUps);
                    status.AppendLine();
                    status.AppendFormat("       Successful dial-ups: {0}", SuccessfulDialUps);
                    status.AppendLine();
                    status.AppendFormat("           Failed dial-ups: {0}", FailedDialUps);
                    status.AppendLine();
                    status.AppendFormat("        Total dial-up time: {0}", new Ticks(TotalDialUpTime).ToElapsedTimeString(3));
                    status.AppendLine();
                }

                status.AppendFormat(" Connection profiles tasks: {0}", m_connectionProfileTaskSettings.Length);
                status.AppendLine();
                status.AppendFormat("          Files downloaded: {0}", FilesDownloaded);
                status.AppendLine();
                status.AppendFormat("          Bytes downloaded: {0:N3} MB", BytesDownloaded / (double)SI2.Mega);
                status.AppendLine();

                return status.ToString();
            }
        }


        // Gets RAS connection state
        private RasConnectionState RasState => RasConnection.GetActiveConnections().FirstOrDefault(ras => ras.EntryName == DialUpEntryName)?.GetConnectionStatus()?.ConnectionState ?? RasConnectionState.Disconnected;

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="Downloader"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        m_cancellationToken.Cancel();
                        DeregisterSchedule(this);

                        if ((object)m_rasDialer != null)
                        {
                            m_rasDialer.Error -= m_rasDialer_Error;
                            m_rasDialer.Dispose();
                        }

                        StatisticsEngine.Unregister(m_deviceProxy);
                        StatisticsEngine.Unregister(this);
                    }
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose(disposing);    // Call base class Dispose().
                }
            }
        }

        /// <summary>
        /// Initializes <see cref="Downloader"/>.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            ConnectionStringParser<ConnectionStringParameterAttribute> parser = new ConnectionStringParser<ConnectionStringParameterAttribute>();

            parser.ParseConnectionString(ConnectionString, this);

            LoadTasks();
            RegisterSchedule(this);

            // Register downloader with the statistics engine
            StatisticsEngine.Register(this, "Downloader", "DLR");
            StatisticsEngine.Register(m_deviceProxy, Name, "Device", "PMU");
        }

        /// <summary>
        /// Attempts to connect to data input source.
        /// </summary>
        protected override void AttemptConnection()
        {
            ConnectionProfileTaskSettings[] taskSettings;

            lock (m_connectionProfileLock)
                taskSettings = m_connectionProfileTaskSettings;

            foreach (ConnectionProfileTaskSettings settings in taskSettings)
            {
                string localPath = settings.LocalPath.ToNonNullString().Trim();

                if (localPath.StartsWith(@"\\") && !string.IsNullOrWhiteSpace(settings.DirectoryAuthUserName) && !string.IsNullOrWhiteSpace(settings.DirectoryAuthPassword))
                {
                    string[] userParts = settings.DirectoryAuthUserName.Split('\\');

                    try
                    {
                        if (userParts.Length == 2)
                            FilePath.ConnectToNetworkShare(localPath.Trim(), userParts[1].Trim(), settings.DirectoryAuthPassword.Trim(), userParts[0].Trim());
                        else
                            throw new InvalidOperationException($"UNC based local path \"{settings.LocalPath}\" or authentication user name \"{settings.DirectoryAuthUserName}\" is not in the correct format.");
                    }
                    catch (Exception ex)
                    {
                        OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Exception while authenticating UNC path \"{settings.LocalPath}\": {ex.Message}", ex));
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to disconnect from data input source.
        /// </summary>
        protected override void AttemptDisconnection()
        {
            // Just leaving UNC paths authenticated for the duration of service run-time since multiple
            // devices may share the same root destination path
        }

        /// <summary>
        /// Gets a short one-line status of this adapter.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>
        /// A short one-line summary of the current status of this adapter.
        /// </returns>
        public override string GetShortStatus(int maxLength)
        {
            if (!Enabled)
                return "Downloading for is paused...".CenterText(maxLength);

            return $"Downloading enabled for schedule: {Schedule}".CenterText(maxLength);
        }

        /// <summary>
        /// Queues scheduled tasks for immediate execution.
        /// </summary>
        [AdapterCommand("Queues scheduled tasks for immediate execution.", "Administrator", "Editor")]
        public void QueueTasks()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                AttemptedConnections++;

                if (UseDialUp)
                {
                    m_dialUpOperation.Priority = HighPriority;
                    m_dialUpOperation.RunOnce();
                    m_dialUpOperation.Priority = NormalPriorty;
                }
                else if (UseLogicalThread)
                {
                    m_ftpOperation.RunOnce();
                }
                else { 
                    m_executeTasks.RunOnce();
                }
            });
        }

        private void LoadTasks()
        {
            ConnectionStringParser<ConnectionStringParameterAttribute> parser = new ConnectionStringParser<ConnectionStringParameterAttribute>();

            using (AdoDataConnection connection = new AdoDataConnection("systemSettings"))
            {
                TableOperations<Device> deviceTable = new TableOperations<Device>(connection);
                TableOperations<ConnectionProfile> connectionProfileTable = new TableOperations<ConnectionProfile>(connection);
                TableOperations<ConnectionProfileTask> connectionProfileTaskTable = new TableOperations<ConnectionProfileTask>(connection);

                lock (m_connectionProfileLock)
                {
                    m_deviceRecord = deviceTable.QueryRecord("Acronym = {0}", Name);
                    m_connectionProfile = connectionProfileTable.LoadRecord(ConnectionProfileID);
                    IEnumerable<ConnectionProfileTask> tasks = connectionProfileTaskTable.QueryRecords(restriction: new RecordRestriction("ConnectionProfileID={0}", ConnectionProfileID));
                    List<ConnectionProfileTaskSettings> connectionProfileTaskSettings = new List<ConnectionProfileTaskSettings>();

                    foreach (ConnectionProfileTask task in tasks)
                    {
                        ConnectionProfileTaskSettings settings = new ConnectionProfileTaskSettings(task.Name, task.ID);
                        parser.ParseConnectionString(task.Settings, settings);
                        connectionProfileTaskSettings.Add(settings);
                    }

                    m_connectionProfileTaskSettings = connectionProfileTaskSettings.ToArray();
                }
            }
        }

        private void ExecuteTasks()
        {
            if (m_cancellationToken.IsCancelled)
                return;

            using (FtpClient client = new FtpClient())
            {
                client.CommandSent += FtpClient_CommandSent;
                client.ResponseReceived += FtpClient_ResponseReceived;
                client.FileTransferProgress += FtpClient_FileTransferProgress;
                client.FileTransferNotification += FtpClient_FileTransferNotification;

                try
                {
                    if (string.IsNullOrWhiteSpace(ConnectionHostName))
                    {
                        OnStatusMessage(MessageLevel.Warning, "No connection host name provided, skipping connection to FTP server...");
                    }
                    else
                    {
                        OnStatusMessage(MessageLevel.Info, $"Attempting connection to FTP server \"{ConnectionUserName}@{ConnectionHostName}\"...");

                        string[] parts = ConnectionHostName.Split(':');

                        if (parts.Length > 1)
                        {
                            client.Server = parts[0];
                            client.Port = int.Parse(parts[1]);
                        }
                        else
                        {
                            client.Server = ConnectionHostName;
                        }

                        client.Timeout = ConnectionTimeout;
                        client.Passive = PassiveFtp;
                        client.ActiveAddress = ActiveFtpAddress;
                        client.MinActivePort = MinActiveFtpPort;
                        client.MaxActivePort = MaxActiveFtpPort;
                        client.Connect(ConnectionUserName, ConnectionPassword);

                        OnStatusMessage(MessageLevel.Info, $"Connected to FTP server \"{ConnectionUserName}@{ConnectionHostName}\"");
                    }

                    Ticks connectionStartTime = DateTime.UtcNow.Ticks;
                    SuccessfulConnections++;

                    string connectionProfileName = m_connectionProfile?.Name ?? "Undefined";
                    ConnectionProfileTaskSettings[] taskSettings;

                    lock (m_connectionProfileLock)
                        taskSettings = m_connectionProfileTaskSettings;

                    if (taskSettings.Length > 0)
                    {
                        m_overallTasksCompleted = 0;
                        m_overallTasksCount = taskSettings.Length;
                        OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, true, $"Starting \"{connectionProfileName}\" connection profile processing...", m_overallTasksCompleted, m_overallTasksCount));

                        foreach (ConnectionProfileTaskSettings settings in taskSettings)
                        {
                            OnStatusMessage(MessageLevel.Info, $"Starting \"{connectionProfileName}\" connection profile \"{settings.Name}\" task processing:");

                            if (string.IsNullOrWhiteSpace(settings.ExternalOperation))
                                ProcessFTPTask(settings, client);
                            else
                                ProcessExternalOperationTask(settings);

                            // Handle local file age limit processing, if enabled
                            if (settings.DeleteOldLocalFiles)
                                HandleLocalFileAgeLimitProcessing(settings);

                            OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, true, null, ++m_overallTasksCompleted, m_overallTasksCount));
                        }

                        OnProgressUpdated(this, new ProgressUpdate(ProgressState.Succeeded, true, $"Completed \"{connectionProfileName}\" connection profile processing.", m_overallTasksCount, m_overallTasksCount));
                    }
                    else
                    {
                        OnProgressUpdated(this, new ProgressUpdate(ProgressState.Skipped, true, $"Skipped \"{connectionProfileName}\" connection profile processing: No tasks defined.", 0, 1));
                    }

                    Ticks connectedTime = DateTime.UtcNow.Ticks - connectionStartTime;
                    OnStatusMessage(MessageLevel.Info, $"FTP session connected for {connectedTime.ToElapsedTimeString(2)}");
                    TotalConnectedTime += connectedTime;
                }
                catch (Exception ex)
                {
                    FailedConnections++;
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to connect to FTP server \"{ConnectionUserName}@{ConnectionHostName}\": {ex.Message}", ex));
                    OnProgressUpdated(this, new ProgressUpdate(ProgressState.Failed, true, $"Failed to connect to FTP server \"{ConnectionUserName}@{ConnectionHostName}\": {ex.Message}", 0, 1));
                    UpdateStatusLogDatabase(null, "", false, ex.Message);
                }

                client.CommandSent -= FtpClient_CommandSent;
                client.ResponseReceived -= FtpClient_ResponseReceived;
                client.FileTransferProgress -= FtpClient_FileTransferProgress;
                client.FileTransferNotification -= FtpClient_FileTransferNotification;
            }
        }

        private void ProcessFTPTask(ConnectionProfileTaskSettings settings, FtpClient client)
        {
            string remotePath = GetRemotePathName(settings);
            string localSubPath = GetLocalPathName(settings,Path.DirectorySeparatorChar.ToString());

            ProcessFTPTask(settings, client, remotePath, localSubPath);

            if (settings.RecursiveDownload)
                ProcessFTPSubDirectories(settings, client, remotePath, localSubPath);
        }

        private void ProcessFTPSubDirectories(ConnectionProfileTaskSettings settings, FtpClient client, string rootRemotePath, string rootLocalSubPath)
        {        
            client.SetCurrentDirectory(rootRemotePath);

            FtpDirectory[] directories = client.CurrentDirectory.SubDirectories.ToArray();

            foreach (FtpDirectory directory in directories)
            {
                if (m_cancellationToken.IsCancelled)
                    return;

                string directoryName = directory.Name;

                if (directoryName.StartsWith(".", StringComparison.Ordinal))
                    continue;

                string remotePath = $"{rootRemotePath}/{directoryName}";
                string localSubPath = GetLocalPathName(settings,Path.Combine(rootLocalSubPath, directoryName));

                ProcessFTPTask(settings, client, remotePath, localSubPath);
                ProcessFTPSubDirectories(settings, client, remotePath, localSubPath);
            }
        }

        private void ProcessFTPTask(ConnectionProfileTaskSettings settings, FtpClient client, string remotePath, string localSubPath)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Cannot process connection profile task \"{settings.Name}\", remote FTP directory path is undefined."));
                return;
            }

            OnStatusMessage(MessageLevel.Info, $"Attempting to set remote FTP directory path \"{remotePath}\"...");

            try
            {
                client.SetCurrentDirectory(remotePath);

                OnStatusMessage(MessageLevel.Info, $"Enumerating remote files in \"{remotePath}\"...");

                try
                {
                    FtpFile[] files = client.CurrentDirectory.Files.ToArray();

                    if (files.Length == 0)
                    {
                        OnStatusMessage(MessageLevel.Info, "No remote files found, remote file processing terminated.");
                    }
                    else if (files.Length > settings.MaximumFileCount && settings.MaximumFileCount > -1)
                    {
                        OnStatusMessage(MessageLevel.Warning, $"Skipping remote file processing, there are {files.Length} remote files which exceeds the set {settings.MaximumFileCount} file limit.");
                    }
                    else
                    {
                        OnStatusMessage(MessageLevel.Info, $"Found {files.Length} remote file{(files.Length > 1 ? "s" : "")}, starting file processing...");

                        m_overallTasksCount += files.Length;
                        OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, true, null, m_overallTasksCompleted, m_overallTasksCount));

                        foreach (FtpFile file in files)
                        {
                            try
                            {
                                if (m_cancellationToken.IsCancelled)
                                    return;

                                if (!FilePath.IsFilePatternMatch(settings.FileSpecs, file.Name, true))
                                    continue;

                                OnStatusMessage(MessageLevel.Info, $"Processing remote file \"{file.Name}\"...");
                                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, false, $"Starting \"{file.Name}\" download...", 0, file.Size));

                                try
                                {
                                    ProcessFile(settings, localSubPath, file);
                                    TotalProcessedFiles++;
                                }
                                catch (Exception ex)
                                {
                                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to process remote file \"{file.Name ?? "undefined"}\" in \"{remotePath}\": {ex.Message}", ex));
                                }
                            }
                            finally
                            {
                                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, true, null, ++m_overallTasksCompleted, m_overallTasksCount));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to enumerate remote files in \"{remotePath}\": {ex.Message}", ex));
                }
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to set remote FTP directory path \"{remotePath}\": {ex.Message}", ex));
            }
        }

        private void ProcessFile(ConnectionProfileTaskSettings settings, string localSubPath, FtpFile file)
        {
            if (settings.LimitRemoteFileDownloadByAge && (DateTime.Now - file.Timestamp).Days > Program.Host.Model.Global.MaxRemoteFileAge)
            {
                OnStatusMessage(MessageLevel.Info, $"File \"{file.Name}\" skipped, timestamp \"{file.Timestamp:yyyy-MM-dd HH:mm.ss.fff}\" is older than {Program.Host.Model.Global.MaxRemoteFileAge} days.");
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Skipped, false, $"File \"{file.Name}\" skipped: File is too old.", 0, file.Size));
                return;
            }

            if (file.Size > settings.MaximumFileSize * SI2.Mega)
            {
                OnStatusMessage(MessageLevel.Info, $"File \"{file.Name}\" skipped, size of {file.Size / SI2.Mega:N3} MB is larger than {settings.MaximumFileSize:N3} MB configured limit.");
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Skipped, false, $"File \"{file.Name}\" skipped: File is too large ({file.Size / (double)SI2.Mega:N3} MB).", 0, file.Size));
                return;
            }

            if (DownloadFile(settings, localSubPath, file) && settings.DeleteRemoteFilesAfterDownload)
            {
                try
                {
                    file.Parent.RemoveFile(file.Name);
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to remove \"{file.Name}\" after download: {ex.Message}", ex));
                }
            }
        }

        private bool DownloadFile(ConnectionProfileTaskSettings settings, string localSubPath, FtpFile file)
        {
            if (string.IsNullOrWhiteSpace(settings.LocalPath) || !Directory.Exists(settings.LocalPath))
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Cannot download file \"{file.Name}\" for connection profile task \"{settings.Name}\": Local path \"{settings.LocalPath ?? ""}\" does not exist."));
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Failed, false, $"Cannot download file \"{file.Name}\": Local path does not exists", 0, file.Size));
                return false;
            }

            string localFileName = Path.Combine(localSubPath, file.Name);
            bool fileChanged = false;

            if (File.Exists(localFileName) && settings.SkipDownloadIfUnchanged)
            {
                try
                {
                    FileInfo info = new FileInfo(localFileName);

                    // Compare file sizes
                    bool localEqualsRemote = info.Length == file.Size;

                    if (localEqualsRemote)
                    {
                        // Compare timestamps, if synchronized
                        if (settings.SynchronizeTimestamps)
                            localEqualsRemote = info.LastWriteTime == file.Timestamp;

                        if (localEqualsRemote)
                        {
                            OnStatusMessage(MessageLevel.Info, $"Skipping file \"{file.Name}\" download for connection profile task \"{settings.Name}\": Local file already exists and matches remote file.");
                            OnProgressUpdated(this, new ProgressUpdate(ProgressState.Skipped, false, $"File \"{file.Name}\" skipped: Local file already exists and matches remote file", 0, file.Size));
                            return false;
                        }
                    }

                    fileChanged = true;
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to get info for local file \"{localFileName}\" for connection profile task \"{settings.Name}\": {ex.Message}", ex));
                }
            }

            if (File.Exists(localFileName) && settings.ArchiveExistingFilesBeforeDownload)
            {
                try
                {
                    string directoryName = Path.Combine(FilePath.GetDirectoryName(localFileName), "Archive\\");
                    string archiveFileName = Path.Combine(directoryName, FilePath.GetFileName(localFileName));

                    Directory.CreateDirectory(directoryName);

                    if (File.Exists(archiveFileName))
                        archiveFileName = FilePath.GetUniqueFilePathWithBinarySearch(archiveFileName);

                    OnStatusMessage(MessageLevel.Info, $"Archiving existing file \"{localFileName}\" to \"{archiveFileName}\"...");
                    File.Move(localFileName, archiveFileName);
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to archive existing local file \"{localFileName}\" before download for connection profile task \"{settings.Name}\": {ex.Message}", ex));
                }
            }

            if (File.Exists(localFileName) && !settings.OverwriteExistingLocalFiles)
            {
                OnStatusMessage(MessageLevel.Info, $"Skipping file \"{file.Name}\" download for connection profile task \"{settings.Name}\": Local file already exists and settings do not allow overwrite.");
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Skipped, false, $"File \"{file.Name}\" skipped: Local file already exists", 0, file.Size));
                return false;
            }

            OnStatusMessage(MessageLevel.Info, $"Downloading \"{file.Name}\" to \"{localFileName}\"...");

            try
            {
                file.Parent.GetFile(localFileName, file.Name);
                FilesDownloaded++;
                BytesDownloaded += file.Size;
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Succeeded, false, $"Download complete for \"{file.Name}\".", file.Size, file.Size));
                UpdateStatusLogDatabase(file, localFileName, true);

                // Send e-mail on file update, if requested
                if (fileChanged && settings.EmailOnFileUpdate)
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            GlobalSettings global = Program.Host.Model.Global;
                            string subject = $"File changed for \"{Name}: {settings.Name}\"";
                            string body = $"<b>File Name = {localFileName}</b></br>";

                            if (string.IsNullOrWhiteSpace(global.SmtpUserName))
                                Mail.Send(global.FromAddress, settings.EmailRecipients, subject, body, true, global.SmtpServer);
                            else
                                Mail.Send(global.FromAddress, settings.EmailRecipients, subject, body, true, global.SmtpServer, global.SmtpUserName, global.SmtpPassword);
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to send e-mail notification about updated file \"{localFileName}\": {ex.Message}"));
                        }
                    });
                }

                // Synchronize local timestamp to that of remote file if requested
                if (settings.SynchronizeTimestamps)
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            FileInfo info = new FileInfo(localFileName);
                            info.LastAccessTime = info.LastWriteTime = file.Timestamp;
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to update timestamp of downloaded file \"{localFileName}\": {ex.Message}"));
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to download file \"{file.Name}\" for connection profile task \"{settings.Name}\" to \"{localFileName}\": {ex.Message}", ex));
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Failed, false, $"Failed to download file \"{file.Name}\": {ex.Message}", 0, file.Size));
            }

            return false;
        }

        private string GetLocalFileName(ConnectionProfileTaskSettings settings, string localSubPath, string fileName)
        {
            TemplatedExpressionParser directoryNameExpressionParser = new TemplatedExpressionParser('<', '>', '[', ']');
            Dictionary<string, string> substitutions = new Dictionary<string, string>
            {
                { "<YYYY>", $"{DateTime.Now.Year}" },
                { "<YY>", $"{DateTime.Now.Year.ToString().Substring(2)}" },
                { "<MM>", $"{DateTime.Now.Month.ToString().PadLeft(2, '0')}" },
                { "<DD>", $"{DateTime.Now.Day.ToString().PadLeft(2, '0')}" },
                { "<DeviceName>", m_deviceRecord.Name ?? "undefined" },
                { "<DeviceAcronym>", m_deviceRecord.Acronym },
                { "<DeviceFolderName>", m_deviceRecord.OriginalSource ?? m_deviceRecord.Acronym },
                { "<ProfileName>", m_connectionProfile.Name ?? "undefined" }
            };

            directoryNameExpressionParser.TemplatedExpression = settings.DirectoryNamingExpression.Replace("\\", "\\\\");

            //         Possible UNC Path                            Sub Directory - duplicate path slashes are removed
            fileName = FilePath.AddPathSuffix(settings.LocalPath) + $"{directoryNameExpressionParser.Execute(substitutions)}{Path.DirectorySeparatorChar}{localSubPath}{Path.DirectorySeparatorChar}{fileName}".RemoveDuplicates(Path.DirectorySeparatorChar.ToString());

            string directoryName = FilePath.GetDirectoryName(fileName);

            if (!Directory.Exists(directoryName))
            {
                try
                {
                    Directory.CreateDirectory(directoryName);
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to create directory \"{directoryName}\": {ex.Message}", ex));
                }
            }

            return fileName;
        }

        private string GetLocalPathName(ConnectionProfileTaskSettings settings, string localSubPath)
        {
            TemplatedExpressionParser directoryNameExpressionParser = new TemplatedExpressionParser('<', '>', '[', ']');
            Dictionary<string, string> substitutions = new Dictionary<string, string>
            {
                { "<YYYY>", $"{DateTime.Now.Year}" },
                { "<YY>", $"{DateTime.Now.Year.ToString().Substring(2)}" },
                { "<MM>", $"{DateTime.Now.Month.ToString().PadLeft(2, '0')}" },
                { "<DD>", $"{DateTime.Now.Day.ToString().PadLeft(2, '0')}" },
                { "<DeviceName>", m_deviceRecord.Name ?? "undefined" },
                { "<DeviceAcronym>", m_deviceRecord.Acronym },
                { "<DeviceFolderName>", m_deviceRecord.OriginalSource ?? m_deviceRecord.Acronym },
                { "<ProfileName>", m_connectionProfile.Name ?? "undefined" }
            };

            directoryNameExpressionParser.TemplatedExpression = settings.DirectoryNamingExpression.Replace("\\", "\\\\");

            // Possible UNC path and sub-directory - duplicate path slashes are removed
            string directoryName = FilePath.AddPathSuffix(settings.LocalPath) + $"{directoryNameExpressionParser.Execute(substitutions)}{Path.DirectorySeparatorChar}{localSubPath}".RemoveDuplicates(Path.DirectorySeparatorChar.ToString());

            if (!Directory.Exists(directoryName))
            {
                try
                {
                    Directory.CreateDirectory(directoryName);
                }
                catch (Exception ex)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to create directory \"{directoryName}\": {ex.Message}", ex));
                }
            }

            return directoryName;
        }

        private string GetRemotePathName(ConnectionProfileTaskSettings settings)
        {
            TemplatedExpressionParser directoryNameExpressionParser = new TemplatedExpressionParser('<', '>', '[', ']');
            Dictionary<string, string> substitutions = new Dictionary<string, string>
            {
                { "<YYYY>", $"{DateTime.Now.Year}" },
                { "<YY>", $"{DateTime.Now.Year.ToString().Substring(2)}" },
                { "<MM>", $"{DateTime.Now.Month.ToString().PadLeft(2, '0')}" },
                { "<DD>", $"{DateTime.Now.Day.ToString().PadLeft(2, '0')}" },
                { "<Month MM>", $"Month {DateTime.Now.Month.ToString().PadLeft(2, '0')}" },
                { "<Day DD>", $"Day {DateTime.Now.Day.ToString().PadLeft(2, '0')}" },
                { "<Day DD-1>", $"Day {DateTime.Now.AddDays(-1).Day.ToString().PadLeft(2, '0')}" },
                { "<DeviceName>", m_deviceRecord.Name ?? "undefined" },
                { "<DeviceAcronym>", m_deviceRecord.Acronym },
                { "<DeviceFolderName>", m_deviceRecord.OriginalSource ?? m_deviceRecord.Acronym },
                { "<ProfileName>", m_connectionProfile.Name ?? "undefined" }
            };

            if(settings.RemotePath.Contains("<Day DD-1>"))
            {
                substitutions["<YYYY>"] = $"{DateTime.Now.AddDays(-1).Year}";
                substitutions["<YY>"] = $"{DateTime.Now.AddDays(-1).Year.ToString().Substring(2)}";
                substitutions["<MM>"] = $"{DateTime.Now.AddDays(-1).Month.ToString().PadLeft(2, '0')}";
                substitutions["<Month MM>"] = $"Month {DateTime.Now.AddDays(-1).Month.ToString().PadLeft(2, '0')}";
            }

            directoryNameExpressionParser.TemplatedExpression = settings.RemotePath;

            string directoryName = directoryNameExpressionParser.Execute(substitutions);

            return directoryName;
        }


        private void ProcessExternalOperationTask(ConnectionProfileTaskSettings settings)
        {
            string externalOperationExecutableName = FilePath.GetAbsolutePath(settings.ExternalOperation);

            if (!File.Exists(externalOperationExecutableName))
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Cannot execute external operation \"{settings.ExternalOperation}\" for connection profile task \"{settings.Name}\": Executable file not found."));
                return;
            }

            OnStatusMessage(MessageLevel.Info, $"Executing external operation \"{settings.ExternalOperation}\"...");
            OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, false, "Starting external action...", 1, 0));

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(externalOperationExecutableName, $"{m_deviceRecord.ID} {settings.ID}");
                startInfo.UseShellExecute = false;
                Process externalOperation = Process.Start(startInfo);

                if ((object)externalOperation == null)
                {
                    OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to start external operation \"{settings.ExternalOperation}\"."));
                    OnProgressUpdated(this, new ProgressUpdate(ProgressState.Failed, false, "Failed to start external action.", 1, 0));
                }
                else
                {
                    while (!externalOperation.WaitForExit(1000))
                    {
                        if (m_cancellationToken.IsCancelled)
                        {
                            TerminateProcessTree(externalOperation.Id);
                            return;
                        }
                    }

                    OnStatusMessage(MessageLevel.Info, $"External operation \"{settings.ExternalOperation}\" completed with status code {externalOperation.ExitCode}.");
                    OnProgressUpdated(this, new ProgressUpdate(ProgressState.Undefined, false, $"External action complete: exit code {externalOperation.ExitCode}.", 1, 1));
                    FilesDownloaded++;
                }
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to execute external operation \"{settings.ExternalOperation}\": {ex.Message}", ex));
                OnProgressUpdated(this, new ProgressUpdate(ProgressState.Failed, false, $"Failed to execute external action: {ex.Message}", 1, 0));
            }
        }

        private void HandleLocalFileAgeLimitProcessing(ConnectionProfileTaskSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.LocalPath) || !Directory.Exists(settings.LocalPath))
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Cannot handle local file age limit processing for connection profile task \"{settings.Name}\": Local path \"{settings.LocalPath ?? ""}\" does not exist."));
                return;
            }

            OnStatusMessage(MessageLevel.Info, $"Enumerating local files in \"{settings.LocalPath}\"...");

            try
            {
                string[] files = FilePath.GetFileList(Path.Combine(FilePath.GetAbsolutePath(settings.LocalPath), "*\\*.*"));
                long deletedCount = 0;

                OnStatusMessage(MessageLevel.Info, $"Found {files.Length} local files, starting age limit processing...");

                foreach (string file in files)
                {
                    // Check file specification restriction
                    if (!FilePath.IsFilePatternMatch(settings.FileSpecs, file, true))
                        continue;

                    DateTime creationTime = File.GetCreationTime(file);

                    if ((DateTime.Now - creationTime).Days > Program.Host.Model.Global.MaxLocalFileAge)
                    {
                        OnStatusMessage(MessageLevel.Info, $"Attempting to delete file \"{file}\" created at \"{creationTime:yyyy-MM-dd HH:mm.ss.fff}\"...");

                        try
                        {
                            string rootPathName = FilePath.GetDirectoryName(settings.LocalPath);
                            string directoryName = FilePath.GetDirectoryName(file);

                            FilePath.WaitForWriteLock(file);
                            File.Delete(file);
                            deletedCount++;
                            OnStatusMessage(MessageLevel.Info, $"File \"{file}\" successfully deleted...");

                            if (!directoryName.Equals(rootPathName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Try to remove sub-folder, this will only succeed if folder is empty...
                                try
                                {
                                    Directory.Delete(directoryName);
                                    OnStatusMessage(MessageLevel.Info, $"Removed empty folder \"{directoryName}\"...");
                                }
                                catch
                                {
                                    // Failure is common case, nothing to report
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to delete file \"{file}\": {ex.Message}", ex));
                        }
                    }
                }

                if (deletedCount > 0)
                    OnStatusMessage(MessageLevel.Info, $"Deleted {deletedCount} files during local file age limit processing.");
                else
                    OnStatusMessage(MessageLevel.Info, "No files deleted during local file age limit processing.");
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to enumerate local files in \"{settings.LocalPath}\": {ex.Message}", ex));
            }
        }

        private void FtpClient_CommandSent(object sender, EventArgs<string> e)
        {
            if (LogConnectionMessages)
                OnStatusMessage(MessageLevel.Info, $"FTP Request: {e.Argument}");
        }

        private void FtpClient_ResponseReceived(object sender, EventArgs<string> e)
        {
            if (LogConnectionMessages)
                OnStatusMessage(MessageLevel.Info, $"FTP Response: {e.Argument}");
        }

        private void FtpClient_FileTransferProgress(object sender, EventArgs<ProcessProgress<long>, TransferDirection> e)
        {
            ProcessProgress<long> progress = e.Argument1;
            OnProgressUpdated(this, new ProgressUpdate(ProgressState.Processing, false, progress.ProgressMessage, progress.Complete, progress.Total));
        }

        private void FtpClient_FileTransferNotification(object sender, EventArgs<FtpAsyncResult> e)
        {
            OnStatusMessage(MessageLevel.Info, $"FTP File Transfer: {e.Argument.Message}, response code = {e.Argument.ResponseCode}");
        }

        private bool ConnectDialUp()
        {
            if (!UseDialUp)
                return false;

            m_startDialUpTime = 0;
            DisconnectDialUp();

            try
            {
                if (RasState == RasConnectionState.Connected)
                    throw new InvalidOperationException($"Cannot connect to \"{DialUpEntryName}\": already connected.");

                OnStatusMessage(MessageLevel.Info, $"Initiating dial-up for \"{DialUpEntryName}\"...");
                AttemptedDialUps++;

                m_rasDialer.EntryName = DialUpEntryName;
                m_rasDialer.PhoneNumber = DialUpPassword;
                m_rasDialer.Timeout = DialUpTimeout;
                m_rasDialer.Credentials = new NetworkCredential(DialUpUserName, DialUpPassword);
                m_rasDialer.Dial();

                m_startDialUpTime = DateTime.UtcNow.Ticks;
                SuccessfulDialUps++;
                OnStatusMessage(MessageLevel.Info, $"Dial-up connected on \"{DialUpEntryName}\"");
                return true;
            }
            catch (Exception ex)
            {
                FailedDialUps++;
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Exception while attempting to dial entry \"{DialUpEntryName}\": {ex.Message}", ex));
                DisconnectDialUp();
            }

            return false;
        }

        private void DisconnectDialUp()
        {
            if (!UseDialUp)
                return;

            try
            {
                OnStatusMessage(MessageLevel.Info, $"Initiating hang-up for \"{DialUpEntryName}\"");
                RasConnection.GetActiveConnections().FirstOrDefault(ras => ras.EntryName == DialUpEntryName)?.HangUp();
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Exception while attempting to hang-up \"{DialUpEntryName}\": {ex.Message}", ex));
            }

            if (m_startDialUpTime > 0)
            {
                Ticks dialUpConnectedTime = DateTime.UtcNow.Ticks - m_startDialUpTime;
                OnStatusMessage(MessageLevel.Info, $"Dial-up connected for {dialUpConnectedTime.ToElapsedTimeString(2)}");
                m_startDialUpTime = 0;
                TotalDialUpTime += dialUpConnectedTime;
            }
        }

        private void m_rasDialer_Error(object sender, ErrorEventArgs e)
        {
            OnProcessException(MessageLevel.Warning, e.GetException());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void UpdateStatusLogDatabase(FtpFile file, string localFileName, bool success, string message = null)
        {
            if (!m_deviceRecord.Enabled)
                return;

            try
            {
                using (AdoDataConnection connection = new AdoDataConnection("systemSettings"))
                {
                    TableOperations<StatusLog> statusLogTable = new TableOperations<StatusLog>(connection);
                    TableOperations<DownloadedFile> downloadedFileTable = new TableOperations<DownloadedFile>(connection);
                    StatusLog log = statusLogTable.QueryRecordWhere("DeviceID = {0}", m_deviceRecord.ID);

                    if (success)
                    {
                        if ((object)log == null)
                        {
                            log = statusLogTable.NewRecord();
                            log.DeviceID = m_deviceRecord.ID;

                            if (s_statusLogInclusions.Any(extension => file.Name.Contains(extension)) && !s_statusLogExclusions.Any(exclusion => file.Name.Contains(exclusion)))
                            {
                                log.LastFile = file.Name;
                                log.FileDownloadTimestamp = log.LastSuccess;

                                downloadedFileTable.AddNewRecord(new DownloadedFile
                                {
                                    DeviceID = m_deviceRecord.ID,
                                    CreationTime = new FileInfo(localFileName).CreationTimeUtc,
                                    File = file.Name,
                                    FileSize = (int)(new FileInfo(localFileName).Length / 1028), // FileSize in KB
                                    Timestamp = log.LastSuccess.GetValueOrDefault()
                                });

                                if (s_maxDownloadThreshold > 0)
                                {
                                    DateTime timeWindow = DateTime.UtcNow.AddHours(-s_maxDownloadThresholdTimeWindow);

                                    int count = downloadedFileTable.QueryRecordCount(new RecordRestriction("Timestamp >= {0} AND DeviceID = {1}", timeWindow, m_deviceRecord.ID));

                                    if (count > s_maxDownloadThreshold)
                                    {
                                        connection.ExecuteNonQuery("UPDATE Device SET Enabled = 0 WHERE ID = {0}", m_deviceRecord.ID);
                                        log.Message = "Disabled due to excessive file production.";
                                        log.LastFailure = log.LastSuccess;

                                        Program.Host.SendRequest(m_deviceRecord.NodeID, "reloadconfig");
                                        OnStatusMessage(MessageLevel.Warning, $"[{m_deviceRecord.Name}] Disabled due to excessive file downloads. Setting: {s_maxDownloadThreshold}; Count: {count}");

                                        m_deviceRecord.Enabled = false;
                                        // TODO: ADD email notification.
                                    }
                                }
                            }

                            statusLogTable.AddNewRecord(log);
                        }
                        else
                        {
                            log.LastSuccess = DateTime.UtcNow;

                            if (s_statusLogInclusions.Any(extension => file.Name.Contains(extension)) && !s_statusLogExclusions.Any(exclusion => file.Name.Contains(exclusion)))
                            {
                                log.LastFile = file.Name;
                                log.FileDownloadTimestamp = (DateTime)log.LastSuccess;

                                downloadedFileTable.AddNewRecord(new DownloadedFile
                                {
                                    DeviceID = m_deviceRecord.ID,
                                    CreationTime = new FileInfo(localFileName).CreationTimeUtc,
                                    File = file.Name,
                                    FileSize = (int)(new FileInfo(localFileName).Length / 1028), // FileSize in KB
                                    Timestamp = (DateTime)log.LastSuccess
                                });

                                if (s_maxDownloadThreshold > 0)
                                {
                                    DateTime timeWindow = DateTime.UtcNow.AddHours(-s_maxDownloadThresholdTimeWindow);

                                    int count = downloadedFileTable.QueryRecordCount(new RecordRestriction("Timestamp >= {0} AND DeviceID = {1}", timeWindow, m_deviceRecord.ID));

                                    if (count > s_maxDownloadThreshold)
                                    {
                                        connection.ExecuteNonQuery("UPDATE Device SET Enabled = 0 WHERE ID = {0}", m_deviceRecord.ID);
                                        log.Message = "Disabled due to excessive file production.";
                                        log.LastFailure = log.LastSuccess;

                                        Program.Host.SendRequest(m_deviceRecord.NodeID, "reloadconfig");
                                        OnStatusMessage(MessageLevel.Warning, $"[{m_deviceRecord.Name}] Disabled due to excessive file downloads. Setting: {s_maxDownloadThreshold}; Count: {count}");

                                        m_deviceRecord.Enabled = false;
                                    }
                                }
                            }

                            statusLogTable.UpdateRecord(log);
                        }
                    }
                    else
                    {
                        if ((object)log == null)
                        {
                            log = statusLogTable.NewRecord();
                            log.Message = message;
                            log.DeviceID = m_deviceRecord.ID;
                            log.FileDownloadTimestamp = null;
                            statusLogTable.AddNewRecord(log);
                        }
                        else
                        {
                            log.LastFailure = DateTime.UtcNow;
                            log.Message = message;
                            statusLogTable.UpdateRecord(log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnProcessException(MessageLevel.Warning, new InvalidOperationException($"Failed to update StatusLog database for device \"{m_deviceRecord.Acronym}\": {ex.Message}"));
            }
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static readonly ScheduleManager s_scheduleManager;
        private static readonly ConcurrentDictionary<string, Downloader> s_instances;
        private static readonly ConcurrentDictionary<string, LogicalThread> s_dialupScheduler;
        private static readonly LogicalThreadScheduler s_logicalThreadScheduler;
        private static readonly int s_ftpThreadCount;
        private static readonly int s_maxDownloadThreshold;
        private static readonly int s_maxDownloadThresholdTimeWindow;
        private static readonly string[] s_statusLogInclusions;
        private static readonly string[] s_statusLogExclusions;

        // Static Events

        /// <summary>
        /// Raised when there is a file transfer progress notification for any downloader instance.
        /// </summary>
        public static event EventHandler<EventArgs<ProgressUpdate>> ProgressUpdated;

        // Static Constructor
        static Downloader()
        {
            const int DefaultFTPThreadCount = 20;
            const int DefaultMaxDownloadThreshold = 0;
            const int DefaultMaxDownloadThresholdTimeWindow = 24;
            const string DefaultStatusLogInclusions = ".rcd,.d00,.dat,.ctl,.cfg,.pcd";
            const string DefaultStatusLogExclusions = "rms.,trend.";

            CategorizedSettingsElementCollection systemSettings = ConfigurationFile.Current.Settings["systemSettings"];
            systemSettings.Add("FTPThreadCount", DefaultFTPThreadCount, "Max thread count for FTP operations. Set to zero for no limit.");
            systemSettings.Add("MaxDownloadThreshold", DefaultMaxDownloadThreshold, "Maximum downloads a meter can have in a specified time range before disabling the meter, subject to specified StatusLog inclusions and exclusions. Set to 0 to disable.");
            systemSettings.Add("MaxDownloadThresholdTimeWindow", DefaultMaxDownloadThresholdTimeWindow, "Time window for the MaxDownloadThreshold in hours.");
            systemSettings.Add("StatusLogInclusions", DefaultStatusLogInclusions, "Default inclusions to apply when writing updates to StatusLog table and checking MaxDownloadThreshold.");
            systemSettings.Add("StatusLogExclusions", DefaultStatusLogExclusions, "Default exclusions to apply when writing updates to StatusLog table and checking MaxDownloadThreshold.");

            s_instances = new ConcurrentDictionary<string, Downloader>();
            s_dialupScheduler = new ConcurrentDictionary<string, LogicalThread>();

            s_ftpThreadCount = systemSettings["FTPThreadCount"].ValueAsInt32(DefaultFTPThreadCount);
            s_logicalThreadScheduler = new LogicalThreadScheduler();
            s_logicalThreadScheduler.MaxThreadCount = s_ftpThreadCount;

            s_scheduleManager = new ScheduleManager();
            s_scheduleManager.ScheduleDue += s_scheduleManager_ScheduleDue;
            s_scheduleManager.Start();

            s_maxDownloadThreshold = systemSettings["MaxDownloadThreshold"].ValueAsInt32(DefaultMaxDownloadThreshold);
            s_maxDownloadThresholdTimeWindow = systemSettings["MaxDownloadThresholdTimeWindow"].ValueAsInt32(DefaultMaxDownloadThresholdTimeWindow);
            s_statusLogInclusions = systemSettings["StatusLogInclusions"].ValueAs(DefaultStatusLogInclusions).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            s_statusLogExclusions = systemSettings["StatusLogExclusions"].ValueAs(DefaultStatusLogExclusions).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void s_scheduleManager_ScheduleDue(object sender, EventArgs<Schedule> e)
        {
            Schedule schedule = e.Argument;
            Downloader instance;

            if (s_instances.TryGetValue(schedule.Name, out instance))
            {
                instance.AttemptedConnections++;

                if (instance.UseDialUp)
                    instance.m_dialUpOperation.RunOnceAsync();
                else if(instance.UseLogicalThread)
                    instance.m_ftpOperation.RunOnceAsync();
                else
                    instance.m_executeTasks.RunOnceAsync();
            }
        }

        // Static Methods

        private static void RegisterSchedule(Downloader instance)
        {
            s_instances.TryAdd(instance.Name, instance);

            if (instance.UseDialUp)
            {
                // Make sure dial-up's using the same resource (i.e., modem) are executed synchronously
                LogicalThread thread = s_dialupScheduler.GetOrAdd(instance.DialUpEntryName, entryName => new LogicalThread(2));
                WeakReference<Downloader> reference = new WeakReference<Downloader>(instance);

                thread.UnhandledException += (sender, e) =>
                {
                    Downloader downloader;
                    if (reference.TryGetTarget(out downloader))
                        downloader.OnProcessException(MessageLevel.Warning, e.Argument);
                };

                instance.m_dialUpOperation = new LogicalThreadOperation(thread, () =>
                {
                    if (instance.ConnectDialUp())
                    {
                        instance.ExecuteTasks();
                        instance.DisconnectDialUp();
                    }
                }, NormalPriorty);
            }
            else if (s_logicalThreadScheduler.MaxThreadCount > 0)
            {
                LogicalThread thread = s_logicalThreadScheduler.CreateThread();
                thread.UnhandledException += (sender, e) =>
                {
                    WeakReference<Downloader> reference = new WeakReference<Downloader>(instance);
                    Downloader downloader;
                    if (reference.TryGetTarget(out downloader))
                        downloader.OnProcessException(MessageLevel.Warning, e.Argument);
                };

                instance.m_ftpOperation = new LogicalThreadOperation(thread, instance.ExecuteTasks);
            }
            else
            {
                instance.m_executeTasks = new LongSynchronizedOperation(instance.ExecuteTasks, exception => instance.OnProcessException(MessageLevel.Warning, exception));
            }

            s_scheduleManager.AddSchedule(instance.Name, instance.Schedule, $"Download schedule for \"{instance.Name}\"", true);
        }

        private static void DeregisterSchedule(Downloader instance)
        {
            s_scheduleManager.RemoveSchedule(instance.Name);
            s_instances.TryRemove(instance.Name, out instance);
        }

        private static void OnProgressUpdated(Downloader instance, ProgressUpdate update)
        {
            ProgressUpdated?.Invoke(instance, new EventArgs<ProgressUpdate>(update));
        }

        private static void TerminateProcessTree(int ancestorID)
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ParentProcessID = {ancestorID}");
                ManagementObjectCollection descendantIDs = searcher.Get();

                foreach (ManagementBaseObject managementObject in descendantIDs)
                {
                    ManagementObject descendantID = managementObject as ManagementObject;

                    if ((object)descendantID != null)
                        TerminateProcessTree(Convert.ToInt32(descendantID["ProcessID"]));
                }

                try
                {
                    using (Process ancestor = Process.GetProcessById(ancestorID))
                        ancestor.Kill();
                }
                catch (ArgumentException)
                {
                    // Process already exited
                }
            }
            catch (Exception ex)
            {
                Program.Host.LogException(new InvalidOperationException($"Failed while attempting to terminate process tree with ancestor ID {ancestorID}: {ex.Message}", ex));
            }
        }

        #region [ Statistic Functions ]

        private static double GetDownloaderStatistic_Enabled(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.IsConnected ? 1.0D : 0.0D;

            return statistic;
        }

        private static double GetDownloaderStatistic_AttemptedConnections(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.AttemptedConnections;

            return statistic;
        }

        private static double GetDownloaderStatistic_SuccessfulConnections(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.SuccessfulConnections;

            return statistic;
        }

        private static double GetDownloaderStatistic_FailedConnections(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.FailedConnections;

            return statistic;
        }

        private static double GetDownloaderStatistic_AttemptedDialUps(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.AttemptedDialUps;

            return statistic;
        }

        private static double GetDownloaderStatistic_SuccessfulDialUps(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.SuccessfulDialUps;

            return statistic;
        }

        private static double GetDownloaderStatistic_FailedDialUps(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.FailedDialUps;

            return statistic;
        }

        private static double GetDownloaderStatistic_FilesDownloaded(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.FilesDownloaded;

            return statistic;
        }

        private static double GetDownloaderStatistic_MegaBytesDownloaded(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = downloader.BytesDownloaded / (double)SI2.Mega;

            return statistic;
        }

        private static double GetDownloaderStatistic_TotalConnectedTime(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = ((Ticks)downloader.TotalConnectedTime).ToSeconds();

            return statistic;
        }

        private static double GetDownloaderStatistic_TotalDialUpTime(object source, string arguments)
        {
            double statistic = 0.0D;
            Downloader downloader = source as Downloader;

            if ((object)downloader != null)
                statistic = ((Ticks)downloader.TotalDialUpTime).ToSeconds();

            return statistic;
        }

        #endregion

        #endregion
    }
}
