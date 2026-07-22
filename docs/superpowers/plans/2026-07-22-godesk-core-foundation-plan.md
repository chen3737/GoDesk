# GoDesk Core Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the dependency-light, security-critical .NET 10 foundation for GoDesk: repository rules, device permissions, device IDs, bounded control framing, canonical CBOR hello messages, operation deduplication, and shared input guards.

**Architecture:** Keep pure domain types in `GoDesk.Core` and wire-format code in `GoDesk.Protocol`; neither project opens sockets, touches CNG, or starts Windows services. A single executable xUnit v3 project under `verification/` validates both libraries without creating `test/` or `tests/`.

**Tech Stack:** C# 14, .NET SDK 10.0.302, `net10.0-windows10.0.22621.0`, System.Formats.Cbor 10.0.10, xUnit v3 3.2.2, Microsoft Testing Platform, PowerShell, Git.

---

## Scope and file map

This plan creates or modifies:

```text
.editorconfig                         C# and text formatting
.gitignore                           build output and forbidden test/ rule
Directory.Build.props                shared compiler, analyzer and restore policy
Directory.Packages.props             centrally pinned packages
NuGet.config                         single explicit package source
global.json                          pinned .NET SDK
GoDesk.sln                           solution

src/GoDesk.Core/
  GoDesk.Core.csproj
  Identity/Base32NoPadding.cs        RFC 4648 alphabet encoder without padding
  Identity/DeviceId.cs               SHA-256 SPKI-derived device identity
  Operations/BoundedOperationSet.cs  recent operation-ID duplicate rejection
  Operations/OperationId.cs          random 128-bit operation identifier
  Security/DeviceCapability.cs       stable on-wire permission bits
  Security/DevicePermissions.cs      validated immutable permission set
  Validation/GoDeskErrorCode.cs      stable application error categories
  Validation/GoDeskException.cs      typed non-secret error
  Validation/InputGuard.cs           byte, UTF-8 and collection bounds

src/GoDesk.Protocol/
  GoDesk.Protocol.csproj
  Control/ControlFrame.cs             decoded frame value
  Control/ControlFrameCodec.cs        fixed header and bounded payload
  Control/ControlMessageType.cs       stable message discriminators
  Control/ProtocolFrameException.cs   malformed-frame exception
  Control/ProtocolLimits.cs           protocol quotas
  Handshake/HelloMessage.cs           validated hello value
  Handshake/HelloMessageCodec.cs      canonical CBOR hello codec

verification/GoDesk.Foundation.Verification/
  GoDesk.Foundation.Verification.csproj
  ArchitectureFacts.cs
  Core/DevicePermissionsTests.cs
  Core/DeviceIdTests.cs
  Core/BoundedOperationSetTests.cs
  Core/InputGuardTests.cs
  Protocol/ControlFrameCodecTests.cs
  Protocol/HelloMessageCodecTests.cs

scripts/verify.ps1                    locked restore, build, verification and folder checks
docs/development.md                   exact local bootstrap and verification commands
```

No production project in this plan depends on WinUI, SQLite, QUIC, MCP, CNG, services, or installer libraries. Those enter later plans after these formats and invariants are stable.

### Task 1: Bootstrap the repository and verification runner

**Files:**

- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `NuGet.config`
- Create: `.editorconfig`
- Modify: `.gitignore`
- Create: `GoDesk.sln`
- Create: `src/GoDesk.Core/GoDesk.Core.csproj`
- Create: `src/GoDesk.Protocol/GoDesk.Protocol.csproj`
- Create: `verification/GoDesk.Foundation.Verification/GoDesk.Foundation.Verification.csproj`
- Create: `verification/GoDesk.Foundation.Verification/ArchitectureFacts.cs`

- [ ] **Step 1: Install and verify the pinned SDK**

Run from an elevated PowerShell only for installation:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

Close that shell, open a normal non-elevated PowerShell in the workspace, then run:

```powershell
dotnet --version
```

Expected: `10.0.302` or a later `10.0.30x` patch accepted by `rollForward: latestPatch`. Do not continue with a preview SDK.

- [ ] **Step 2: Initialize Git and preserve the approved specifications**

Run:

```powershell
git init -b main
git add .gitignore docs
git commit -m "docs: add approved GoDesk specifications"
```

Expected: a new root commit containing only `.gitignore` and `docs/`.

- [ ] **Step 3: Add deterministic SDK, package and compiler policy**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="System.Formats.Cbor" Version="10.0.10" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
  </ItemGroup>
</Project>
```

Create `NuGet.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
```

Create `.editorconfig`:

```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csproj,props,targets}]
indent_style = space
indent_size = 4

[*.md]
trim_trailing_whitespace = false
```

Replace `.gitignore` with:

```gitignore
test/
bin/
obj/
.vs/
artifacts/
TestResults/
*.user
*.suo
```

- [ ] **Step 4: Create the projects and solution**

Create `src/GoDesk.Core/GoDesk.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

Create `src/GoDesk.Protocol/GoDesk.Protocol.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="System.Formats.Cbor" />
    <ProjectReference Include="..\GoDesk.Core\GoDesk.Core.csproj" />
  </ItemGroup>
</Project>
```

Create `verification/GoDesk.Foundation.Verification/GoDesk.Foundation.Verification.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <ProjectReference Include="..\..\src\GoDesk.Core\GoDesk.Core.csproj" />
    <ProjectReference Include="..\..\src\GoDesk.Protocol\GoDesk.Protocol.csproj" />
  </ItemGroup>
</Project>
```

Run:

```powershell
dotnet new sln -n GoDesk --format sln
dotnet sln GoDesk.sln add src/GoDesk.Core/GoDesk.Core.csproj
dotnet sln GoDesk.sln add src/GoDesk.Protocol/GoDesk.Protocol.csproj
dotnet sln GoDesk.sln add verification/GoDesk.Foundation.Verification/GoDesk.Foundation.Verification.csproj
```

Expected: all three projects are listed by `dotnet sln GoDesk.sln list`.

- [ ] **Step 5: Add and run the first verification**

Create `verification/GoDesk.Foundation.Verification/ArchitectureFacts.cs`:

```csharp
namespace GoDesk.Foundation.Verification;

public sealed class ArchitectureFacts
{
    [Fact]
    public void Foundation_runs_only_on_64_bit_windows()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(Environment.Is64BitProcess);
    }
}
```

Run:

```powershell
dotnet restore GoDesk.sln
dotnet build GoDesk.sln --configuration Release --no-restore
dotnet run --project verification/GoDesk.Foundation.Verification --configuration Release --no-build
```

Expected: restore creates committed `packages.lock.json` files, build has zero warnings and errors, and 1 verification passes.

- [ ] **Step 6: Commit the bootstrap**

```powershell
git add .editorconfig .gitignore global.json Directory.Build.props Directory.Packages.props NuGet.config GoDesk.sln src verification
git commit -m "build: bootstrap GoDesk .NET foundation"
```

### Task 2: Define the immutable device permission model

**Files:**

- Create: `src/GoDesk.Core/Security/DeviceCapability.cs`
- Create: `src/GoDesk.Core/Security/DevicePermissions.cs`
- Create: `verification/GoDesk.Foundation.Verification/Core/DevicePermissionsTests.cs`

- [ ] **Step 1: Write the failing permission tests**

Create `verification/GoDesk.Foundation.Verification/Core/DevicePermissionsTests.cs`:

```csharp
using GoDesk.Core.Security;

namespace GoDesk.Foundation.Verification.Core;

public sealed class DevicePermissionsTests
{
    [Fact]
    public void Paired_default_grants_everything_except_admin_terminal()
    {
        DevicePermissions permissions = DevicePermissions.PairedDefault;

        Assert.True(permissions.Allows(DeviceCapability.Terminal));
        Assert.True(permissions.Allows(DeviceCapability.PermanentDelete));
        Assert.True(permissions.Allows(DeviceCapability.ExecuteFile));
        Assert.False(permissions.Allows(DeviceCapability.AdminTerminal));
    }

    [Fact]
    public void Grant_and_revoke_return_new_values()
    {
        DevicePermissions original = DevicePermissions.PairedDefault;
        DevicePermissions granted = original.Grant(DeviceCapability.AdminTerminal);
        DevicePermissions revoked = granted.Revoke(DeviceCapability.Terminal);

        Assert.False(original.Allows(DeviceCapability.AdminTerminal));
        Assert.True(granted.Allows(DeviceCapability.AdminTerminal));
        Assert.False(revoked.Allows(DeviceCapability.Terminal));
    }

    [Fact]
    public void Unknown_permission_bits_are_rejected()
    {
        ulong unknownBit = 1UL << 63;
        Assert.Throws<ArgumentOutOfRangeException>(() => new DevicePermissions(unknownBit));
    }
}
```

- [ ] **Step 2: Run the tests and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because `GoDesk.Core.Security` and its types do not exist.

- [ ] **Step 3: Implement stable capability bits and validated permissions**

Create `src/GoDesk.Core/Security/DeviceCapability.cs`:

```csharp
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
```

Create `src/GoDesk.Core/Security/DevicePermissions.cs`:

```csharp
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

    public static DevicePermissions PairedDefault { get; } =
        new((ulong)(AllCapabilities & ~DeviceCapability.AdminTerminal));

    public DevicePermissions(ulong bits)
    {
        ulong knownBits = (ulong)AllCapabilities;
        if ((bits & ~knownBits) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bits), "Unknown capability bits are not allowed.");
        }

        Bits = bits;
    }

    public ulong Bits { get; }

    public bool Allows(DeviceCapability capability) =>
        capability != DeviceCapability.None &&
        (Bits & (ulong)capability) == (ulong)capability;

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
        if (capability == DeviceCapability.None ||
            ((ulong)capability & ~(ulong)AllCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capability));
        }
    }
}
```

- [ ] **Step 4: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 4 total verifications pass.

- [ ] **Step 5: Commit**

```powershell
git add src/GoDesk.Core/Security verification/GoDesk.Foundation.Verification/Core/DevicePermissionsTests.cs
git commit -m "feat: add device permission model"
```

### Task 3: Derive canonical device IDs from public keys

**Files:**

- Create: `src/GoDesk.Core/Identity/Base32NoPadding.cs`
- Create: `src/GoDesk.Core/Identity/DeviceId.cs`
- Create: `verification/GoDesk.Foundation.Verification/Core/DeviceIdTests.cs`

- [ ] **Step 1: Write the failing identity tests**

Create `verification/GoDesk.Foundation.Verification/Core/DeviceIdTests.cs`:

```csharp
using System.Text;
using GoDesk.Core.Identity;

namespace GoDesk.Foundation.Verification.Core;

public sealed class DeviceIdTests
{
    [Fact]
    public void Base32_matches_the_RFC_4648_foo_vector()
    {
        string encoded = Base32NoPadding.Encode(Encoding.ASCII.GetBytes("foo"));
        Assert.Equal("MZXW6", encoded);
    }

    [Fact]
    public void Device_id_is_deterministic_uppercase_and_52_characters()
    {
        byte[] spki = [1, 2, 3, 4, 5, 6];

        DeviceId first = DeviceId.FromSubjectPublicKeyInfo(spki);
        DeviceId second = DeviceId.FromSubjectPublicKeyInfo(spki);

        Assert.Equal(first, second);
        Assert.Equal(52, first.Value.Length);
        Assert.Matches("^[A-Z2-7]{52}$", first.Value);
    }

    [Fact]
    public void Invalid_device_id_text_is_rejected()
    {
        Assert.Throws<FormatException>(() => DeviceId.Parse("GD-invalid"));
    }
}
```

- [ ] **Step 2: Run and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because `Base32NoPadding` and `DeviceId` do not exist.

- [ ] **Step 3: Implement Base32 without padding**

Create `src/GoDesk.Core/Identity/Base32NoPadding.cs`:

```csharp
using System.Text;

namespace GoDesk.Core.Identity;

public static class Base32NoPadding
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        uint buffer = 0;
        int bitsInBuffer = 0;

        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                int index = (int)((buffer >> bitsInBuffer) & 0x1F);
                output.Append(Alphabet[index]);
            }
        }

        if (bitsInBuffer > 0)
        {
            int index = (int)((buffer << (5 - bitsInBuffer)) & 0x1F);
            output.Append(Alphabet[index]);
        }

        return output.ToString();
    }
}
```

- [ ] **Step 4: Implement SHA-256 SPKI device IDs**

Create `src/GoDesk.Core/Identity/DeviceId.cs`:

```csharp
using System.Security.Cryptography;

namespace GoDesk.Core.Identity;

public readonly record struct DeviceId
{
    public const int EncodedLength = 52;

    private DeviceId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static DeviceId FromSubjectPublicKeyInfo(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        if (subjectPublicKeyInfo.IsEmpty)
        {
            throw new ArgumentException("The public key cannot be empty.", nameof(subjectPublicKeyInfo));
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(subjectPublicKeyInfo, digest);
        return new DeviceId(Base32NoPadding.Encode(digest));
    }

    public static DeviceId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length != EncodedLength ||
            value.Any(character =>
                !(character is >= 'A' and <= 'Z') &&
                !(character is >= '2' and <= '7')))
        {
            throw new FormatException("A device ID must be 52 uppercase Base32 characters.");
        }

        return new DeviceId(value);
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 5: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 7 total verifications pass.

- [ ] **Step 6: Commit**

```powershell
git add src/GoDesk.Core/Identity verification/GoDesk.Foundation.Verification/Core/DeviceIdTests.cs
git commit -m "feat: derive canonical device IDs"
```

### Task 4: Add bounded control-frame encoding

**Files:**

- Create: `src/GoDesk.Protocol/Control/ProtocolLimits.cs`
- Create: `src/GoDesk.Protocol/Control/ControlMessageType.cs`
- Create: `src/GoDesk.Protocol/Control/ControlFrame.cs`
- Create: `src/GoDesk.Protocol/Control/ProtocolFrameException.cs`
- Create: `src/GoDesk.Protocol/Control/ControlFrameCodec.cs`
- Create: `verification/GoDesk.Foundation.Verification/Protocol/ControlFrameCodecTests.cs`

- [ ] **Step 1: Write the failing frame tests**

Create `verification/GoDesk.Foundation.Verification/Protocol/ControlFrameCodecTests.cs`:

```csharp
using GoDesk.Protocol.Control;

namespace GoDesk.Foundation.Verification.Protocol;

public sealed class ControlFrameCodecTests
{
    [Fact]
    public void Frame_round_trips_with_exact_payload()
    {
        byte[] encoded = ControlFrameCodec.Encode(ControlMessageType.Hello, [1, 2, 3]);
        ControlFrame frame = ControlFrameCodec.Decode(encoded);

        Assert.Equal(ProtocolLimits.CurrentVersion, frame.Version);
        Assert.Equal(ControlMessageType.Hello, frame.MessageType);
        Assert.Equal(new byte[] { 1, 2, 3 }, frame.Payload.ToArray());
    }

    [Fact]
    public void Oversized_payload_is_rejected_before_encoding()
    {
        byte[] payload = new byte[ProtocolLimits.MaxControlPayloadBytes + 1];
        Assert.Throws<ProtocolFrameException>(() =>
            ControlFrameCodec.Encode(ControlMessageType.Hello, payload));
    }

    [Fact]
    public void Truncated_frame_is_rejected()
    {
        byte[] encoded = ControlFrameCodec.Encode(ControlMessageType.Hello, [1, 2, 3]);
        Assert.Throws<ProtocolFrameException>(() => ControlFrameCodec.Decode(encoded[..^1]));
    }

    [Fact]
    public void Unknown_message_type_is_rejected()
    {
        byte[] encoded = ControlFrameCodec.Encode(ControlMessageType.Hello, []);
        encoded[2] = 0x7F;
        encoded[3] = 0xFF;

        Assert.Throws<ProtocolFrameException>(() => ControlFrameCodec.Decode(encoded));
    }
}
```

- [ ] **Step 2: Run and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because the control framing types do not exist.

- [ ] **Step 3: Add constants, discriminators and values**

Create `src/GoDesk.Protocol/Control/ProtocolLimits.cs`:

```csharp
namespace GoDesk.Protocol.Control;

public static class ProtocolLimits
{
    public const ushort CurrentVersion = 1;
    public const int ControlHeaderBytes = 8;
    public const int MaxControlPayloadBytes = 64 * 1024;
    public const int MaxTextBytes = 4 * 1024;
    public const int MaxCollectionItems = 1_024;
    public const int MaxCborDepth = 8;
}
```

Create `src/GoDesk.Protocol/Control/ControlMessageType.cs`:

```csharp
namespace GoDesk.Protocol.Control;

public enum ControlMessageType : ushort
{
    Hello = 1,
    Challenge = 2,
    Pairing = 3,
    PermissionState = 4,
    Error = 5,
}
```

Create `src/GoDesk.Protocol/Control/ControlFrame.cs`:

```csharp
namespace GoDesk.Protocol.Control;

public sealed record ControlFrame(
    ushort Version,
    ControlMessageType MessageType,
    ReadOnlyMemory<byte> Payload);
```

Create `src/GoDesk.Protocol/Control/ProtocolFrameException.cs`:

```csharp
namespace GoDesk.Protocol.Control;

public sealed class ProtocolFrameException : Exception
{
    public ProtocolFrameException()
        : base("The protocol frame is invalid.")
    {
    }

    public ProtocolFrameException(string message)
        : base(message)
    {
    }

    public ProtocolFrameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 4: Implement the fixed eight-byte header codec**

Create `src/GoDesk.Protocol/Control/ControlFrameCodec.cs`:

```csharp
using System.Buffers.Binary;

namespace GoDesk.Protocol.Control;

public static class ControlFrameCodec
{
    public static byte[] Encode(ControlMessageType messageType, ReadOnlySpan<byte> payload)
    {
        if (!Enum.IsDefined(messageType))
        {
            throw new ProtocolFrameException("The control message type is unknown.");
        }

        if (payload.Length > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The control payload exceeds 64 KiB.");
        }

        byte[] frame = new byte[ProtocolLimits.ControlHeaderBytes + payload.Length];
        Span<byte> header = frame.AsSpan(0, ProtocolLimits.ControlHeaderBytes);
        BinaryPrimitives.WriteUInt16BigEndian(header, ProtocolLimits.CurrentVersion);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], (ushort)messageType);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ProtocolLimits.ControlHeaderBytes));
        return frame;
    }

    public static ControlFrame Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < ProtocolLimits.ControlHeaderBytes)
        {
            throw new ProtocolFrameException("The control frame header is truncated.");
        }

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(frame);
        ushort rawType = BinaryPrimitives.ReadUInt16BigEndian(frame[2..]);
        uint rawLength = BinaryPrimitives.ReadUInt32BigEndian(frame[4..]);

        if (version != ProtocolLimits.CurrentVersion)
        {
            throw new ProtocolFrameException("The protocol version is unsupported.");
        }

        var messageType = (ControlMessageType)rawType;
        if (!Enum.IsDefined(messageType))
        {
            throw new ProtocolFrameException("The control message type is unknown.");
        }

        if (rawLength > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The advertised payload exceeds 64 KiB.");
        }

        int expectedLength = checked(ProtocolLimits.ControlHeaderBytes + (int)rawLength);
        if (frame.Length != expectedLength)
        {
            throw new ProtocolFrameException("The control frame length does not match its header.");
        }

        return new ControlFrame(
            version,
            messageType,
            frame[ProtocolLimits.ControlHeaderBytes..].ToArray());
    }
}
```

- [ ] **Step 5: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 11 total verifications pass.

- [ ] **Step 6: Commit**

```powershell
git add src/GoDesk.Protocol/Control verification/GoDesk.Foundation.Verification/Protocol/ControlFrameCodecTests.cs
git commit -m "feat: add bounded control framing"
```

### Task 5: Encode and decode the canonical CBOR hello message

**Files:**

- Create: `src/GoDesk.Protocol/Handshake/HelloMessage.cs`
- Create: `src/GoDesk.Protocol/Handshake/HelloMessageCodec.cs`
- Create: `verification/GoDesk.Foundation.Verification/Protocol/HelloMessageCodecTests.cs`

- [ ] **Step 1: Write the failing CBOR tests**

Create `verification/GoDesk.Foundation.Verification/Protocol/HelloMessageCodecTests.cs`:

```csharp
using GoDesk.Core.Identity;
using GoDesk.Protocol.Control;
using GoDesk.Protocol.Handshake;

namespace GoDesk.Foundation.Verification.Protocol;

public sealed class HelloMessageCodecTests
{
    private static readonly DeviceId SampleDeviceId =
        DeviceId.FromSubjectPublicKeyInfo([1, 2, 3, 4]);

    [Fact]
    public void Hello_round_trips_without_losing_fields()
    {
        var original = new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
            featureBits: 0x15,
            unixTimeMilliseconds: 1_700_000_000_000);

        byte[] encoded = HelloMessageCodec.Encode(original);
        HelloMessage decoded = HelloMessageCodec.Decode(encoded);

        Assert.Equal(original.ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(original.DeviceId, decoded.DeviceId);
        Assert.Equal(original.Nonce.ToArray(), decoded.Nonce.ToArray());
        Assert.Equal(original.FeatureBits, decoded.FeatureBits);
        Assert.Equal(original.UnixTimeMilliseconds, decoded.UnixTimeMilliseconds);
    }

    [Fact]
    public void Nonce_must_be_exactly_32_bytes()
    {
        Assert.Throws<ArgumentException>(() => new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            new byte[31],
            0,
            0));
    }

    [Fact]
    public void Trailing_CBOR_data_is_rejected()
    {
        var hello = new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            new byte[32],
            0,
            0);
        byte[] encoded = [.. HelloMessageCodec.Encode(hello), 0x00];

        Assert.Throws<ProtocolFrameException>(() => HelloMessageCodec.Decode(encoded));
    }
}
```

- [ ] **Step 2: Run and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because the hello message types do not exist.

- [ ] **Step 3: Implement the validated hello value**

Create `src/GoDesk.Protocol/Handshake/HelloMessage.cs`:

```csharp
using GoDesk.Core.Identity;

namespace GoDesk.Protocol.Handshake;

public sealed record HelloMessage
{
    public HelloMessage(
        ushort protocolVersion,
        DeviceId deviceId,
        ReadOnlySpan<byte> nonce,
        ulong featureBits,
        long unixTimeMilliseconds)
    {
        if (nonce.Length != 32)
        {
            throw new ArgumentException("A hello nonce must contain exactly 32 bytes.", nameof(nonce));
        }

        if (string.IsNullOrEmpty(deviceId.Value))
        {
            throw new ArgumentException("A hello message requires a device ID.", nameof(deviceId));
        }

        ProtocolVersion = protocolVersion;
        DeviceId = deviceId;
        Nonce = nonce.ToArray();
        FeatureBits = featureBits;
        UnixTimeMilliseconds = unixTimeMilliseconds;
    }

    public ushort ProtocolVersion { get; }
    public DeviceId DeviceId { get; }
    public ReadOnlyMemory<byte> Nonce { get; }
    public ulong FeatureBits { get; }
    public long UnixTimeMilliseconds { get; }
}
```

- [ ] **Step 4: Implement strict canonical CBOR**

Create `src/GoDesk.Protocol/Handshake/HelloMessageCodec.cs`:

```csharp
using System.Formats.Cbor;
using GoDesk.Core.Identity;
using GoDesk.Protocol.Control;

namespace GoDesk.Protocol.Handshake;

public static class HelloMessageCodec
{
    public static byte[] Encode(HelloMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(0);
        writer.WriteUInt64(message.ProtocolVersion);
        writer.WriteInt32(1);
        writer.WriteTextString(message.DeviceId.Value);
        writer.WriteInt32(2);
        writer.WriteByteString(message.Nonce.Span);
        writer.WriteInt32(3);
        writer.WriteUInt64(message.FeatureBits);
        writer.WriteInt32(4);
        writer.WriteInt64(message.UnixTimeMilliseconds);
        writer.WriteEndMap();
        return writer.Encode();
    }

    public static HelloMessage Decode(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The hello payload exceeds 64 KiB.");
        }

        try
        {
            var reader = new CborReader(payload, CborConformanceMode.Canonical);
            int? count = reader.ReadStartMap();
            if (count != 5)
            {
                throw new ProtocolFrameException("A hello message must contain five fields.");
            }

            ushort? version = null;
            DeviceId? deviceId = null;
            byte[]? nonce = null;
            ulong? featureBits = null;
            long? unixTimeMilliseconds = null;
            var seenKeys = new HashSet<int>();

            for (int index = 0; index < count.Value; index++)
            {
                int key = reader.ReadInt32();
                if (!seenKeys.Add(key))
                {
                    throw new ProtocolFrameException("A hello field is duplicated.");
                }

                switch (key)
                {
                    case 0:
                        ulong rawVersion = reader.ReadUInt64();
                        if (rawVersion > ushort.MaxValue)
                        {
                            throw new ProtocolFrameException("The protocol version is out of range.");
                        }
                        version = (ushort)rawVersion;
                        break;
                    case 1:
                        string rawDeviceId = reader.ReadTextString();
                        if (rawDeviceId.Length != DeviceId.EncodedLength)
                        {
                            throw new ProtocolFrameException("The device ID length is invalid.");
                        }
                        deviceId = DeviceId.Parse(rawDeviceId);
                        break;
                    case 2:
                        nonce = reader.ReadByteString();
                        if (nonce.Length != 32)
                        {
                            throw new ProtocolFrameException("The hello nonce length is invalid.");
                        }
                        break;
                    case 3:
                        featureBits = reader.ReadUInt64();
                        break;
                    case 4:
                        unixTimeMilliseconds = reader.ReadInt64();
                        break;
                    default:
                        throw new ProtocolFrameException("The hello message contains an unknown field.");
                }
            }

            reader.ReadEndMap();
            if (reader.BytesRemaining != 0)
            {
                throw new ProtocolFrameException("Trailing CBOR data is not allowed.");
            }

            if (version is null || deviceId is null || nonce is null ||
                featureBits is null || unixTimeMilliseconds is null)
            {
                throw new ProtocolFrameException("The hello message is incomplete.");
            }

            return new HelloMessage(
                version.Value,
                deviceId.Value,
                nonce,
                featureBits.Value,
                unixTimeMilliseconds.Value);
        }
        catch (ProtocolFrameException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CborContentException or InvalidOperationException or FormatException)
        {
            throw new ProtocolFrameException("The hello CBOR payload is malformed.");
        }
    }
}
```

- [ ] **Step 5: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 14 total verifications pass.

- [ ] **Step 6: Commit**

```powershell
git add src/GoDesk.Protocol/Handshake verification/GoDesk.Foundation.Verification/Protocol/HelloMessageCodecTests.cs
git commit -m "feat: add canonical hello message codec"
```

### Task 6: Reject recently duplicated operation IDs

**Files:**

- Create: `src/GoDesk.Core/Operations/OperationId.cs`
- Create: `src/GoDesk.Core/Operations/BoundedOperationSet.cs`
- Create: `verification/GoDesk.Foundation.Verification/Core/BoundedOperationSetTests.cs`

- [ ] **Step 1: Write the failing deduplication tests**

Create `verification/GoDesk.Foundation.Verification/Core/BoundedOperationSetTests.cs`:

```csharp
using GoDesk.Core.Operations;

namespace GoDesk.Foundation.Verification.Core;

public sealed class BoundedOperationSetTests
{
    [Fact]
    public void Duplicate_operation_is_rejected()
    {
        var set = new BoundedOperationSet(2);
        var operationId = new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.True(set.TryAdd(operationId));
        Assert.False(set.TryAdd(operationId));
    }

    [Fact]
    public void Oldest_operation_is_evicted_at_capacity()
    {
        var set = new BoundedOperationSet(2);
        var first = new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = new OperationId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var third = new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333"));

        Assert.True(set.TryAdd(first));
        Assert.True(set.TryAdd(second));
        Assert.True(set.TryAdd(third));
        Assert.True(set.TryAdd(first));
    }

    [Fact]
    public void Capacity_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedOperationSet(0));
    }
}
```

- [ ] **Step 2: Run and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because the operation types do not exist.

- [ ] **Step 3: Implement the operation identifier**

Create `src/GoDesk.Core/Operations/OperationId.cs`:

```csharp
namespace GoDesk.Core.Operations;

public readonly record struct OperationId(Guid Value)
{
    public static OperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
```

- [ ] **Step 4: Implement the thread-safe bounded set**

Create `src/GoDesk.Core/Operations/BoundedOperationSet.cs`:

```csharp
namespace GoDesk.Core.Operations;

public sealed class BoundedOperationSet
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Queue<OperationId> _insertionOrder = new();
    private readonly HashSet<OperationId> _known = [];

    public BoundedOperationSet(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool TryAdd(OperationId operationId)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("An operation ID cannot be empty.", nameof(operationId));
        }

        lock (_gate)
        {
            if (!_known.Add(operationId))
            {
                return false;
            }

            _insertionOrder.Enqueue(operationId);
            while (_known.Count > _capacity)
            {
                OperationId oldest = _insertionOrder.Dequeue();
                _known.Remove(oldest);
            }

            return true;
        }
    }
}
```

- [ ] **Step 5: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 17 total verifications pass.

- [ ] **Step 6: Commit**

```powershell
git add src/GoDesk.Core/Operations verification/GoDesk.Foundation.Verification/Core/BoundedOperationSetTests.cs
git commit -m "feat: add bounded operation deduplication"
```

### Task 7: Add stable errors and reusable input guards

**Files:**

- Create: `src/GoDesk.Core/Validation/GoDeskErrorCode.cs`
- Create: `src/GoDesk.Core/Validation/GoDeskException.cs`
- Create: `src/GoDesk.Core/Validation/InputGuard.cs`
- Create: `verification/GoDesk.Foundation.Verification/Core/InputGuardTests.cs`

- [ ] **Step 1: Write the failing boundary tests**

Create `verification/GoDesk.Foundation.Verification/Core/InputGuardTests.cs`:

```csharp
using GoDesk.Core.Validation;

namespace GoDesk.Foundation.Verification.Core;

public sealed class InputGuardTests
{
    [Fact]
    public void UTF8_limit_counts_bytes_not_UTF16_characters()
    {
        GoDeskException exception = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text("名称", maxBytes: 5, fieldName: "name"));

        Assert.Equal(GoDeskErrorCode.InvalidInput, exception.Code);
    }

    [Fact]
    public void Collection_limit_accepts_the_boundary_and_rejects_one_more()
    {
        InputGuard.CollectionCount(10, maxCount: 10, fieldName: "items");
        Assert.Throws<GoDeskException>(() =>
            InputGuard.CollectionCount(11, maxCount: 10, fieldName: "items"));
    }

    [Fact]
    public void Byte_limit_rejects_negative_and_oversized_values()
    {
        Assert.Throws<GoDeskException>(() => InputGuard.ByteLength(-1, 10, "payload"));
        Assert.Throws<GoDeskException>(() => InputGuard.ByteLength(11, 10, "payload"));
    }
}
```

- [ ] **Step 2: Run and verify the compile failure**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: build fails because the validation types do not exist.

- [ ] **Step 3: Implement stable error categories**

Create `src/GoDesk.Core/Validation/GoDeskErrorCode.cs`:

```csharp
namespace GoDesk.Core.Validation;

public enum GoDeskErrorCode
{
    InvalidInput = 1,
    ProtocolViolation = 2,
    AuthenticationFailed = 3,
    PermissionDenied = 4,
    NotFound = 5,
    Busy = 6,
    Timeout = 7,
    Unavailable = 8,
    Conflict = 9,
    InternalFailure = 10,
}
```

Create `src/GoDesk.Core/Validation/GoDeskException.cs`:

```csharp
namespace GoDesk.Core.Validation;

public sealed class GoDeskException : Exception
{
    public GoDeskException()
        : this(GoDeskErrorCode.InternalFailure, "An internal GoDesk failure occurred.")
    {
    }

    public GoDeskException(string safeMessage)
        : this(GoDeskErrorCode.InternalFailure, safeMessage)
    {
    }

    public GoDeskException(string safeMessage, Exception innerException)
        : base(safeMessage, innerException)
    {
        Code = GoDeskErrorCode.InternalFailure;
    }

    public GoDeskException(GoDeskErrorCode code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
    }

    public GoDeskErrorCode Code { get; }
}
```

- [ ] **Step 4: Implement the reusable guards**

Create `src/GoDesk.Core/Validation/InputGuard.cs`:

```csharp
using System.Text;

namespace GoDesk.Core.Validation;

public static class InputGuard
{
    public static void Utf8Text(string value, int maxBytes, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateMaximum(maxBytes, nameof(maxBytes));

        if (Encoding.UTF8.GetByteCount(value) > maxBytes)
        {
            throw Invalid(fieldName, $"must not exceed {maxBytes} UTF-8 bytes");
        }
    }

    public static void CollectionCount(int count, int maxCount, string fieldName)
    {
        ValidateMaximum(maxCount, nameof(maxCount));
        if (count < 0 || count > maxCount)
        {
            throw Invalid(fieldName, $"must contain between 0 and {maxCount} items");
        }
    }

    public static void ByteLength(long length, long maxLength, string fieldName)
    {
        ValidateMaximum(maxLength, nameof(maxLength));
        if (length < 0 || length > maxLength)
        {
            throw Invalid(fieldName, $"must contain between 0 and {maxLength} bytes");
        }
    }

    private static void ValidateMaximum(long maximum, string parameterName)
    {
        if (maximum < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static GoDeskException Invalid(string fieldName, string rule) =>
        new(GoDeskErrorCode.InvalidInput, $"Field '{fieldName}' {rule}.");
}
```

- [ ] **Step 5: Run all verifications**

```powershell
dotnet run --project verification/GoDesk.Foundation.Verification
```

Expected: 20 total verifications pass.

- [ ] **Step 6: Commit**

```powershell
git add src/GoDesk.Core/Validation verification/GoDesk.Foundation.Verification/Core/InputGuardTests.cs
git commit -m "feat: add bounded input validation"
```

### Task 8: Add one-command verification and developer documentation

**Files:**

- Create: `scripts/verify.ps1`
- Create: `docs/development.md`
- Modify: `docs/superpowers/plans/2026-07-22-godesk-implementation-roadmap.md`

- [ ] **Step 1: Add the repository verification script**

Create `scripts/verify.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$goDeskRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $goDeskRoot
try {
    $forbidden = @(
        Get-ChildItem -LiteralPath $goDeskRoot -Directory -Recurse -Force |
            Where-Object { $_.Name -in @('test', 'tests') }
    )

    if ($forbidden.Count -ne 0) {
        throw "Forbidden test directory found: $($forbidden.FullName -join ', ')"
    }

    $ignoreRules = Get-Content -LiteralPath (Join-Path $goDeskRoot '.gitignore') -Encoding utf8
    if ($ignoreRules -notcontains 'test/') {
        throw '.gitignore must contain test/'
    }

    dotnet restore GoDesk.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }

    dotnet build GoDesk.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

    dotnet run `
        --project verification/GoDesk.Foundation.Verification `
        --configuration Release `
        --no-build
    if ($LASTEXITCODE -ne 0) { throw 'foundation verification failed' }
}
finally {
    Pop-Location
}
```

- [ ] **Step 2: Document the exact local workflow**

Create `docs/development.md`:

````markdown
# GoDesk 开发环境

## 前置条件

- Windows 11 22H2 或更高版本；
- x64；
- .NET SDK 10.0.302；
- Git 2.53 或更高版本；
- 普通开发和验证使用非管理员 PowerShell。

安装 SDK：

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

验证：

```powershell
dotnet --version
git --version
```

## 构建和验证

```powershell
& ./scripts/verify.ps1
```

验证代码只能放在 `verification/`，性能程序只能放在 `benchmarks/`。项目不得创建 `test/` 或 `tests/`，并且 `.gitignore` 必须保留 `test/`。

## 依赖更新

依赖版本集中在 `Directory.Packages.props`。更新后先执行普通 `dotnet restore` 更新 `packages.lock.json`，审查包来源、许可证和安全告警，再执行 `./scripts/verify.ps1`。不得在未更新锁文件的情况下提交包版本变化。
````

- [ ] **Step 3: Run the complete verification from a clean build state**

Run:

```powershell
$goDeskRoot = (Resolve-Path -LiteralPath .).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
$buildDirs = @(
    Get-ChildItem -LiteralPath $goDeskRoot -Directory -Recurse -Force |
        Where-Object { $_.Name -in @('bin', 'obj') }
)

foreach ($buildDir in $buildDirs) {
    $resolvedBuildDir = (Resolve-Path -LiteralPath $buildDir.FullName).Path
    $requiredPrefix = $goDeskRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedBuildDir.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the workspace: $resolvedBuildDir"
    }
}

foreach ($buildDir in $buildDirs) {
    Remove-Item -LiteralPath $buildDir.FullName -Recurse -Force
}

& ./scripts/verify.ps1
```

Expected: locked restore succeeds, Release build reports zero warnings and errors, 20 verifications pass, and the forbidden-folder check passes.

- [ ] **Step 4: Mark phase 1 complete only after evidence is recorded**

In `docs/superpowers/plans/2026-07-22-godesk-implementation-roadmap.md`, change the stage 1 heading to:

```markdown
### 阶段 1：核心基础（已完成）
```

Add immediately below its artifact list:

```markdown
验收证据：`scripts/verify.ps1` 在 Release 配置下完成锁定还原、零警告构建和 20 项基础验证。
```

- [ ] **Step 5: Commit the verified foundation**

```powershell
git add scripts/verify.ps1 docs/development.md docs/superpowers/plans/2026-07-22-godesk-implementation-roadmap.md src verification
git add -- '**/packages.lock.json'
git commit -m "build: add repeatable foundation verification"
git status --short
```

Expected: commit succeeds and `git status --short` prints no output.

## Plan completion gate

Do not begin device key storage or pairing until all of the following are true:

- `dotnet --version` resolves through `global.json` without preview fallback;
- locked restore succeeds;
- Release build has zero warnings and errors;
- all 20 verifications pass;
- no `test/` or `tests/` directory exists;
- `.gitignore` contains `test/`;
- permission bit values and default administrator-terminal exception match both approved specifications;
- the working tree is clean.

## Primary references

- [.NET 10 download](https://dotnet.microsoft.com/download/dotnet/10.0)
- [System.Formats.Cbor 10.0.10](https://www.nuget.org/packages/System.Formats.Cbor/10.0.10)
- [xUnit v3 3.2.2](https://xunit.net/releases/v3/3.2.2)
- [Microsoft Testing Platform with xUnit v3](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
