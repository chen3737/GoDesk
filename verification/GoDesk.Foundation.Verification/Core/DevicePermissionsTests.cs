using GoDesk.Core.Security;

namespace GoDesk.Foundation.Verification.Core;

public sealed class DevicePermissionsTests
{
    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Paired_default_grants_everything_except_admin_terminal()
    {
        DevicePermissions permissions = DevicePermissions.PairedDefault;

        (DeviceCapability Capability, ulong Bits)[] expectedCapabilities =
        [
            (DeviceCapability.ViewScreen, 0x0001UL),
            (DeviceCapability.ControlInput, 0x0002UL),
            (DeviceCapability.ReceiveAudio, 0x0004UL),
            (DeviceCapability.SwitchDisplay, 0x0008UL),
            (DeviceCapability.UploadToDevice, 0x0010UL),
            (DeviceCapability.DownloadFromDevice, 0x0020UL),
            (DeviceCapability.BrowseUserProfile, 0x0040UL),
            (DeviceCapability.MoveOrRename, 0x0080UL),
            (DeviceCapability.DeleteToRecycleBin, 0x0100UL),
            (DeviceCapability.PermanentDelete, 0x0200UL),
            (DeviceCapability.ExecuteFile, 0x0400UL),
            (DeviceCapability.SecureDesktop, 0x0800UL),
            (DeviceCapability.Terminal, 0x1000UL),
            (DeviceCapability.AdminTerminal, 0x2000UL),
        ];

        foreach ((DeviceCapability capability, ulong bits) in expectedCapabilities)
        {
            Assert.Equal(bits, (ulong)capability);
        }

        Assert.Equal((DeviceCapability)0x3FFFUL, DevicePermissions.AllCapabilities);

        Assert.True(permissions.Allows(DeviceCapability.Terminal));
        Assert.True(permissions.Allows(DeviceCapability.PermanentDelete));
        Assert.True(permissions.Allows(DeviceCapability.ExecuteFile));
        Assert.False(permissions.Allows(DeviceCapability.AdminTerminal));

        foreach ((DeviceCapability capability, _) in expectedCapabilities)
        {
            Assert.Equal(capability != DeviceCapability.AdminTerminal, permissions.Allows(capability));
        }
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Grant_and_revoke_return_new_values()
    {
        DevicePermissions defaultPermissions = default;
        DevicePermissions zeroPermissions = new(0);
        DevicePermissions nonzeroPermissions = new((ulong)DeviceCapability.ViewScreen);
        DevicePermissions original = DevicePermissions.PairedDefault;

        DevicePermissions granted = original.Grant(DeviceCapability.AdminTerminal);
        DevicePermissions revoked = granted.Revoke(DeviceCapability.Terminal);

        Assert.Equal(defaultPermissions, zeroPermissions);
        Assert.Equal(defaultPermissions.Bits, zeroPermissions.Bits);
        Assert.NotEqual(defaultPermissions.Bits, nonzeroPermissions.Bits);
        Assert.NotEqual(defaultPermissions, nonzeroPermissions);
        Assert.False(defaultPermissions.Allows(DeviceCapability.ViewScreen));
        Assert.False(original.Allows(DeviceCapability.AdminTerminal));
        Assert.True(granted.Allows(DeviceCapability.AdminTerminal));
        Assert.True(granted.Allows(DeviceCapability.Terminal));
        Assert.False(revoked.Allows(DeviceCapability.Terminal));
        Assert.False(original.Allows(DeviceCapability.None));
        Assert.False(original.Allows(DeviceCapability.Terminal | DeviceCapability.AdminTerminal));
        Assert.True(granted.Allows(DeviceCapability.Terminal | DeviceCapability.AdminTerminal));

        ArgumentOutOfRangeException grantNoneException =
            Assert.Throws<ArgumentOutOfRangeException>(() => original.Grant(DeviceCapability.None));
        ArgumentOutOfRangeException revokeNoneException =
            Assert.Throws<ArgumentOutOfRangeException>(() => original.Revoke(DeviceCapability.None));
        ArgumentOutOfRangeException grantUnknownException =
            Assert.Throws<ArgumentOutOfRangeException>(() => original.Grant((DeviceCapability)(1UL << 63)));
        ArgumentOutOfRangeException revokeUnknownException =
            Assert.Throws<ArgumentOutOfRangeException>(() => original.Revoke((DeviceCapability)(1UL << 63)));

        Assert.Equal("capability", grantNoneException.ParamName);
        Assert.Equal("capability", revokeNoneException.ParamName);
        Assert.Equal("capability", grantUnknownException.ParamName);
        Assert.Equal("capability", revokeUnknownException.ParamName);
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Unknown_permission_bits_are_rejected()
    {
        const ulong UnknownBit = 1UL << 63;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new DevicePermissions(UnknownBit));

        Assert.Equal("bits", exception.ParamName);
        Assert.Contains("Unknown capability bits are not allowed.", exception.Message, StringComparison.Ordinal);
    }
#pragma warning restore CA1707
}
