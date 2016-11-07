for %%i in ("%~dp0..") do set "folder=%%~fi"
echo %folder%
SET parentPath=%folder%\bin\Debug
sc create AstekBatchService binPath= "%parentPath%\AstekBatchService.exe" DisplayName= "Astek Batch Service" start= auto
sc description AstekBatchService "Lauches jobs at regular interval for ASTEK"