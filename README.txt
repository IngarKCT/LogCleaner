LogCleaner

A small Windows service to clean up directories with log files.

It will monitor the directories configured in
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LogCleaner\CleanDirectories

If the total size of all files in the directory exceeds CleanSizeMegabytes MiB,
it will delete files older than CleanAgeMinutes, oldest first, until the desired
size has been reached.

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
		Timer interval		
		
	CleanAgeMinutes
		Only delete files with a modification time older than this. Can be 0!
		
	CleanSizeMegabytes
		Only delete files in derectories with a total size in megabytes (MiB) larger than this. Can be 0!
		  
	CleanDirectories
		List of directories to watch. Every minute, the service will check each directory for size, and delete old files accordingly.
		If both CleanAgeMinutes and CleanSizeMegabytes are 0, the service will essentially just delete all files in them.

