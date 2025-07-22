using System.Diagnostics;
using System.Windows;
using YukkuriMovieMaker.Plugin;

namespace Afterimage
{
    internal class AfterimageVersionCheck : IPlugin
    {
        public string Name => "[Version Checker]残像";
        
        public AfterimageVersionCheck()
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await CheckVersionAndNotifyAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AfterimageVersionCheck] バージョンチェック失敗: {ex.Message}");
                }
            });
        }

        
        private static async Task CheckVersionAndNotifyAsync()
        {
            if (await GetVersion.CheckVersionAsync("残像"))
            {
                string url = "https://ymm4-info.net/ymme/";
                var result = MessageBox.Show(
                    $"「残像エフェクト」に新しいバージョンがあります。\n\n配布サイトを開いて最新バージョンを確認しますか？\n{url}",
                    "更新通知",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
