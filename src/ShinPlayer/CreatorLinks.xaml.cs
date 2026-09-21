using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ShinPlayer;

public partial class CreatorLinks : UserControl
{
    public CreatorLinks() => InitializeComponent();
    private void Kai_Click(object sender, RoutedEventArgs e) => OpenPage("https://ai-campus.kr/");
    private void Naver_Click(object sender, RoutedEventArgs e) => OpenPage("https://contents.premium.naver.com/market/ai");
    private void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { MessageBox.Show(Window.GetWindow(this), "기본 브라우저를 열지 못했습니다. 브라우저 설정을 확인해 주세요.", "신플레이어", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
