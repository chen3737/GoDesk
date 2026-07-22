Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

Push-Location -LiteralPath $repositoryRoot
try {
    $skippedDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($directoryName in @('.git', '.worktrees', '.vs', 'bin', 'obj', 'artifacts', 'TestResults')) {
        [void]$skippedDirectoryNames.Add($directoryName)
    }

    $pendingDirectories = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
    $pendingDirectories.Push((Get-Item -LiteralPath $repositoryRoot -Force))
    $forbiddenDirectories = [System.Collections.Generic.List[string]]::new()

    while ($pendingDirectories.Count -gt 0) {
        $currentDirectory = $pendingDirectories.Pop()
        foreach ($childDirectory in Get-ChildItem -LiteralPath $currentDirectory.FullName -Directory -Force) {
            if ($childDirectory.Name -in @('test', 'tests')) {
                $forbiddenDirectories.Add($childDirectory.FullName)
                continue
            }

            if ($skippedDirectoryNames.Contains($childDirectory.Name)) {
                continue
            }

            if (($childDirectory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                continue
            }

            $pendingDirectories.Push($childDirectory)
        }
    }

    if ($forbiddenDirectories.Count -gt 0) {
        $paths = ($forbiddenDirectories | Sort-Object) -join [Environment]::NewLine
        throw "Forbidden test directory detected:$([Environment]::NewLine)$paths"
    }

    $gitIgnorePath = Join-Path $repositoryRoot '.gitignore'
    $gitIgnoreLines = [System.IO.File]::ReadAllLines($gitIgnorePath)
    if ($gitIgnoreLines -notcontains 'test/') {
        throw '.gitignore must contain the exact line test/.'
    }

    dotnet restore (Join-Path $repositoryRoot 'GoDesk.sln') --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore failed.'
    }

    dotnet build (Join-Path $repositoryRoot 'GoDesk.sln') --configuration Release --no-restore --tl:off
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }

    $verificationProject = Join-Path $repositoryRoot 'verification\GoDesk.Foundation.Verification\GoDesk.Foundation.Verification.csproj'
    dotnet run --project $verificationProject --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        throw 'foundation verification failed.'
    }
}
finally {
    Pop-Location
}
