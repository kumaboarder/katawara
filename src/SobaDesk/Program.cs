using System.Drawing;
using Photino.NET;

namespace SobaDesk;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        LaunchOptions.Parse(args);

        var host = new AppHost();
        var window = new PhotinoWindow()
            .SetTitle(LaunchOptions.Strict ? "傍ら" : "傍ら（制限なし）")
            .SetUseOsDefaultSize(false)
            .SetSize(new Size(1280, 820))
            .SetMinSize(800, 520)
            .Center()
            .SetContextMenuEnabled(false)
            .SetGrantBrowserPermissions(false)
            .SetNotificationRegistrationId("SobaDesk.Viewer.8f2c1e4a")
            .RegisterCustomSchemeHandler("preview", host.ServePreview)
            .RegisterWebMessageReceivedHandler((sender, message) =>
            {
                if (sender is PhotinoWindow photino) host.HandleMessage(photino, message);
            });

        var icon = new[]
            {
                Path.Combine(WorkspaceStore.ExeDir(), "soba.ico"),
                Path.Combine(host.WebRoot, "favicon.ico"),
            }
            .FirstOrDefault(File.Exists);
        if (icon is not null) window.SetIconFile(icon);

        if (File.Exists(host.IndexPath))
        {
            window.Load(host.IndexPath);
        }
        else
        {
            window.LoadRawString(
                "<!DOCTYPE html><html lang=\"ja\"><meta charset=\"utf-8\"><body style=\"font-family:Segoe UI,sans-serif;padding:2rem;background:#f3eee4;color:#1c1814\">" +
                "<h1>傍ら</h1><p>画面ファイル (wwwroot) が見つかりません。</p><p><code>" +
                System.Net.WebUtility.HtmlEncode(host.WebRoot) +
                "</code></p><p>ZIP を展開したフォルダで <code>start.bat</code> を実行してください。</p></body></html>");
        }

        host.Attach(window);
        window.WaitForClose();
    }
}
