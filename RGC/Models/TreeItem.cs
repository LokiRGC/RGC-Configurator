using System.Collections.ObjectModel;
using System.Windows;

namespace RGC.Models
{
    public class TreeItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsFile { get; set; }

        public string IconSrc
        {
            get
            {
                if (!IsFile) return "pack://application:,,,/folder_icon.png";
                var ext = System.IO.Path.GetExtension(Name).ToLowerInvariant();
                return ext switch
                {
                    ".json" => "pack://application:,,,/json_icon.png",
                    _ => ""
                };
            }
        }

        public string FileIcon => IsFile && string.IsNullOrEmpty(IconSrc) ? "📄" : "";
        public Visibility FileIconVisibility => string.IsNullOrEmpty(IconSrc) && IsFile ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ImageIconVisibility => !string.IsNullOrEmpty(IconSrc) ? Visibility.Visible : Visibility.Collapsed;
        public ObservableCollection<TreeItem> Children { get; set; } = new();
    }
}
