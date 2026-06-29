namespace Monica.DevOps.FileOps.Exceptions;

public enum FileOpsMessageCode
{
    PathRequired,
    AllowedRootsRequired,
    ConfiguredRootMissing,
    PathOutsideAllowedRoots,
    EntryNotFound,
    DirectoryExpected,
    FileExpected,
    HiddenEntriesDisabled,
    WriteOperationsDisabled,
    DeleteOperationsDisabled,
    PathReadOnly,
    InvalidEntryName,
    EntryAlreadyExists,
    TextFileTooLarge,
    BinaryFilePreviewUnsupported,
    UploadTooLarge,
    DownloadTooLarge,
    CannotDeleteRoot,
    UploadFilesRequired,
    EditableExtensionRequired,
    SelectionRequired
}

public sealed class FileOpsOperationException : Exception
{
    public FileOpsMessageCode MessageCode { get; }

    public IReadOnlyList<string> MessageArguments { get; }

    private FileOpsOperationException(
        FileOpsMessageCode messageCode,
        string message,
        IReadOnlyList<string> messageArguments,
        Exception? innerException = null)
        : base(message, innerException)
    {
        MessageCode = messageCode;
        MessageArguments = messageArguments;
    }

    public static FileOpsOperationException PathRequired() => Create(FileOpsMessageCode.PathRequired);

    public static FileOpsOperationException AllowedRootsRequired() => Create(FileOpsMessageCode.AllowedRootsRequired);

    public static FileOpsOperationException ConfiguredRootMissing(string rootPath) => Create(FileOpsMessageCode.ConfiguredRootMissing, rootPath);

    public static FileOpsOperationException PathOutsideAllowedRoots(string path) => Create(FileOpsMessageCode.PathOutsideAllowedRoots, path);

    public static FileOpsOperationException EntryNotFound(string path) => Create(FileOpsMessageCode.EntryNotFound, path);

    public static FileOpsOperationException DirectoryExpected(string path) => Create(FileOpsMessageCode.DirectoryExpected, path);

    public static FileOpsOperationException FileExpected(string path) => Create(FileOpsMessageCode.FileExpected, path);

    public static FileOpsOperationException HiddenEntriesDisabled(string path) => Create(FileOpsMessageCode.HiddenEntriesDisabled, path);

    public static FileOpsOperationException WriteOperationsDisabled() => Create(FileOpsMessageCode.WriteOperationsDisabled);

    public static FileOpsOperationException DeleteOperationsDisabled() => Create(FileOpsMessageCode.DeleteOperationsDisabled);

    public static FileOpsOperationException PathReadOnly(string path) => Create(FileOpsMessageCode.PathReadOnly, path);

    public static FileOpsOperationException InvalidEntryName(string name) => Create(FileOpsMessageCode.InvalidEntryName, name);

    public static FileOpsOperationException EntryAlreadyExists(string path) => Create(FileOpsMessageCode.EntryAlreadyExists, path);

    public static FileOpsOperationException TextFileTooLarge(string path, string maxSize) => Create(FileOpsMessageCode.TextFileTooLarge, path, maxSize);

    public static FileOpsOperationException BinaryFilePreviewUnsupported(string path) => Create(FileOpsMessageCode.BinaryFilePreviewUnsupported, path);

    public static FileOpsOperationException UploadTooLarge(string fileName, string maxSize) => Create(FileOpsMessageCode.UploadTooLarge, fileName, maxSize);

    public static FileOpsOperationException DownloadTooLarge(string path, string maxSize) => Create(FileOpsMessageCode.DownloadTooLarge, path, maxSize);

    public static FileOpsOperationException CannotDeleteRoot(string path) => Create(FileOpsMessageCode.CannotDeleteRoot, path);

    public static FileOpsOperationException UploadFilesRequired() => Create(FileOpsMessageCode.UploadFilesRequired);

    public static FileOpsOperationException EditableExtensionRequired(string path) => Create(FileOpsMessageCode.EditableExtensionRequired, path);

    public static FileOpsOperationException SelectionRequired() => Create(FileOpsMessageCode.SelectionRequired);

    private static FileOpsOperationException Create(FileOpsMessageCode messageCode, params string[] messageArguments)
    {
        return new FileOpsOperationException(messageCode, BuildMessage(messageCode, messageArguments), messageArguments);
    }

    private static string BuildMessage(FileOpsMessageCode messageCode, IReadOnlyList<string> arguments)
    {
        return messageCode switch
        {
            FileOpsMessageCode.PathRequired => "A file system path is required.",
            FileOpsMessageCode.AllowedRootsRequired => "At least one allowed root path is required.",
            FileOpsMessageCode.ConfiguredRootMissing => $"Configured root path '{arguments[0]}' does not exist.",
            FileOpsMessageCode.PathOutsideAllowedRoots => $"Path '{arguments[0]}' is outside the allowed roots.",
            FileOpsMessageCode.EntryNotFound => $"Entry '{arguments[0]}' was not found.",
            FileOpsMessageCode.DirectoryExpected => $"Path '{arguments[0]}' must be a directory.",
            FileOpsMessageCode.FileExpected => $"Path '{arguments[0]}' must be a file.",
            FileOpsMessageCode.HiddenEntriesDisabled => $"Hidden path '{arguments[0]}' is not available.",
            FileOpsMessageCode.WriteOperationsDisabled => "Write operations are disabled.",
            FileOpsMessageCode.DeleteOperationsDisabled => "Delete operations are disabled.",
            FileOpsMessageCode.PathReadOnly => $"Path '{arguments[0]}' is read-only.",
            FileOpsMessageCode.InvalidEntryName => $"Entry name '{arguments[0]}' is invalid.",
            FileOpsMessageCode.EntryAlreadyExists => $"Entry '{arguments[0]}' already exists.",
            FileOpsMessageCode.TextFileTooLarge => $"File '{arguments[0]}' is larger than the text editor limit of {arguments[1]}.",
            FileOpsMessageCode.BinaryFilePreviewUnsupported => $"Binary file preview is not supported for '{arguments[0]}'.",
            FileOpsMessageCode.UploadTooLarge => $"Upload file '{arguments[0]}' exceeds the allowed limit of {arguments[1]}.",
            FileOpsMessageCode.DownloadTooLarge => $"File '{arguments[0]}' exceeds the download limit of {arguments[1]}.",
            FileOpsMessageCode.CannotDeleteRoot => $"Root path '{arguments[0]}' cannot be deleted.",
            FileOpsMessageCode.UploadFilesRequired => "At least one file is required for upload.",
            FileOpsMessageCode.EditableExtensionRequired => $"File '{arguments[0]}' cannot be edited with the current extension policy.",
            FileOpsMessageCode.SelectionRequired => "At least one file system entry must be selected.",
            _ => throw new ArgumentOutOfRangeException(nameof(messageCode), messageCode, null)
        };
    }
}
