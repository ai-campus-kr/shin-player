using System;
using System.Diagnostics;
using System.Windows;

namespace ShinPlayer;

public partial class MainWindow
{
    private void KaiPromo_Click(object sender, RoutedEventArgs e) => OpenCreatorPage("https://ai-campus.kr/");
    private void NaverPromo_Click(object sender, RoutedEventArgs e) => OpenCreatorPage("https://contents.premium.naver.com/market/ai");
    private void OpenCreatorPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { StatusText.Text = "브라우저를 열지 못했습니다: " + ex.Message; }
    }
}
