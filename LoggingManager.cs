// AlwaysTooLate.Logging (c) 2018-2019 Always Too Late.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using AlwaysTooLate.Core;
using UnityEngine;

namespace AlwaysTooLate.Logging
{
    public delegate void MessageHandler(string message, LogType level);

    /// <summary>
    ///     LoggingManager class. Provides logging functionality such as logging disable and cute log output file.
    ///     This manager spawns additional thread (when enabled) which is being used, to write logs into the log file.
    ///     Should be initialized on main (entry) scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LoggingManager : BehaviourSingleton<LoggingManager>
    {
        private volatile bool _disposed;
        private ConcurrentQueue<Log> _logQueue;
        private FileStream _logStream;
        private Thread _logThread;
        private StreamWriter _logWriter;

        /// <summary>
        ///     The folder, which collects old log files.
        /// </summary>
        [Tooltip("The folder, which collects old log files.")]
        public string BackupDirectory = "./Logs/";

        /// <summary>
        ///     When true, backup folder will be created which will store all old logs.
        /// </summary>
        [Tooltip("When true, backup folder will be created which will store all old logs.")]
        public bool BackupOldLogs = true;

        /// <summary>
        ///     When true, logs which will go the the backup folder will be compressed.
        /// </summary>
        [Tooltip("When true, logs which will go the the backup folder will be compressed.")]
        public bool CompressBackups = true;

        /// <summary>
        ///     When false, no logs will be produced (even into the standard log file!).
        /// </summary>
        [Tooltip("When false, no logs will be produced (even into the standard log file!).")]
        public bool EnableLogs = true;

        /// <summary>
        ///     When false, logs won't contain any stacktrace information.
        /// </summary>
        [Tooltip("When false, logs won't contain any stacktrace information.")]
        public bool EnableStacktrace = true;

        /// <summary>
        ///     When true an additional thread is spawned, which writes the logs into the log file.
        /// </summary>
        [Tooltip("When true an additional thread is being spawn, which writes the logs into the log file.")]
        public bool EnableThreadedWriter = true;

        /// <summary>
        ///     Name of the log output file.
        /// </summary>
        [Tooltip("Name of the log output file.")]
        public string LogFileName = "Log.txt";

        /// <summary>
        ///     The amount of time (ms) that logger thread waits before writing batch of logs into the log file.
        /// </summary>
        [Tooltip("The amount of time (ms) that logger thread waits before writing batch of logs into the log file.")]
        public int LoggerThreadFrequency = 30;

        /// <summary>
        ///     When true, the log stacktrace will be stripped to format: (file:line), instead of
        ///     full stack trace.
        /// </summary>
        [Tooltip("When true, the log stacktrace will be stripped to format: (file:line), instead of full stack trace.")]
        public bool StripStacktrace = true;

        /// <summary>
        ///     The log time format.
        /// </summary>
        [Tooltip("The log time format.")] public string TimeFormat = "dd/MM/yyyy HH:mm:ss";

        /// <summary>
        ///     When false, log callback may be called from non-main thread!
        /// </summary>
        [Tooltip("When false, log callback may be called from non-main thread!")]
        public bool UseDispatchedLogCallback = true;

        /// <summary>
        ///     When true, logging manager will invoke log callback when new log is writing.
        /// </summary>
        [Tooltip("When true, logging manager will invoke log callback when new log is writing.")]
        public bool UseLogCallback = true;

        protected override void OnAwake()
        {
            // Enable or disable logs
            Debug.unityLogger.logEnabled = EnableLogs;

            // Create output log queue
            _logQueue = new ConcurrentQueue<Log>();

            // Add the unity3d log message handle
            Application.logMessageReceived += UnityHandle;

            if (EnableThreadedWriter)
            {
                // create log thread
                _logThread = new Thread(LoggingThread);
                _logThread.Start();
            }

            // remove old file or backup it - if it exists
            if (BackupOldLogs && File.Exists(LogFileName))
            {
                // backup old log file

                // create backup directory if doesn't exist yet.
                if (!Directory.Exists(BackupDirectory))
                    Directory.CreateDirectory(BackupDirectory);

                var backupFileName = LogFileName.Split('.')[0] + DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");

                if (CompressBackups)
                {
                    using (var archive = ZipFile.Open(Path.Combine(BackupDirectory, backupFileName + ".zip"),
                        ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(LogFileName, LogFileName);
                    }

                    // Delete log file
                    File.Delete(LogFileName);
                }
                else
                {
                    // Move the file and change it's name
                    File.Copy(LogFileName, Path.Combine(BackupDirectory, backupFileName + ".txt"));
                }
            }
            else
            {
                // Remove old file
                if (File.Exists(LogFileName))
                    File.Delete(LogFileName);
            }

            // create log file stream
            _logStream = new FileStream(LogFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            _logWriter = new StreamWriter(_logStream);
        }

        protected void OnDestroy()
        {
            _disposed = true;

            // We need to dispose only log writer as it will dispose the _logStream.
            _logWriter.Dispose();

            _logThread.Join(1000);

            if (_logThread.IsAlive)
                _logThread.Abort();
        }

        private void LoggingThread()
        {
            while (!_disposed)
            {
                FlushLogs();
                Thread.Sleep(LoggerThreadFrequency); // sleep some time to get some more new fresh logs to eat.
            }
        }

        private void UnityHandle(string condition, string stackTrace, LogType type)
        {
            if (!EnableLogs)
                return;

            Write(condition, stackTrace, type);
        }

        private string ConstructMessage(Log log)
        {
            var sender = log.Sender.ToString();

            if (log.Sender != null && StripStacktrace && EnableStacktrace)
            {
                // Find first '.cs:'
                var fileExtensionIdx = sender.IndexOf(".cs:", StringComparison.InvariantCultureIgnoreCase);

                // or '.cpp'
                if (fileExtensionIdx == -1)
                    fileExtensionIdx = sender.IndexOf(".cpp:", StringComparison.InvariantCultureIgnoreCase);

                if (fileExtensionIdx >= 0)
                {
                    var fileExtensionIdxEnd = sender.IndexOf(')', fileExtensionIdx) - 1;
                    var fileNameIdx = sender.LastIndexOf('/', fileExtensionIdx, fileExtensionIdx - 1);

                    if (fileExtensionIdxEnd >= 0 && fileNameIdx < sender.Length)
                        sender = sender.Substring(fileNameIdx + 1, fileExtensionIdxEnd - fileNameIdx);
                }
            }

            // construct log message
            var msg = log.Sender != null && EnableStacktrace
                ? $"{log.Time.ToString(TimeFormat)} [{log.Type}] ({sender}): {log.Message}"
                : $"{log.Time.ToString(TimeFormat)} [{log.Type}] {log.Message}";

            return msg.Replace("\n", "") + "\n";
        }

        private void FlushLog(Log log)
        {
            // Construct log message
            var message = ConstructMessage(log);

            // Write log to the file
            _logWriter.Write(message);

            if (UseLogCallback && !UseDispatchedLogCallback)
                // Try call OnMessage
                OnMessage?.Invoke(message, log.Type);
        }

        /// <summary>
        ///     Writes all queued logs into the log file.
        /// </summary>
        public void FlushLogs()
        {
            while (_logQueue.TryDequeue(out var log)) // process all logs
            {
                FlushLog(log);

                // Flush
                _logStream.Flush();
                _logWriter.Flush();
            }
        }

        /// <summary>
        ///     Write the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="sender">The message sender(optional, use null when don't want to use this)</param>
        /// <param name="type">The log level.</param>
        public void Write(string message, object sender, LogType type)
        {
            if (!EnableLogs)
                return;

            // construct log
            var log = new Log
            {
                Type = type,
                Message = message,
                Sender = sender,
                Time = DateTime.Now
            };

            if (EnableThreadedWriter)
                // Enqueue the log message
                _logQueue.Enqueue(log);
            else
                FlushLog(log);

            if (UseLogCallback && UseDispatchedLogCallback)
                // try call OnMessage
                OnMessage?.Invoke(ConstructMessage(log), log.Type);
        }

        /// <summary>
        ///     OnMessage event - called when new log is queued.
        ///     UseLogCallback = true; is required!
        /// </summary>
        public static event MessageHandler OnMessage;

        private struct Log
        {
            public string Message { get; set; }
            public object Sender { get; set; }
            public LogType Type { get; set; }
            public DateTime Time { get; set; }
        }
    }
}