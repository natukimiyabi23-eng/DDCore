namespace DDCore.Core.Model;

public enum SupportState
{
    Supported,
    Approximate,
    Unknown,
    Unsupported,
    Invalid,
}

public enum ObservationQuality
{
    Complete,
    Partial,
    Stale,
    Unsupported,
    Invalid,
}
