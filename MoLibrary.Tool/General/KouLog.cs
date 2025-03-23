using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Timers;
using MoLibrary.Tool.Extensions;
using Timer = System.Timers.Timer;

namespace MoLibrary.Tool.General;

public class KouLog
{
    #region Global log config
    /// <summary>
    /// [Only useful before the KouLog initialized] Default value is AppDomain base directory.
    /// </summary>
    public static string? CustomLogPath { get; set; } = null;
    /// <summary>
    /// [Only useful before the KouLog initialized] Default value is 1000 millisecond; 
    /// </summary>
    public static double? CustomWriteInterval { get; set; } = null;
    private static string _defaultLogBaseDirectory = null!;
    private static DateTime _nextFileNameChangeTime = DateTime.MinValue;
    private static readonly ConcurrentDictionary<string, KouLog> _logQueueDict = new();
    private static Timer _writeLogTimer = null!;
    private static string _curLogFileName = null!;
    private static bool _hasInitialized;
    private static readonly object _initializeLock = new();
    #endregion

    public KouLog()
    {
        
    }
    /// <summary>
    /// Create a logger on specific SubDirectory.
    /// </summary>
    /// <param name="subDirectory">always means the name of class for log.</param>
    public KouLog(string subDirectory)
    {
        SubDirectory = subDirectory;
    }

    protected readonly object LogWriteLock = new();
    protected readonly object LogBufferLock = new();
    protected StringBuilder Buffer = null!;
    /// <summary>
    /// Combine with the log path and the sub directory. 
    /// </summary>
    public string? SubDirectory { get; init; }
    /// <summary>
    /// Default is LogLevel.Info
    /// </summary>
    public LogLevel LoggingLevel { get; init; } = LogLevel.Info;


    private static void Initialize()
    {
        if(_hasInitialized) return;
        lock (_initializeLock)
        {
            if (_hasInitialized) return;
            var interval = CustomWriteInterval ?? 1000;
            _writeLogTimer = new Timer(interval);
            _writeLogTimer.Elapsed += WriteLogTimerElapsed;
            _defaultLogBaseDirectory = CustomLogPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _writeLogTimer.Start();
            _nextFileNameChangeTime = DateTime.Now.Date.AddDays(1);
            _curLogFileName = DateTime.Now.Date.ToString("yyyy-MM-dd");
            _hasInitialized = true;
        }
    }

    public enum LogLevel
    {
        /// <summary>
        /// A log level used for events considered to be useful during software debugging when more granular information is needed.
        /// </summary>
        Debug,
        /// <summary>
        /// An event happened, the event is purely informative and can be ignored during normal operations.
        /// </summary>
        Info,
        /// <summary>
        /// Unexpected behavior happened inside the application, but it is continuing its work and the key business features are operating as expected.
        /// </summary>
        Warning,
        /// <summary>
        /// One or more functionalities are not working, preventing some functionalities from working correctly.
        /// </summary>
        Error,
        /// <summary>
        /// One or more key business functionalities are not working and the whole system doesn’t fulfill the business functionalities.
        /// </summary>
        Fatal
    }

    /// <summary>
    /// Also the buffer key of every logger.
    /// </summary>
    public string CurLogFileDirectory => 
        SubDirectory != null ? Path.Combine(_defaultLogBaseDirectory, SubDirectory) : _defaultLogBaseDirectory;
    public string CurLogFilePath
    {
        get
        {
            if (DateTime.Now >= _nextFileNameChangeTime)
            {
                _curLogFileName = _nextFileNameChangeTime.ToString("yyyy-MM-dd");
                _nextFileNameChangeTime = _nextFileNameChangeTime.AddDays(1);
            } 
            
            return Path.Combine(CurLogFileDirectory, _curLogFileName + ".log");
        }
    }

    private static void WriteLogTimerElapsed(object sender, ElapsedEventArgs args)
    {
        foreach (var (_, logger) in _logQueueDict)
        {
            RetrieveAndWrite(logger);
        }
    }
    private static void RetrieveAndWrite(KouLog logger)
    {
        var buffer = logger.Buffer;
        if (buffer.Length > 0)
        {
            string logMsg;
            lock (logger.LogBufferLock)
            {
                logMsg = buffer.ToString();
                buffer.Clear();
            }

            logger.WriteLog(logMsg);
        }
    }
    private void WriteLog(string? content)
    {
        if (content.IsNullOrEmpty()) return;
        if (!Directory.Exists(CurLogFileDirectory))
        {
            Directory.CreateDirectory(CurLogFileDirectory);
        }

        lock (LogWriteLock)
        {
            FileTool.AppendFile(CurLogFilePath, content);
        }
    }

    /// <summary>
    /// Add log into log file.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="level"></param>
    /// <param name="writeInstantly">Usually uses in quit program.</param>
    public void Add(string? content, LogLevel? level = null, bool writeInstantly = false)
    {
        if(content == null) return;
        Initialize();
        var logContent = $"[{DateTime.Now}][{(level ?? LoggingLevel).ToString().ToUpper()}] {content}";
        var logger = _logQueueDict.GetOrAdd(CurLogFileDirectory, p =>
        {
            Buffer = new StringBuilder();
            return this;
        });
        lock (logger.LogBufferLock)
        {
            logger.Buffer.AppendLine(logContent);
        }
        if (writeInstantly)
        {
            RetrieveAndWrite(this);
        }
    }
    /// <summary>
    /// Use normal config to quickly add log into log file.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="level"></param>
    /// <param name="writeInstantly">Usually uses in quit program.</param>
    public static void QuickAdd(string? content, LogLevel level = LogLevel.Info, bool writeInstantly = false)
    {
        new KouLog().Add(content, level, writeInstantly);
    }
}