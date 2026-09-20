using Firetrack.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Firetrack.ViewModels
{
    public class PdfDocumentInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // ICS, Clearance, Disposal
        public DateTime GeneratedAt { get; set; }
        public long SizeBytes { get; set; }

        public string DisplayCategory => Category switch
        {
            "ICS" => "📄 ICS",
            "Clearance" => "✅ Clearance",
            "DisposalCertificates" => "🗑️ Disposal",
            _ => Category
        };

        public string SizeDisplay => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
            _ => $"{SizeBytes / (1024.0 * 1024.0):F2} MB"
        };
    }

    public class PdfArchiveViewModel : ViewModelBase
    {
        private ObservableCollection<PdfDocumentInfo> _documents = new();
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public ObservableCollection<PdfDocumentInfo> Documents
        {
            get => _documents;
            set { _documents = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand LoadCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand ShareCommand { get; }
        public ICommand DeleteCommand { get; }

        public PdfArchiveViewModel()
        {
            LoadCommand = new Command(async () => await LoadDocumentsAsync());
            OpenCommand = new Command<PdfDocumentInfo>(OnOpen);
            ShareCommand = new Command<PdfDocumentInfo>(OnShare);
            DeleteCommand = new Command<PdfDocumentInfo>(OnDelete);

            _ = LoadDocumentsAsync();
        }

        private async System.Threading.Tasks.Task LoadDocumentsAsync()
        {
            IsBusy = true;
            try
            {
                // ✅ Run file IO on a background thread — this is the missing await
                var ordered = await System.Threading.Tasks.Task.Run(() =>
                {
                    var results = new List<PdfDocumentInfo>();
                    var root = FileSystem.AppDataDirectory;

                    var folders = new (string Folder, string Category)[]
                    {
                ("ICS", "ICS"),
                ("Clearance", "Clearance"),
                ("DisposalCertificates", "DisposalCertificates")
                    };

                    foreach (var (folder, category) in folders)
                    {
                        var path = Path.Combine(root, folder);
                        if (!Directory.Exists(path)) continue;

                        foreach (var file in Directory.GetFiles(path, "*.pdf"))
                        {
                            var info = new FileInfo(file);
                            results.Add(new PdfDocumentInfo
                            {
                                FilePath = file,
                                FileName = Path.GetFileNameWithoutExtension(file),
                                Category = category,
                                GeneratedAt = info.LastWriteTime,
                                SizeBytes = info.Length
                            });
                        }
                    }

                    return results.OrderByDescending(d => d.GeneratedAt).ToList();
                });

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Documents.Clear();
                    foreach (var d in ordered)
                        Documents.Add(d);

                    StatusMessage = ordered.Count == 0
                        ? "No PDFs generated yet."
                        : $"{ordered.Count} document(s) available.";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnOpen(PdfDocumentInfo? doc)
        {
            if (doc == null || !File.Exists(doc.FilePath))
            {
                await Shell.Current.DisplayAlert("Error", "File not found.", "OK");
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(doc.FilePath)
                });
            }
            catch
            {
                await Shell.Current.DisplayAlert("Info",
                    "No PDF viewer found. Try Share instead.", "OK");
            }
        }

        private async void OnShare(PdfDocumentInfo? doc)
        {
            if (doc == null || !File.Exists(doc.FilePath)) return;

            try
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = doc.FileName,
                    File = new ShareFile(doc.FilePath)
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnDelete(PdfDocumentInfo? doc)
        {
            if (doc == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Delete PDF", $"Delete '{doc.FileName}'?", "Yes", "Cancel");
            if (!confirm) return;

            try
            {
                if (File.Exists(doc.FilePath))
                    File.Delete(doc.FilePath);

                Documents.Remove(doc);
                StatusMessage = $"{Documents.Count} document(s) remaining.";
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}