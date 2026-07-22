namespace GoDesk.Core.Security;

public readonly record struct DevicePermissions
{
    public const DeviceCapability AllCapabilities =
        DeviceCapability.ViewScreen |
        DeviceCapability.ControlInput |
        DeviceCapability.ReceiveAudio |
        DeviceCapability.SwitchDisplay |
        DeviceCapability.UploadToDevice |
        DeviceCapability.DownloadFromDevice |
        DeviceCapability.BrowseUserProfile |
        DeviceCapability.MoveOrRename |
        DeviceCapability.DeleteToRecycleBin |
        DeviceCapability.PermanentDelete |
        DeviceCapability.ExecuteFile |
        DeviceCapability.SecureDesktop |
        DeviceCapability.Terminal |
        DeviceCapability.AdminTerminal;

    public DevicePermissions(ulong bits)
    {
        if ((bits & ~(ulong)AllCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bits),
                bits,
                "Unknown capability bits are not allowed.");
        }

        Bits = bits;
    }

    public static DevicePermissions PairedDefault { get; } =
        new((ulong)(AllCapabilities & ~DeviceCapability.AdminTerminal));

    public ulong Bits { get; }

    public bool Allows(DeviceCapability capability)
    {
        ulong capabilityBits = (ulong)capability;
        return capability != DeviceCapability.None && (Bits & capabilityBits) == capabilityBits;
    }

    public DevicePermissions Grant(DeviceCapability capability)
    {
        ValidateCapability(capability);
        return new DevicePermissions(Bits | (ulong)capability);
    }

    public DevicePermissions Revoke(DeviceCapability capability)
    {
        ValidateCapability(capability);
        return new DevicePermissions(Bits & ~(ulong)capability);
    }

    private static void ValidateCapability(DeviceCapability capability)
    {
        if (capability == DeviceCapability.None || (capability & ~AllCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capability));
        }
    }
}
