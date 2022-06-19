namespace ImageMounter.IO.Native.Enum
{
    public enum SystemInformationClass : uint
    {
        SystemBasicInformation  // ' 0x002C 
,
        SystemProcessorInformation  // ' 0x000C 
,
        SystemPerformanceInformation    // ' 0x0138 
,
        SystemTimeInformation   // ' 0x0020 
,
        SystemPathInformation   // ' Not implemented 
,
        SystemProcessInformation    // ' 0x00C8+ per process 
,
        SystemCallInformation   // ' 0x0018 + (n * 0x0004) 
,
        SystemConfigurationInformation  // ' 0x0018 
,
        SystemProcessorCounters // ' 0x0030 per cpu 
,
        SystemGlobalFlag        // ' 0x0004 (fails If size != 4) 
,
        SystemCallTimeInformation   // ' Not implemented 
,
        SystemModuleInformation // ' 0x0004 + (n * 0x011C) 
,
        SystemLockInformation   // ' 0x0004 + (n * 0x0024) 
,
        SystemStackTraceInformation // ' Not implemented 
,
        SystemPagedPoolInformation  // ' checked build only 
,
        SystemNonPagedPoolInformation   // ' checked build only 
,
        SystemHandleInformation // ' 0x0004 + (n * 0x0010) 
,
        SystemObjectTypeInformation // ' 0x0038+ + (n * 0x0030+) 
,
        SystemPageFileInformation   // ' 0x0018+ per page file 
,
        SystemVdmInstemulInformation    // ' 0x0088 
,
        SystemVdmBopInformation // ' invalid info Class 
,
        SystemCacheInformation  // ' 0x0024 
,
        SystemPoolTagInformation    // ' 0x0004 + (n * 0x001C) 
,
        SystemInterruptInformation  // ' 0x0000 Or 0x0018 per cpu 
,
        SystemDpcInformation    // ' 0x0014 
,
        SystemFullMemoryInformation // ' checked build only 
,
        SystemLoadDriver        // ' 0x0018 Set mode only 
,
        SystemUnloadDriver      // ' 0x0004 Set mode only 
,
        SystemTimeAdjustmentInformation // ' 0x000C 0x0008 writeable 
,
        SystemSummaryMemoryInformation  // ' checked build only 
,
        SystemNextEventIdInformation    // ' checked build only 
,
        SystemEventIdsInformation   // ' checked build only 
,
        SystemCrashDumpInformation  // ' 0x0004 
,
        SystemExceptionInformation  // ' 0x0010 
,
        SystemCrashDumpStateInformation // ' 0x0004 
,
        SystemDebuggerInformation   // ' 0x0002 
,
        SystemContextSwitchInformation  // ' 0x0030 
,
        SystemRegistryQuotaInformation  // ' 0x000C 
,
        SystemAddDriver     // ' 0x0008 Set mode only 
,
        SystemPrioritySeparationInformation // ' 0x0004 Set mode only 
,
        SystemPlugPlayBusInformation    // ' Not implemented 
,
        SystemDockInformation   // ' Not implemented 
,
        SystemPowerInfo     // ' 0x0060 (XP only!) 
,
        SystemProcessorSpeedInformation // ' 0x000C (XP only!) 
,
        SystemTimeZoneInformation   // ' 0x00AC 
,
        SystemLookasideInformation  // ' n * 0x0020 
,
        SystemSetTimeSlipEvent,
        SystemCreateSession // ' Set mode only 
,
        SystemDeleteSession // ' Set mode only 
,
        SystemInvalidInfoClass1 // ' invalid info Class 
,
        SystemRangeStartInformation // ' 0x0004 (fails If size != 4) 
,
        SystemVerifierInformation,
        SystemAddVerifier,
        SystemSessionProcessesInformation   // ' checked build only 
,
        MaxSystemInfoClass
    }

}
