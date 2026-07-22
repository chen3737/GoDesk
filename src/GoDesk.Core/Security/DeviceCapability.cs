namespace GoDesk.Core.Security;

[Flags]
public enum DeviceCapability : ulong
{
    None = 0,
    ViewScreen = 1UL << 0,
    ControlInput = 1UL << 1,
    ReceiveAudio = 1UL << 2,
    SwitchDisplay = 1UL << 3,
    UploadToDevice = 1UL << 4,
    DownloadFromDevice = 1UL << 5,
    BrowseUserProfile = 1UL << 6,
    MoveOrRename = 1UL << 7,
    DeleteToRecycleBin = 1UL << 8,
    PermanentDelete = 1UL << 9,
    ExecuteFile = 1UL << 10,
    SecureDesktop = 1UL << 11,
    Terminal = 1UL << 12,
    AdminTerminal = 1UL << 13,
}
