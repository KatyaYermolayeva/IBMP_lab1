using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFolder(object sender, RoutedEventArgs e)
        {
            string[] formats = { ".exe", ".dll", ".so", ".dylib" };

            string path = "";
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                path = folderBrowserDialog.SelectedPath;
            }
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            List<PublisherInfo> infos = new List<PublisherInfo>();
            foreach (string filename in System.IO.Directory.GetFiles(path))
            {
                FileInfo fileInfo = new FileInfo(filename);
                if (formats.Contains(fileInfo.Extension))
                {
                    try
                    {
                        X509Certificate certificate = X509Certificate.CreateFromSignedFile(filename);
                        string subject = certificate.Subject;
                        string pattern = @"O=""(.+?)"",";
                        Match m = Regex.Match(subject, pattern);
                        string name = subject;
                        if (!m.Success)
                        {
                            pattern = @"O=(.+?),";
                            m = Regex.Match(subject, pattern);
                        }
                        name = m.Groups[1].Value;
                        AddPublishersFile(name, fileInfo, infos);
                    }
                    catch (Exception)
                    {
                        string name = "Unknown";
                        AddPublishersFile(name, fileInfo, infos);
                    }
                }
            }
            infos = infos.OrderByDescending(i => i.Files.Count).ToList();
            InfoGrid.ItemsSource = infos;
            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(infos, options);
            File.WriteAllText(@"publishers.json", json);
        }

        public void AddPublishersFile(string name, FileInfo fileInfo, List<PublisherInfo> infos)
        {
            PublisherInfo info = infos.Find(i => i.Publisher.Equals(name));
            if (info == null)
            {
                info = new PublisherInfo()
                {
                    Publisher = name,
                    Files = new List<string> { fileInfo.Name },
                    SumSize = GetFileSizeOnDisk(fileInfo.FullName)
                };
                infos.Add(info);
            }
            else
            {
                info.Files.Add(fileInfo.Name);
                info.SumSize += GetFileSizeOnDisk(fileInfo.FullName);
            }
        }

        public static long GetFileSizeOnDisk(string file)
        {
            FileInfo info = new FileInfo(file);
            uint dummy, sectorsPerCluster, bytesPerSector;
            int result = GetDiskFreeSpaceW(info.Directory.Root.FullName, out sectorsPerCluster, out bytesPerSector, out dummy, out dummy);
            if (result == 0) throw new Win32Exception();
            uint clusterSize = sectorsPerCluster * bytesPerSector;
            uint hosize;
            uint losize = GetCompressedFileSizeW(file, out hosize);
            long size;
            size = (long)hosize << 32 | losize;
            return ((size + clusterSize - 1) / clusterSize) * clusterSize;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
           [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
           out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
           out uint lpTotalNumberOfClusters);
    }
}
