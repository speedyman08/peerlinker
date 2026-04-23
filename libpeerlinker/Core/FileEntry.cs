namespace libpeerlinker.FileHandling;

// Represents a file named inside the torrent metadata.
public record FileEntry
{
    public required Int64 NumPieces;
    public required string[]? DirectoryHierarchy;
    public required string SuggestedFilename;
    public required Int64 Size;
}