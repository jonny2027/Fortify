/*
 * Copyright 1995, 2019 Perforce Software.  All rights reserved.
 *
 * This file is part of Perforce - the FAST SCM System.
 */

/*
 * msgserver2.h - More definitions of errors for server subsystem.
 * The MsgServer2 class contains overflow messages from MsgServer.
 */

class MsgServer2 {

    public:
	static ErrorId ExtensionDeletePreview;
	static ErrorId ExtensionInstallPreview;
	static ErrorId WarnPreviewMode;
	static ErrorId UseReopen2;
	static ErrorId UseReopen3;
	static ErrorId StgKeyMiss;
	static ErrorId StgBadCount;
	static ErrorId StgOrphan;
	static ErrorId UseResolve2;
	static ErrorId UseOpened3;
	static ErrorId StorageUpgradeInProgress;
	static ErrorId StorageEdgeFailure;
	static ErrorId UseStreamlog;
	static ErrorId SubmitNoBgXferTarget;
	static ErrorId SubmitBgXferNoConfig;
	static ErrorId SubmitBgNotEdge;
	static ErrorId SubmitBgNotConfigured;
	static ErrorId UsePullt;
	static ErrorId SubmitNoBackgroundThreads;
	static ErrorId StorageNoUpgrade;
	static ErrorId FailoverForced;
	static ErrorId FailoverWriteServerID;
	static ErrorId FailoverServerIDBad;
	static ErrorId FailoverMasterTooOld;
	static ErrorId FailoverCfgCommit;
	static ErrorId FailoverUnCfgCommit;
	static ErrorId FailoverDetails;
	static ErrorId FailoverNeedYOK;
	static ErrorId FailoverWriteFConfigs;
	static ErrorId FailoverDeleteFConfigs;
	static ErrorId FailoverFConfigsBad;
	static ErrorId FailoverFBackOldWarn;
	static ErrorId ServerIDReused;
	static ErrorId StorageRestoreDigest;
	static ErrorId xuUpstream;
	static ErrorId xuAtStart;
	static ErrorId xuUpstream2;
	static ErrorId xuAtStart2;
	static ErrorId JournalRequired;
	static ErrorId ShelvedStreamDeleted;
	static ErrorId NoShelvedStreamDelete;
	static ErrorId DescribeShelvedStream;
	static ErrorId ShelveCompleteStream;
	static ErrorId ShelveCompleteBoth;
	static ErrorId ShelveDeleteJustFiles;
	static ErrorId StorageWaitComplete;
	static ErrorId ExtensionNameCfgUniq;
	static ErrorId UpgradeWarning;
	static ErrorId BadUpLbr;
	static ErrorId MissingLbr;
	static ErrorId NoStreamFieldsResolve;
	static ErrorId UseDiffA;
	static ErrorId UseDiff2A;
	static ErrorId NoStreamDefaultShelve;
	static ErrorId NoStreamShelve;
	static ErrorId ShelveStreamBegin;
	static ErrorId StreamShelfOccupied;
	static ErrorId StreamShelfReadOnly;
	static ErrorId ServiceNotSupported;
	static ErrorId NoRplMissingMandatory;
	static ErrorId UnexpectedRotJnlChange;
	static ErrorId RunExtErrorWarning;
	static ErrorId RunExtErrorFailed;
	static ErrorId RunExtErrorFatal;
	static ErrorId StorageCleanupWarn;
	static ErrorId VerifyDataProblem;
	static ErrorId VerifyData;
	static ErrorId ExtensionPostInstallMsg;
	static ErrorId UseStreamSpec;
	static ErrorId UseLbrScan;
	static ErrorId LbrScanBusy;
	static ErrorId LbrScanBadDepot;
	static ErrorId LbrScanPathInUse;
	static ErrorId LbrScanUnderPath;
	static ErrorId LbrScanNotFound;
	static ErrorId LbrScanBadPath;
	static ErrorId StorageZeroRefClean;
	static ErrorId StorageZeroCount;
	static ErrorId StorageDupZero;
	static ErrorId ExtensionRunFunction;
	static ErrorId StringTooLarge;
	static ErrorId ExtensionNonUTF8Data;
	static ErrorId StorageShareRep;
	static ErrorId StorageSingle;
	static ErrorId StorageSymlink;
	static ErrorId ExtMissingCfg;
	static ErrorId ExtMissingCfgEvent;
	static ErrorId MissingMovedFilesHeader;
	static ErrorId MissingMovedFile;
	static ErrorId UpdatedLbrType;
	static ErrorId InvalidExtName;
	static ErrorId DigestFail;
	static ErrorId EndOfStorePhase1;
	static ErrorId DigestFail2;
	static ErrorId NoFilesInSvrRtForVal;
	static ErrorId UseHeartbeat;
	static ErrorId UseHeartbeatWait;
	static ErrorId UseHeartbeatInterval;
	static ErrorId UseHeartbeatCount;
	static ErrorId HeartbeatNoTarget;
	static ErrorId HeartbeatExiting;
	static ErrorId HeartbeatAccessFailed;
	static ErrorId HeartbeatMaxWait;
	static ErrorId HeartbeatTargetTooOld;
	static ErrorId SkippedKeyed;
	static ErrorId DuplicateCertificate;
	static ErrorId ExtensionCertInstallSuccess;
	static ErrorId ExtensionCertInstallPreview;
	static ErrorId ExtensionCertDelSuccess;
	static ErrorId ExtensionCertDelPreview;
	static ErrorId ExtensionCertMissing;
	static ErrorId ExtensionNotSigned;
	static ErrorId ExtensionSignUntrusted;
	static ErrorId ClientTooOldToPackage;
	static ErrorId StreamSpecIntegPend;
	static ErrorId StreamSpecInteg;
	static ErrorId BadExternalAddr;
	static ErrorId ShelvePromotedStream;
	static ErrorId ShelvePromotedBoth;
	static ErrorId StreamSpecPermsDisabled;
	static ErrorId UseDbSchema;
	static ErrorId UseStreamSpecParentView;
	static ErrorId StgOrphanIndex;
	static ErrorId StgIndexMismatch;
	static ErrorId StgOrphanStart;
	static ErrorId StgOrphanPause;
	static ErrorId StgOrphanRestart;
	static ErrorId StgOrphanCancelled;
	static ErrorId StgOrphanWait;
	static ErrorId StgScanHeader;
	static ErrorId StgNoScans;
	static ErrorId UpgradeInfo;
	static ErrorId UpgradeComplete;
	static ErrorId UpgradeNeeded;
	static ErrorId UpgradeRplUnknown;
	static ErrorId UseUpgrades;
	static ErrorId BadPRoot;
	static ErrorId FailedToUpdUnExpKtextDigest;
	static ErrorId StreamHasParentView; 
	static ErrorId StreamParentViewChanged;
	static ErrorId StreamPVSpecOpen;
	static ErrorId UpdateDigestReport;
	static ErrorId UpdateDigestProgress;
	static ErrorId StreamPVVirtualOnlyInh;
	static ErrorId SSInhPVIntegNotDone;
	static ErrorId StreamPVTaskOnlyInh;
	static ErrorId SSIntegNotCurStream;
	static ErrorId ExtCfgMissing;
	static ErrorId NoUnshelveVirtIntoNoInh;
	static ErrorId NoUnshelveNoInhIntoVirt;
	static ErrorId ReplicaSharedConfig;
	static ErrorId RtMonitorDisabled;
	static ErrorId UseMonitorRT;
	static ErrorId SwitchStreamUnrelated;
	static ErrorId PurgeReportArchive;
	static ErrorId ReplicaLag;
	static ErrorId InfoProxyServerID;
	static ErrorId MoveReaddIntegConflictResolveWarn;
	static ErrorId ShelveArchiveInUse;
	static ErrorId ShelveDupDiff;
	static ErrorId ShelveNotPromoted;
	static ErrorId VerifyRepairKtext;
	static ErrorId VerifyRepairNone;
	static ErrorId VerifyRepairConflict;
	static ErrorId VerifyRepairSnapped;
	static ErrorId VerifyRepairCopied;
	static ErrorId UseVerifyR;
	static ErrorId InfoCommitServer;
	static ErrorId InfoEdgeServer;
	static ErrorId MovePairSplit;
	static ErrorId UseTopology;
	static ErrorId TopologyOnCurrentSvr;
	static ErrorId FileNoMatchStgDigest;
	static ErrorId FileNoMatchStgSize;
	static ErrorId UseStreams2;
	static ErrorId UnknownContext;
	static ErrorId RplTooBig;
	static ErrorId RplReduced;
	static ErrorId FailbackStandbyRestrict;
	static ErrorId UseP4dF;
	static ErrorId P4dFBadMaster;
	static ErrorId P4dFRefuseMissing;
	static ErrorId P4dFFConfigsMissing;
	static ErrorId P4dFStandbyNotStandby;
	static ErrorId P4dFBadRplFrom;
	static ErrorId P4dFPreview;
	static ErrorId P4dFStarting;
	static ErrorId P4dFOK;
	static ErrorId P4dFSuccess;
	static ErrorId P4dFFailbackNotRun;
	static ErrorId P4dFRestrictedStart;
	static ErrorId IntegTaskNoDirect;
	static ErrorId ExtraPxcIDUsage;
	static ErrorId BadPxcExtraFlag;
	static ErrorId FailbackWriteServerID;
	static ErrorId UseFailback;
	static ErrorId FailbackMasterTooOld;
	static ErrorId FailbackFConfigsMissing;
	static ErrorId UseFailoverB;
	static ErrorId FailbackStandbyNotRestricted;
	static ErrorId FailbackNeedsFm;
	static ErrorId FailbackNeedsFs;
	static ErrorId FailbackStandbyBad;
	static ErrorId FailoverRunFailback;
	static ErrorId LbrDeletionFailed;
	static ErrorId ThreadMustBeNumeric;
	static ErrorId ThreadBiggerMinusTwo;
	static ErrorId NoStdoutPDump;
	static ErrorId NoDirPDump;
	static ErrorId BadJournalSubOpt;
	static ErrorId LicenseExpiryWarning;
	static ErrorId InfoProxyCacheRoot;
	static ErrorId InfoProxyRoot;
	static ErrorId UpgradeFeatureUnknown;
	static ErrorId NoParallelMflag;
	static ErrorId NoParallelCflag;
	static ErrorId NoParallelSflag;
	static ErrorId BadRecoverDir;
	static ErrorId BadRecoverFileName;
	static ErrorId NoStorageMflag;
	static ErrorId NoParallelSSflag;
	static ErrorId SyncWStreamViewChange;
	static ErrorId SyncWStreamViewHeadChange;
	static ErrorId StreamAtChangeVsStreamViewChange;
	static ErrorId UseRenameClient;
	static ErrorId FromToSame;
	static ErrorId ClientNotExist;
	static ErrorId NewClientExists;
	static ErrorId RenameClientUnloaded;
	static ErrorId RenameClientPartitioned;
	static ErrorId ClientOpenStream;
	static ErrorId RenameNeedsMaster;
	static ErrorId RenameNeedsCommit;
	static ErrorId RenameNoPromoted;
	static ErrorId ClientRenamed;
	static ErrorId ClientNeedsRecover;
	static ErrorId ClientRenameFailed;
	static ErrorId ClientNotLocal;
	static ErrorId RenameCommitOld;
	static ErrorId UseRenameWorkspace;
	static ErrorId MultiFilePara;
	static ErrorId PasswordChangeSU;
	static ErrorId RenameClientNotAllowed;
	static ErrorId RenameClientAdminSuper;
	static ErrorId RenameClientSuper;
	static ErrorId RenameClientNotOwner;
	static ErrorId PreserveChangeNumberConflict;
	static ErrorId DistributionBlockSubmit;
	static ErrorId DistributionServerOverrides;
	static ErrorId MissingParallelFile;
	static ErrorId AltSyncNoSupport;
	static ErrorId AltSyncNotConfigured;
	static ErrorId AltSyncNoVersion;
	static ErrorId AltSyncBadVersion;
	static ErrorId AltSyncActive;
	static ErrorId UpgradeAuthDown;
	static ErrorId UpgradeAuth;
	static ErrorId UpgradeAuthNoLicense;
	static ErrorId UpgradeAuthMaintenance;
	static ErrorId UpgradeAuthRestricted;
	static ErrorId LowResourceTerm;
	static ErrorId ImpatientPauseTerm;
	static ErrorId TooManyPausedTerm;
	static ErrorId TooMuchResourceMonitor;
	static ErrorId UseAdminResourceMonitor;
	static ErrorId StartupCapabilities;
	static ErrorId BadPressureThresholds;
	static ErrorId PopulateSparseStreamDesc;
	static ErrorId NonResidentOpenMustSync;
	static ErrorId BadJField;
	static ErrorId TooManySparseStreamFiles;
	static ErrorId TraitDepotNotConfigured;
	static ErrorId TraitDepotNotForCommit;
	static ErrorId NoValidIPOrMACAddresses;
	static ErrorId ValidIPv4Address;
	static ErrorId ValidIPv6Address;
	static ErrorId ValidMACAddress;
	static ErrorId NoTraitValueInDepot;
	static ErrorId Types64Warn;
	static ErrorId Types64Err;
	static ErrorId ConfigureSetComment;
	static ErrorId ProxyClearCacheNotSet;
	static ErrorId StreamMustBeSparse;
	static ErrorId InvalidDestStreamType;
	static ErrorId UseStreamConvertSparse;
	static ErrorId ConfigureMandatoryComment;
	static ErrorId NoJournalRotateWarning;
	static ErrorId CommitVerifyNoExternalAddress;
	static ErrorId SwitchStreamFailedReconcile;
	static ErrorId MissingConfigDbFile;
	static ErrorId MissingPcdir;
	static ErrorId NoStreamSpecEditStreamAtChangeClient;
	static ErrorId NoStreamSpecUnshelveStreamAtChangeClient;
} ;