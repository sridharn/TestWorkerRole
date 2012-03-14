using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudDrive[] drives = null;

        public override void Run()
        {
            Trace.WriteLine("$projectname$ entry point called", "Information");

            while (true)
            {
                Thread.Sleep(10000);
                Trace.WriteLine("Working", "Information");
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });

            int noOfDrives = 1;
            int sizeInGb = 5;

            try
            {
                var noOfDrivesString = RoleEnvironment.GetConfigurationSettingValue("NoOfDrives");
                var parsed = int.TryParse(noOfDrivesString, out noOfDrives);
                if (!parsed)
                {
                    noOfDrives = 1;
                }
            }
            catch { }

            try
            {
                var sizeInGBString = RoleEnvironment.GetConfigurationSettingValue("DriveSizeInGB");
                var parsed = int.TryParse(sizeInGBString, out sizeInGb);
                if (!parsed)
                {
                    sizeInGb = 5;
                }
            }
            catch { }

            drives = new CloudDrive[noOfDrives];

            for (int i = 0; i < noOfDrives; i++)
            {
                try
                {
                    drives[i] = MountCloudDrive(
                        "StorageAccount",
                        "container"+i,
                        "blob"+i,
                        "LocalStorage"+i,
                        sizeInGb * 1024);
                    //drive2 = MountCloudDrive(
                    //    "StorageAccount2",
                    //    "container2",
                    //    "blob2",
                    //    "LocalStorage2");

                }
                catch (Exception e)
                {
                    Trace.TraceError("Exception in onstop - unmount of drive");
                    Trace.TraceError(e.Message);
                    Trace.TraceError(e.StackTrace);
                    throw;
                }
            }

            return base.OnStart();
        }

        public override void OnStop()
        {
            try
            {
                for (int i = 0; i < drives.Length; i++)
                {
                    Trace.TraceInformation("Unmount called on drive");
                    if (drives[i] != null)
                    {
                        drives[i].Unmount();
                    }
                    Trace.TraceInformation("Unmount completed on drive");
                }
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                Trace.TraceWarning("Exception in onstop - unmount of drive");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
            }

            base.OnStop();
        }

        private CloudDrive MountCloudDrive(
            string storageAccountConnectionString,
            string containerName,
            string blobName,
            string localCachePath,
            int sizeInMB)
        {
            CloudDrive drive = null;
            Trace.TraceInformation("Mounting cloud drive for dir");

            CloudStorageAccount storageAccount = CloudStorageAccount.FromConfigurationSetting(storageAccountConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();

            Trace.TraceInformation("Get container");
            // this should be the name of your replset
            var driveContainer = blobClient.GetContainerReference(containerName);

            // create blob container (it has to exist before creating the cloud drive)
            try
            {
                driveContainer.CreateIfNotExist();
            }
            catch (Exception e)
            {
                Trace.TraceInformation("Exception when creating container");
                Trace.TraceInformation(e.Message);
                Trace.TraceInformation(e.StackTrace);
            }

            var mongoBlobUri = blobClient.GetContainerReference(containerName).GetPageBlobReference(blobName).Uri.ToString();
            Trace.TraceInformation(string.Format("Blob uri obtained {0}", mongoBlobUri));

            // create the cloud drive
            drive = storageAccount.CreateCloudDrive(mongoBlobUri);
            try
            {
                drive.Create(sizeInMB);
            }
            catch (Exception e)
            {
                // exception is thrown if all is well but the drive already exists
                Trace.TraceInformation("Exception when creating cloud drive. safe to ignore");
                Trace.TraceInformation(e.Message);
                Trace.TraceInformation(e.StackTrace);

            }

            Trace.TraceInformation("Initialize cache");
            var localStorage = RoleEnvironment.GetLocalResource(localCachePath);

            CloudDrive.InitializeCache(localStorage.RootPath.TrimEnd('\\'),
                localStorage.MaximumSizeInMegabytes);

            // mount the drive and get the root path of the drive it's mounted as
            try
            {
                Trace.TraceInformation(string.Format("Trying to mount blob as azure drive on {0}",
                    RoleEnvironment.CurrentRoleInstance.Id));
                var driveLetter = drive.Mount(localStorage.MaximumSizeInMegabytes,
                    DriveMountOptions.None);
                Trace.TraceInformation(string.Format("Write lock acquired on azure drive, mounted as {0}, on role instance",
                    driveLetter, RoleEnvironment.CurrentRoleInstance.Id));
            }
            catch (Exception e)
            {
                Trace.TraceWarning("could not acquire blob lock.");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
                throw;
            }
            return drive;
        }
    }
}
