LogCleaner

A small Windows service to clean up directories with log files.

It will monitor the directories configured in
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LogCleaner\CleanDirectories

If the total size of all files in the directory and its subdirectories exceeds
CleanSizeMegabytes MiB, it will delete files older than CleanAgeMinutes, 
oldest first, until the desired size has been reached.

The service will also remove empty subdirectories.

If no configuration is found on startup, the service will create the necessary registry keys.
Except for CleanIntervalSeconds, configuration changes take effect immediatly.
Changing CleanIntervalSeconds requires a restart.

HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LogCleaner
	
	LogLevel 
		Messages are sent the Application event log	
		0 = Essential messages only
		1 = Information
		2 = Debug
		
	TestMode
		0 = Delete files
		any other value = Do not delete files
		
	CleanIntervalSeconds
		Timer interval in seconds.
		If set to 0 or negative, it will revert to 300 seconds.
		
	CleanAgeMinutes
		Only delete files with a modification time older than this.
		If set to 0, file age is ignored.
		If set, young files are preserved.
		
	CleanSizeMegabytes
		Only delete files in directories with a total size in megabytes (MiB) larger than this.
		If set to 0, directory size is ignored and all files are potential delete candidates.
				  
	CleanDirectories
		List of directories to watch. 
		Every CleanIntervalSeconds, the service will check each directory for size, 
		and delete old files accordingly. 

Example configurations
	
	Keep directory size under 5 MiB:
		CleanAgeMinutes=0
		CleanSizeMegabytes=5
	
	Delete files older than an hour:
		CleanAgeMinutes=60
		CleanSizeMegabytes=0
		
	Try to keep directory size under 5 MiB by deleting files older than an hour:
		CleanAgeMinutes=60
		CleanSizeMegabytes=5
		
	Delete everything:
		CleanAgeMinutes=0
		CleanSizeMegabytes=0
