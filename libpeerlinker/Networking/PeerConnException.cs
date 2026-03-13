namespace libpeerlinker.Peers;

/// <summary>
/// Exception that indicates something went wrong with the peer connection. In every case we would drop them so this is
/// why it's a catch all kind of thing
/// </summary>
public class PeerConnException(string msg): Exception(msg) {}