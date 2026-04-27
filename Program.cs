using System.IO;
using System.Threading;
using System;
using System.Timers;
using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;

namespace FolderBackupWatcher
{
    /// <summary>
    /// Основной класс программы для отслеживания изменений в папке и создания резервных копий.
    /// </summary>
    class Program
    {
        private static FileSystemWatcher? _watcher;
        private static System.Timers.Timer? _debounceTimer;

        private static string _sourceFolder = "";
        private static string _backupFolder = "";

        /// <summary>
        /// Выполняет инициализацию и запуск наблюдателя.
        /// </summary>
        static void Main(string[] args)
        {
            string configPath = "config.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine("Ошибка: Файл config.json не найден!");
                return;
            }

            try
            {
                string jsonString = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(jsonString);

                _sourceFolder = config?.SourceFolder ?? "";
                _backupFolder = config?.BackupFolder ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении конфига: {ex.Message}");
                return;
            }

            if (!Directory.Exists(_sourceFolder))
            {
                Console.WriteLine("Исходная папка не существует!");
                return;
            }

            if (!Directory.Exists(_backupFolder))
            {
                try
                {
                    Directory.CreateDirectory(_backupFolder);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Не удалось создать папку для бэкапов: " + ex.Message);
                    return;
                }
            }

            _watcher = SetupWatcher(_sourceFolder);
            _debounceTimer = SetupDebounceTimer();

            Console.CancelKeyPress += OnCancelKeyPress;

            _watcher.EnableRaisingEvents = true;
            Console.WriteLine("Наблюдение началось, нажмите ctr + c для завершения!");
            Console.ReadLine();
            CleanUp();
        }

        /// <summary>
        /// Создает объект класса Watcher для наблюдения изменений.
        /// </summary>
        private static FileSystemWatcher SetupWatcher(string path)
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 65536
            };

            watcher.Changed += OnFolderChanged;
            watcher.Created += OnFolderChanged;
            watcher.Renamed += OnFolderChanged;

            return watcher;
        }

        /// <summary>
        /// Создание таймера для предотвращения копирования одинаковых файлов.
        /// </summary>
        private static System.Timers.Timer SetupDebounceTimer()
        {
            const int DebounceDelayMs = 10000;

            var timer = new System.Timers.Timer(DebounceDelayMs)
            {
                AutoReset = false,
                Enabled = false
            };

            timer.Elapsed += OnTimedEvent;
            return timer;
        }

        /// <summary>
        /// Обработчик событий для сохранения.
        /// </summary>
        private static void OnFolderChanged(object? sender, FileSystemEventArgs e)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
            Console.WriteLine($"Обнаружено изменение: {e.ChangeType} → {e.FullPath}");
        }

        /// <summary>
        /// Срабатывает по истечении таймера задержки. Запускает процесс создания бэкапа.
        /// </summary>
        private static void OnTimedEvent(object? source, ElapsedEventArgs e)
        {
            try
            {
                Console.WriteLine("Старт создания резервной копии...");
                string sourceName = Path.GetFileName(_sourceFolder.TrimEnd(Path.DirectorySeparatorChar));
                string timeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string folderName = Path.Combine(_backupFolder, $"{sourceName}_Copy_{timeNow}");
                CopyDirectory(_sourceFolder, folderName);

                Console.WriteLine("Успешно создана копия: " + folderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при создании бэкапа: " + ex.Message);
            }
        }

        /// <summary>
        /// Копирует содержимое одной директории в другую.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="backup"></param>
        private static void CopyDirectory(string source, string backup)
        {
            Directory.CreateDirectory(backup);

            foreach (var file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(backup, Path.GetFileName(file));
                File.Copy(file, destFile, false);
            }

            foreach (var subDir in Directory.GetDirectories(source))
            {
                string destSubDir = Path.Combine(backup, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// Очищает память от наблюдателя и таймера.
        /// </summary>
        private static void CleanUp()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
        }

        /// <summary>
        /// Добавляет комбинацию Ctr + c для очистки памяти и завершения работы.
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("\nРабота программы завершена");
            CleanUp();
            e.Cancel = true;
            Environment.Exit(0);
        }

    }

    public class AppConfig
    {
        public string SourceFolder { get; set; } = "";
        public string BackupFolder { get; set; } = "";
    }
}
