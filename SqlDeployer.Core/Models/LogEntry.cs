namespace SqlDeployer.Models;

public enum LogKind { Success, Error, Info }

public record LogEntry(string Message, LogKind Kind);
