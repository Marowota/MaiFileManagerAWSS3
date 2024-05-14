using Amazon.S3;
using Amazon.S3.Model;
using MaiFileManager.Classes;
using MaiFileManager.Classes.Aws;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace MaiFileManager.Pages;

public partial class Setting : ContentPage, INotifyPropertyChanged
{
    private bool IsFirstLoad = false;
    public bool HidePassword { get; set; }
    private Owner currentAcc = null;
    private StorageService storageService;
    private string awsAccessKey;
    private string awsSecretKey;
    private List<string> bucketNames = null;
    private bool IsCompletedLoad = false;
    private bool isCreatingBucket = false;


    public bool IsCreatingBucket
    {
        get
        {
            return isCreatingBucket;
        }
        set
        {
            isCreatingBucket = value;
            OnPropertyChanged(nameof(IsCreatingBucket));
            OnPropertyChanged(nameof(IsNotCreatingBucket));
        }
    }
    public bool IsNotCreatingBucket
    {
        get
        {
            return !isCreatingBucket;
        }
    }


    public List<string> BucketNames
    {
        get
        {
            return bucketNames;
        }
        set
        {
            bucketNames = value;
            OnPropertyChanged(nameof(BucketNames));
        }
    }

    public string AwsAccessKey
    {
        get
        {
            return awsAccessKey;
        }
        set
        {
            awsAccessKey = value;
            OnPropertyChanged(nameof(AwsAccessKey));
        }
    }
    public string AwsSecretKey
    {
        get
        {
            return awsSecretKey;
        }
        set
        {
            awsSecretKey = value;
            OnPropertyChanged(nameof(AwsSecretKey));
        }
    }
    internal Owner CurrentAcc
    {
        get
        {
            return currentAcc;
        }
        set
        {
            currentAcc = value;
            OnPropertyChanged(nameof(CurrentAcc));
            OnPropertyChanged(nameof(AwsAccNameView));
            OnPropertyChanged(nameof(IsSignedIn));
            OnPropertyChanged(nameof(IsNotSignedIn));
        }
    }
    public string AwsAccNameView
    {
        get
        {
            string accName = CurrentAcc == null ? "Not signed in" : CurrentAcc.DisplayName;
            return accName;
        }

    }

    public bool IsSignedIn
    {
        get
        {
            return CurrentAcc != null;
        }
    }
    public bool IsNotSignedIn
    {
        get
        {
            return CurrentAcc == null;
        }
    }


    public event PropertyChangedEventHandler PropertyChanged;
    public Setting()
    {
        InitializeComponent();
        InitLoad();
        IsCompletedLoad = true;
    }

    public void OnPropertyChanged([CallerMemberName] string name = "") =>
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    async void InitLoad()
    {
        int rd = Preferences.Default.Get("Sort_by", 0);
        int theme = Preferences.Default.Get("Theme", 0);
        IsFirstLoad = true;
        HidePassword = true;
        AwsAccessKey = Preferences.Default.Get("Aws_access_key", "");
        AwsSecretKey = Preferences.Default.Get("Aws_secret_key", "");
        storageService = new StorageService(new AwsCredentials(AwsAccessKey, AwsSecretKey), "");
        switch (theme)
        {
            case 0: DefaultRd.IsChecked = true; break;
            case 1: LightRd.IsChecked = true; break;
            case 2: DarkRd.IsChecked = true; break;
        }
        switch (rd)
        {
            case 0: NameAZ.IsChecked = true; break;
            case 1: NameZA.IsChecked = true; break;
            case 2: SizeSL.IsChecked = true; break;
            case 3: SizeLS.IsChecked = true; break;
            case 4: TypeAZ.IsChecked = true; break;
            case 5: TypeZA.IsChecked = true; break;
            case 6: DateNO.IsChecked = true; break;
            case 7: DateON.IsChecked = true; break;
        }
        OnPropertyChanged(nameof(HidePassword));
        await GetBucket();
        string defaultBucket = Preferences.Default.Get("Aws_Bucket_name", "");
        if (defaultBucket != "")
        {
            if (BucketNames.Contains(defaultBucket))
            {
                BucketPicker.SelectedIndex = BucketNames.IndexOf(defaultBucket);
            }
            else
            {
                BucketPicker.SelectedIndex = BucketNames.IndexOf(defaultBucket);
            }
        }

    }

    private void ShowPasswordChk_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {

        if (ShowPasswordChk.IsChecked)
        {
            HidePassword = false;
        }
        else
        {
            HidePassword = true;
        }
        if (!IsFirstLoad)
        {
            OnPropertyChanged(nameof(HidePassword));
        }
    }


    private async Task GetBucket()
    {
        if (AwsAccessKey == "" || AwsSecretKey == "")
        {
            return;
        }
        try
        {
            ListBucketsResponse response = await storageService.GetBuckets();
            if (response == null)
            {
                BucketNames = new List<string>();
                await DisplayAlert("Error", "Something went wrong, check your internet connection", "OK");
            }
            else
            {
                CurrentAcc = response.Owner;
                Debug.WriteLine("Owner: ", response.Owner.DisplayName);
                BucketNames = response.Buckets.Select(b => b.BucketName).ToList();

            }
        }
        catch (AmazonS3Exception ex)
        {
            Debug.WriteLine(ex.Message);
            await DisplayAlert("Error", ex.Message, "OK");
        }
        catch (WebException ex)
        {
            Debug.WriteLine(ex.Message);
            await DisplayAlert("Error", "Something went wrong, check your internet connection", "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            await DisplayAlert("Error", "Something went wrong", "OK");

        }

    }

    private async void Authenticate_Clicked(object sender, EventArgs e)
    {
        if (AccessKey.Text == null || SecretKey.Text == null || AccessKey.Text == "" || SecretKey.Text == "")
        {
            await DisplayAlert("Error", "Access key and secret key must not be empty", "OK");
            return;
        }
        Preferences.Default.Set("Aws_access_key", AccessKey.Text);
        Preferences.Default.Set("Aws_secret_key", SecretKey.Text);
        storageService.ChangeCredential(new AwsCredentials(Preferences.Default.Get("Aws_access_key", ""),
                           Preferences.Default.Get("Aws_secret_key", "")));
        await GetBucket();

    }
    private void BucketPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (BucketPicker.SelectedIndex != -1)
        {
            Preferences.Default.Set("Aws_Bucket_name", BucketNames[BucketPicker.SelectedIndex]);
        }
    }


    private async void NewBucket_Clicked(object sender, EventArgs e)
    {
        if (!IsCompletedLoad) return;
        List<Amazon.RegionEndpoint> listRegion = Amazon.RegionEndpoint.EnumerableAllRegions.ToList();
        string bucketName = null;
        string regionName = "Return";
        while (regionName == "Return")
        {

            bucketName = await DisplayPromptAsync("New bucket", "Bucket name: ", accept: "Next", cancel: "Cancel", initialValue: bucketName, placeholder: "Bucket name");
            if (bucketName == null)
            {
                return;
            }
            regionName = await DisplayActionSheet("Select region", "Cancel", "Return", listRegion.Select(r => (r.SystemName + " - " + r.DisplayName)).ToArray());
            if (regionName == "Cancel")
            {
                return;
            }
        }
        IsCreatingBucket = true;
        Debug.WriteLine(regionName);
        Amazon.RegionEndpoint regionObject = listRegion.Find(r => (r.SystemName + " - " + r.DisplayName) == regionName);
        if (regionObject == null)
        {
            return;
        }
        if (await storageService.IsBucketExist(bucketName))
        {
            await DisplayAlert("Error", "Bucket name already exist", "OK");
            return;
        }
        storageService.ChangeCredential(new AwsCredentials(Preferences.Default.Get("Aws_access_key", ""),
                                      Preferences.Default.Get("Aws_secret_key", "")), region: regionObject);
        bool isCreateSuccess = await storageService.CreateBucket(bucketName);
        await GetBucket();
        if (isCreateSuccess == true)
        {
            Preferences.Default.Set("Aws_Bucket_name", bucketName);
            await DisplayAlert("Success", "Bucket created", "OK");
        }
        IsCreatingBucket = false;

    }

    private void Signout_Clicked(object sender, EventArgs e)
    {
        CurrentAcc = null;
        BucketNames = null;
        Preferences.Default.Set("Aws_access_key", "");
        Preferences.Default.Set("Aws_secret_key", "");
        AwsAccessKey = Preferences.Default.Get("Aws_access_key", "");
        AwsSecretKey = Preferences.Default.Get("Aws_secret_key", "");

    }


    private async void DefaultRd_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if ((sender as RadioButton).IsChecked)
        {
            if (!IsFirstLoad)
            {
                bool allow = await DisplayAlert("Theme change", "The application must restart to apply theme change, continue?", "OK", "Cancel");
                if (!allow)
                {
                    InitLoad();
                    IsFirstLoad = false;
                    return;
                }
            }
            Preferences.Default.Set("Theme", 0);
            Application.Current.UserAppTheme = AppTheme.Unspecified;
#if ANDROID
            AndroidX.AppCompat.App.AppCompatDelegate.DefaultNightMode = AndroidX.AppCompat.App.AppCompatDelegate.ModeNightFollowSystem;
#endif
            if (!IsFirstLoad)
            {
                Application.Current.MainPage = new AppShell();
            }
            IsFirstLoad = false;
        }
    }

    private async void LightRd_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if ((sender as RadioButton).IsChecked)
        {
            if (!IsFirstLoad)
            {
                bool allow = await DisplayAlert("Theme change", "The application must restart to apply theme change, continue?", "OK", "Cancel");
                if (!allow)
                {
                    InitLoad();
                    IsFirstLoad = false;
                    return;
                }
            }
            Preferences.Default.Set("Theme", 1);
            Application.Current.UserAppTheme = AppTheme.Light;
#if ANDROID
            AndroidX.AppCompat.App.AppCompatDelegate.DefaultNightMode = AndroidX.AppCompat.App.AppCompatDelegate.ModeNightNo;
#endif
            if (!IsFirstLoad)
            {
                Application.Current.MainPage = new AppShell();
            }
            IsFirstLoad = false;
        }
    }

    private async void DarkRd_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if ((sender as RadioButton).IsChecked)
        {
            if (!IsFirstLoad)
            {
                bool allow = await DisplayAlert("Theme change", "The application must restart to apply theme change, continue?", "OK", "Cancel");
                if (!allow)
                {
                    InitLoad();
                    IsFirstLoad = false;
                    return;
                }
            }
            Preferences.Default.Set("Theme", 2);
            Application.Current.UserAppTheme = AppTheme.Dark;
#if ANDROID
            AndroidX.AppCompat.App.AppCompatDelegate.DefaultNightMode = AndroidX.AppCompat.App.AppCompatDelegate.ModeNightYes;
#endif
            if (!IsFirstLoad)
            {
                Application.Current.MainPage = new AppShell();
            }
            IsFirstLoad = false;
        }
    }

    private void NameAZ_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 0);
    }

    private void NameZA_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 1);
    }

    private void SizeSL_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 2);
    }

    private void SizeLS_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 3);
    }

    private void TypeAZ_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 4);
    }

    private void TypeZA_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 5);
    }

    private void DateNO_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 6);
    }

    private void DateON_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        Preferences.Default.Set("Sort_by", 7);
    }

}
