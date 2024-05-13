using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AndroidX.Core.Util;
using CommunityToolkit.Maui.Behaviors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        public async Task CheckSignedIn()
        {

            if (awsCredentials.AwsSecretKey == null || awsCredentials.AwsKey == null || awsCredentials.AwsSecretKey == "" || awsCredentials.AwsKey == "")
            {
                throw new Exception("Not signed in");
            }
            try
            {
                await GetBuckets();
            }
            catch
            {
                throw new Exception("Access denied, please check your credential");
            }
        }

        public async Task<bool> IsBucketExist(string bucketName)
        {
            
            try
            {
                var request = new ListBucketsRequest();
                var response =  await client.ListBucketsAsync(request);

                return response.Buckets.Any(b => b.BucketName == bucketName);
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error checking bucket: '{ex.Message}'");
                await Shell.Current.DisplayAlert("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error checking bucket: '{ex.Message}'");
                await Shell.Current.DisplayAlert("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when checking bucket.");
                await Shell.Current.DisplayAlert("Error", $"Application Message:'{ex.Message}'", "OK");
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
                await Shell.Current.DisplayAlert("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex) { 
                Debug.WriteLine($"Error creating bucket: '{ex.Message}'");
                await Shell.Current.DisplayAlert("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when creating bucket.");
                await Shell.Current.DisplayAlert("Error", $"Application Message:'{ex.Message}'", "OK");
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

        public async Task<String> DownloadObjectFromBucketAsync(string objectName, string filePath)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return "";
            }
            // Create a GetObject request
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
            };

            // Issue request and remember to dispose of the response
            using GetObjectResponse response = await client.GetObjectAsync(request);

            try
            {
                // Save object to local file
                await response.WriteResponseStreamToFileAsync($"{filePath}\\{objectName}", true, CancellationToken.None);
                return $"{filePath}\\{objectName}";
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error saving {objectName}: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return "";
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error saving {objectName}: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"AWS Message:'{ex.Message}'", "OK");
                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when downloading object.");
                await Shell.Current.DisplayAlert("Error", $"Application Message:'{ex.Message}'", "OK");
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

        public async Task<bool> DeleteObjectAsync(string path)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return false;
            }
            try
            {
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = path,
                };

                Debug.WriteLine($"Deleting object: {path}");
                await client.DeleteObjectAsync(deleteObjectRequest);
                Debug.WriteLine($"Object: {path} deleted from {bucketName}.");
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await Shell.Current.DisplayAlert("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await Shell.Current.DisplayAlert("Error", $"AWS Message:'{ex.Message}'", "OK");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' when deleting an object.");
                await Shell.Current.DisplayAlert("Error", $"Application Message:'{ex.Message}'", "OK");
                return false;
            }
        }

        public async Task<List<S3Object>> ListAllFileInPath(string path)
        {
            if (await CheckAndCreateBucket(bucketName) == false)
            {
                return new List<S3Object>();
            }
            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = path,
                };

                ListObjectsV2Response response;

                response = await client.ListObjectsV2Async(request);
                List<S3Object> s3Objects = response.S3Objects;
                
                Debug.WriteLine($"Found {s3Objects.Count} objects in {bucketName} with path {path}.");
                foreach (var item in s3Objects)
                {
                    Debug.WriteLine($"key = {item.Key} size = {item.Size}");
                }

                for (int i = s3Objects.Count - 1; i >= 0; i--)
                {
                    S3Object s3Object = s3Objects[i];
                    string tmp = s3Object.Key.Remove(0, path.Length);
                    if (tmp.StartsWith("/"))
                    {
                        tmp = tmp.Remove(0, 1);
                    }

                    Debug.WriteLine("checking" + tmp);

                    if (tmp.Contains('/') && tmp.Last() != '/' || tmp.Length == 0)
                    {
                        s3Objects.RemoveAt(i);
                    }
                }
                
                Debug.WriteLine($"Found {s3Objects.Count} objects in {bucketName} with path {path} after removal.");
                foreach (var item in s3Objects)
                {
                    Debug.WriteLine($"key = {item.Key} size = {item.Size}");
                }
                
                return s3Objects;
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await Shell.Current.DisplayAlert("Error", $"AWS S3 Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
            catch (AmazonServiceException ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await Shell.Current.DisplayAlert("Error", $"AWS Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                await Shell.Current.DisplayAlert("Error", $"Application Message:'{ex.Message}'", "OK");
                return new List<S3Object>();
            }
        }

    }
}
