# Explicit release inputs: adding a C# file never silently expands production.
function Get-OzlProductionSources {
    param([Parameter(Mandatory=$true)][string]$ProjectDirectory)
    $names = @(
        'BuddyLaunchArguments.cs',
        'BuddyPresentationBridge.cs',
        'BuddyTakeover.cs',
        'BundledAssets.cs',
        'ChinesePatch.cs',
        'ComponentBatch.cs',
        'ComponentCache.cs',
        'ComponentEvidence.cs',
        'ComponentHelp.cs',
        'ComponentPage.cs',
        'CoreComponents.cs',
        'CoreDeployment.cs',
        'CoreReleaseProtocol.cs',
        'CoreReleaseSources.cs',
        'CoreUpdaterProgram.cs',
        'CoreUpdateService.cs',
        'CoreUpdateTransaction.cs',
        'DirectorySelection.cs',
        'DisplayCache.cs',
        'DlssInstaller.cs',
        'FeedbackService.cs',
        'FeedbackUpload.cs',
        'FeedbackController.cs',
        'ForceTakeover.cs',
        'GameDiagnostics.cs',
        'GameLogModels.cs',
        'GameLogAnalyzer.cs',
        'GameLogAnalysisService.cs',
        'GameLogAnalysisController.cs',
        'GameLogAnalysisHost.cs',
        'GameDiscovery.cs',
        'GameFavorites.cs',
        'GameProcessGuard.cs',
        'GameStartupModels.cs',
        'GameStartupComponent.cs',
        'StartupOptionsController.cs',
        'GameStartupMonitor.cs',
        'GameStartupPresentation.cs',
        'GameStartupPresenter.cs',
        'GameStartupHost.cs',
        'GameStartupWindow.cs',
        'GameVersionProbe.cs',
        'GoldControls.cs',
        'GuideController.cs',
        'HardwareProbe.cs',
        'HelpContent.cs',
        'HelpPage.cs',
        'InstallerExperience.cs',
        'InstallerForm.cs',
        'InstallerProgram.cs',
        'InstallerShortcut.cs',
        'InstallWorkflow.cs',
        'JavaApprovals.cs',
        'LauncherContact.cs',
        'LauncherDiagnostics.cs',
        'LauncherFeatures.cs',
        'LauncherJournal.cs',
        'LauncherLaunch.cs',
        'LauncherMaintenance.cs',
        'LauncherNativeState.cs',
        'LauncherPictures.cs',
        'LauncherRuntime.cs',
        'LauncherShell.cs',
        'LauncherStatusMetadata.cs',
        'LauncherTheme.cs',
        'LauncherWorkshopRebuild.cs',
        'LaunchPreflight.cs',
        'LaunchProfiles.cs',
        'LegalContent.cs',
        'LocalAddon.cs',
        'ManagedBackgroundWorker.cs',
        'ModConfigurations.cs',
        'ModernAssets.cs',
        'ModernController.cs',
        'LibraryController.cs',
        'LibraryHost.cs',
        'RecommendedServers.cs',
        'SaveBackups.cs',
        'SaveEnvironments.cs',
        'SaveEnvironmentRuntime.cs',
        'SaveEnvironmentController.cs',
        'SaveEnvironmentLaunch.cs',
        'SnapshotPreparation.cs',
        'SnapshotModChoices.cs',
        'SnapshotStorage.cs',
        'SnapshotFileSystem.cs',
        'SnapshotWorkshopResolver.cs',
        'SnapshotPreviewWorkflow.cs', 'SnapshotDownloadWorkflow.cs',
        'ModWorkshopController.cs',
        'SafehouseCatalog.cs',
        'SafehouseTransferFormat.cs',
        'SafehouseTransfer.cs',
        'JavaComponents.cs',
        'VersionLibrary.cs',
        'SteamVersionWorkflow.cs',
        'ModernDispatchQueue.cs',
        'ModernHost.cs',
        'ModernHostPolicy.cs',
        'OrangeProgress.cs',
        'OrangeUI.cs',
        'PlayProfiles.cs',
        'PlayProfileTransaction.cs',
        'PlaytimeTracker.cs',
        'PortableDeployment.cs',
        'PresentationBridge.cs',
        'Program.cs',
        'ReticlePage.cs',
        'ReticleSettings.cs',
        'RuntimeEvidence.cs',
        'SaveDeletion.cs',
        'SaveProfiles.cs',
        'ServerLaunchTranslations.cs',
        'ServerProfiles.cs',
        'ServerPublicRules.cs',
        'ServerWorkshopCatalog.cs',
        'ServerWorkshopPreparation.cs',
        'SettingsChinese.cs',
        'SettingsPage.cs',
        'ShaderPage.cs',
        'ShaderSelection.cs',
        'ShootClient.cs',
        'ShootClientPage.cs',
        'StatusDashboard.cs',
        'SteamLauncherNative.cs',
        'SteamLauncherServices.cs',
        'SteamLaunchOptions.cs',
        'SteamWorkshop.cs',
        'ViewpointAddons.cs',
        'ViewpointSettings.cs',
        'WorkshopUpdate.cs',
        'WorkshopWorker.cs'
    )
    foreach ($name in $names) {
        $source = [IO.Path]::GetFullPath((Join-Path $ProjectDirectory $name))
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing production source: $name" }
        $source
    }
}

function Get-OzlVelopackSources {
    param([Parameter(Mandatory=$true)][string]$ProjectDirectory)
    Get-OzlProductionSources -ProjectDirectory $ProjectDirectory
    foreach ($name in @('DistributionRuntime.cs','VelopackRuntime.cs','VelopackFeed.cs','VelopackUpdateBackend.cs','ApplyLeaseWorker.cs','VelopackApplyLease.cs','ReleaseCheckpoint.cs','NativeApplyGuard.cs')) {
        $source = Join-Path $ProjectDirectory $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing Velopack production source: $name" }
        $source
    }
}

function Get-OzlDiagnosticSources {
    param([Parameter(Mandatory=$true)][string]$ProjectDirectory)
    Get-OzlProductionSources -ProjectDirectory $ProjectDirectory
    # DiagnosticSelfTests is added explicitly below, not a second time via the glob.
    Get-ChildItem -LiteralPath $ProjectDirectory -Filter '*Tests.cs' -File |
        Where-Object { $_.Name -ne 'DiagnosticSelfTests.cs' } |
        Sort-Object Name | ForEach-Object { $_.FullName }
    foreach ($name in @('LauncherTestPaths.cs', 'DiagnosticProgram.cs', 'DiagnosticSelfTests.cs')) {
        $source = [IO.Path]::GetFullPath((Join-Path $ProjectDirectory $name))
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing diagnostic source: $name" }
        $source
    }
}
