using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Diagnostics;
using System.Net;

namespace MaiFileManager.Classes.Aws
{
    internal class StorageService : IStorageService
    {

        private AwsCredentials awsCredentials;
        private AmazonS3Client client;
        internal string bucketName;

        public StorageService(AwsCredentials awsCredentials, string bucketName = "", RegionEndpoint region = null)
        {
            this.awsCredentials = awsCredentials;
            this.client = GetClient(awsCredentials, region);
            this.bucketName = bucketName;
        }

        public AmazonS3Client GetClient(AwsCredentials awsCredentials, RegionEndpoint region)
        {
            var credentials = new BasicAWSCredentials(awsCredentials.AwsKey, awsCredentials.AwsSecretKey);

            var config = new AmazonS3Config()
            {
                RegionEndpoint = region ?? Amazon.RegionEndpoint.USEast1
            };
            return new AmazonS3Client(credentials, config);
        }

        public void ChangeCredential(AwsCredentials credentials, RegionEndpoint region = null)
        {
            awsCredentials = credentials;
            client = GetClient(awsCredentials, region);
        }

        public async Task<ListBucketsResponse> GetBuckets()
        {
            return await client.ListBucketsAsync();
        }


        //public async Task CheckSignedIn()
        //{

        //    if (awsCredentials.AwsSecretKey == null || awsCredentials.AwsKey == null || awsCredentials.AwsSecretKey == "" || awsCredentials.AwsKey == "")
        //    {
        //        throw new Exception("Not signed in");
        //    }
        //    try
        //    {
        //        await GetBuckets();
        //    }
        //    catch
        //    {
        //        throw new Exception("Access denied, please check your credential");
        //    }
        //}

        public async Task SendNotification(string title, string message, string cancel)
        {
            Page tmp = Shell.Current.CurrentPage;
            await tmp.Dispatcher.DispatchAsync(async () =>
                await tmp.DisplayAlert(title, message, cancel));
        }
        public async Task<bool> IsBucketExist(string bucketName)
        {
            
            try
            {
                var request = new ListBucketsRequest();
                var response =  await client.GetBucketLocationAsync(bucketName);

                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error checking bucket: '{ex.Message}'");
                await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error checking bucket: '{ex.Message}'");
                await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");
                
                return false;
            }
        }

        public async Task<bool> CreateBucket(string bucketName)
        {
            try
            {
                var request = new PutBucketRequest
                {
                    BucketName = bucketName,
                    UseClientRegion = true,
                };

                var response = await client.PutBucketAsync(request);
                Debug.WriteLine($"Bucket '{bucketName}' created.");
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error creating bucket: '{ex.Message}'");
                
                await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex) { 
                Debug.WriteLine($"Error creating bucket: '{ex.Message}'");

                await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when creating bucket.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");

                return false;
            }
        }

        public async Task<bool> CheckAndCreateBucket (string bucketName)
        {
            if (!(await IsBucketExist(bucketName)))
            {
                return await CreateBucket(bucketName);
            }
            return true;
        }

        public async Task<String> DownloadObjectFromBucketAsync(string objectName, string filePath, CancellationToken cancellationToken)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return "";
            }
            CancellationTokenSource sourceGet = new CancellationTokenSource();
            CancellationTokenSource sourceWrite = new CancellationTokenSource();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.WriteLine($"Downloading object from S3 bucket: {objectName}");
                // Create a GetObject request
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectName,
                };
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Issue request and remember to dispose of the response
                    using GetObjectResponse response = await client.GetObjectAsync(request, sourceGet.Token);

                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.WriteLine("Saving to local file: {0}", filePath);
                    // Save object to local file
                    await response.WriteResponseStreamToFileAsync(filePath, true, sourceWrite.Token);

                    cancellationToken.ThrowIfCancellationRequested();
                    return filePath;
                }
                catch (AmazonS3Exception ex)
                {
                    Debug.WriteLine($"Error saving {objectName}: {ex.Message}");
                    await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                    return "";
                }
                catch (AmazonServiceException ex)
                {
                    Debug.WriteLine($"Error saving {objectName}: {ex.Message}");
                    await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                    return "";
                }
                catch (WebException ex)
                {
                    Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                    await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                    return "";
                }
            }
            catch (OperationCanceledException ex)
            {
                sourceGet.Cancel();
                sourceWrite.Cancel();
                Debug.WriteLine("Operation canceled");
                return "";
            }
            catch (Android.OS.NetworkOnMainThreadException)
            {
                Debug.WriteLine("Operation canceled gahahahaha");
                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when downloading object.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");
                return "";
            }
        }


        public async Task<bool> UploadFileAsync(string objectName, string filePath, string savedPath)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return false;
            }
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = savedPath + "/" + objectName,
                FilePath = filePath,
                
            };

            var response = await client.PutObjectAsync(request);
            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine($"Successfully uploaded {objectName} to {bucketName}.");
                return true;
            }
            else
            {
                Debug.WriteLine($"Could not upload {objectName} to {bucketName}.");
                return false;
            }
        }

        public async Task<bool> DeleteObjectAsync(string path, string bucket = null)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return false;
            }
            try
            {
                if (bucket == null )
                {
                    bucket = bucketName;
                }
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = path,
                };
                ListObjectsV2Response listResponse;
                listResponse = await client.ListObjectsV2Async(listRequest);

                if (listResponse.S3Objects.Count == 0)
                {
                    Debug.WriteLine($"Object: {path} not found in {bucket} when deleting object.");
                    await SendNotification("Error", $"Object: {path} not found in {bucket}.", "OK");
                    return false;
                }

                Debug.WriteLine($"Deleting object: {path}");
                listResponse.S3Objects.ForEach(async (obj) => await client.DeleteObjectAsync(bucket, obj.Key));
                Debug.WriteLine($"Object: {path} deleted from {bucket}.");
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");
                return false;
            }
        }

        public async Task<CopyObjectResponse> CopyingObjectAsync(
                        string sourceKey,
                        string destinationKey,
                        string sourceBucketName,
                        string destinationBucketName)
        {
            var response = new CopyObjectResponse();
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = sourceBucketName,
                    Prefix = sourceKey,
                };
                ListObjectsV2Response listResponse;
                listResponse = await client.ListObjectsV2Async(listRequest);

                listResponse.S3Objects.ForEach(async (obj) =>
                {
                    var request = new CopyObjectRequest
                    {
                        SourceBucket = sourceBucketName,
                        SourceKey = obj.Key,
                        DestinationBucket = destinationBucketName,
                        DestinationKey = destinationKey + obj.Key.Remove(0, sourceKey.Length),
                    };
                    response = await client.CopyObjectAsync(request);
                });

            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when copying an object.");
                await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return null;
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when copying an object.");
                await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                return null;
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when copying bucket.");
                await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when copying an object.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");
                return null;
            }

            return response;
        }


        public async Task<List<S3Object>> ListAllFileInPath(string path, CancellationToken cancellationToken = default)
        {

            try
            {
                if (await CheckAndCreateBucket(bucketName) == false)
                {
                    return new List<S3Object>();
                }
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = path,
                };

                ListObjectsV2Response response;

                response = await client.ListObjectsV2Async(request, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                List<S3Object> s3Objects = response.S3Objects ?? new List<S3Object>();
                Debug.WriteLine($"Found {s3Objects.Count} objects in {bucketName} with path {path}.");
                foreach (var item in s3Objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.WriteLine($"key = {item.Key} size = {item.Size}");
                }

                for (int i = s3Objects.Count - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    S3Object s3Object = s3Objects[i];
                    string tmp = s3Object.Key.Remove(0, path.Length);
                    if (tmp.StartsWith("/"))
                    {
                        tmp = tmp.Remove(0, 1);
                    }

                    Debug.WriteLine("checking" + tmp);

                    if (!((tmp.EndsWith("/") && tmp.LastIndexOf('/') == tmp.IndexOf('/')) 
                        || tmp.IndexOf('/') == -1 ) || tmp.Length == 0)
                    {
                        s3Objects.RemoveAt(i);
                    }
                }
                
                Debug.WriteLine($"Found {s3Objects.Count} objects in {bucketName} with path {path} after removal.");
                foreach (var item in s3Objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.WriteLine($"key = {item.Key} size = {item.Size}");
                }
                
                return s3Objects;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await SendNotification("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await SendNotification("Error", $"AWS Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await SendNotification("Error", "Can't connect to S3 Service, check your internet connection", "OK");
                return new List<S3Object>();
            }
            catch (System.OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine("Canceled loading file");
                return new List<S3Object>();
            }
            catch (Android.OS.OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine("Canceled loading file");
                return new List<S3Object>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await SendNotification("Error", $"Application Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
        }

    }
}
