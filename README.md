## Worker Role test

Simple worker role to mount required number of cloud drives. 7zip the binaries that you need deployed and place at
WorkerRole1\Binaries\Binaries.7z. Ensure it is set to copy always.

### To Run
* Copy ServiceConfiguration.Local.cscfg to ServiceConfiguration.Cloud.cscfg
* Set StorageAccount in Settings to the required storage account credentials
* Set required drive size by setting DriveSizeInGB in Settings
* To mount more than 1 drive 
** Set NoOfDrives in Settings
** Add new LocalStorage settings named LocalStorage2, LocalStorage3 etc
* Choose required instance size
* Build and deploy

