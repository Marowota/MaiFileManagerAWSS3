using CommunityToolkit.Maui.Views;
using MaiFileManager.Classes;
using MaiFileManager.Classes.Aws;
using System.Text;
using static Android.Graphics.ImageDecoder;

namespace MaiFileManager.Pages;

public partial class SpinnerPopup : Popup
{
    internal CancellationTokenSource? _cts;
    internal StorageService awsStorageService;
    internal FileInfo file;
    internal StringBuilder pathResult;
	public SpinnerPopup()
	{
		InitializeComponent();
	}
    internal SpinnerPopup(CancellationTokenSource cts, StorageService awsStorageService, FileInfo file, ref StringBuilder path)
    {
        InitializeComponent();
        _cts = cts;
        this.awsStorageService = awsStorageService;
        this.file = file;
        this.pathResult = path;
    }

    private async void Popup_Opened(object sender, CommunityToolkit.Maui.Core.PopupOpenedEventArgs e)
    {
        pathResult.Append(await awsStorageService.DownloadObjectFromBucketAsync(file.FullName.Remove(0, MaiConstants.HomePath.Length + 1), file.FullName, _cts.Token));
        if (!_cts.IsCancellationRequested) this.Close();
    }
    private void Popup_Closed(object sender, CommunityToolkit.Maui.Core.PopupClosedEventArgs e)
    {
		if (_cts != null && e.WasDismissedByTappingOutsideOfPopup)
        {
            _cts.Cancel();
        }
    }

}