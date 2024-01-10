using MauiAndroidAutoUpdate.Helpers;
using MauiAndroidAutoUpdate.Services;

namespace MauiAndroidAutoUpdate.Views;

public partial class AutoUpdateTestView : ContentPage
{
	public AutoUpdateTestView()
	{
		InitializeComponent();
	}


	protected override void OnAppearing()
	{
		base.OnAppearing();

		this.lblVersion.Text = VersionTracking.Default.CurrentVersion.ToString();
		this.lblBuild.Text = VersionTracking.Default.CurrentBuild.ToString();    }

	private async void Button_Clicked(object sender, EventArgs e)
	{
		//������� VersionCheck ���� üũ
		/*
		if (VersionCheck.Instance.IsNetworkAccess())
		{
			if (await VersionCheck.Instance.IsUpdate())
			{
				await VersionCheck.Instance.UpdateCheck();

				return;
			}
		}
		*/

		//�׽�Ʈ�� ���� ������Ʈ ȣ��
		AutoUpdateView view = new AutoUpdateView();
		//await Application.Current.MainPage.Navigation.PushModalAsync(view);
		await this.Navigation.PushModalAsync(view);
	}

	private void Button_Install_Slient(object sender, EventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() => {
			InstallApkSessionApiService install = new InstallApkSessionApiService();
			install.InstallApkSessionApi();
		});        
	}
}