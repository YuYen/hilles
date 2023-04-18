reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\w32time\TimeProviders\NtpServer" /v Enabled /t REG_DWORD /d 1 /f
net stop w32time
net start w32time
