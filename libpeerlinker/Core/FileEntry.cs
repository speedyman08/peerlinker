namespace libpeerlinker.FileHandling;

// Represents a file which is provided in a tracker.
public record FileEntry
{
    public required Int64 NumPieces;
    public required string[]? DirectoryHierarchy;
    public required string SuggestedFilename;
    public required Int64 Size;
}