using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;
using PreviewSource = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>;

namespace MyLittleSlideShow.Views;

public partial class MainWindow : Window
{
    public enum WhichImage
    {
        Start,
        Next,
        Previous,
        LastLoadedFile
    }

    public enum TimerToggle
    {
        Start,
        Stop,
        Auto
    }

    private readonly SettingsManager _settingsManager;
    private AppSettings S => _settingsManager.Settings;

    private readonly DispatcherTimer _slideShowTimer;
    private readonly DispatcherTimer _hideControlsTimer;
    private readonly DispatcherTimer _zoomReloadTimer;

    // Ruhezeit ohne Radbewegung, nach der neu eingelesen und geschaerft wird.
    // Bewusst kurz, aber lang genug fuer Maeuse ohne Rasterung: solange das Rad
    // frei nachlaeuft, treffen weiter Ereignisse ein und der Timer beginnt von
    // vorn - neu gerechnet wird also genau einmal, wenn das Rad steht.
    private static readonly TimeSpan ZoomIdleDelay = TimeSpan.FromMilliseconds(150);

    private string[] _files = [];
    private int _fileIndex;
    private string _currentFile = string.Empty;
    private bool _isFirstFileAtStart = true;
    private int _remainingSeconds;
    private readonly string _version;

    // Verhindert, dass das Fenster mitwandert, waehrend ueber der Lupe gezoomt wird.
    private bool _allowWindowDrag = true;

    // Live-Vorschau, solange das Einstellungsfenster offen ist. _preview
    // ueberlagert dann die gespeicherten Darstellungswerte.
    private readonly DispatcherTimer _previewTimer;
    private DisplayPreview? _preview;
    private PreviewSource? _previewSource;
    private bool _previewSourceFailed;
    private (bool Sharpen, int Value, int Threshold) _renderedSharpening;

    private bool AmbiVisible => _preview?.WithAmbiFrame ?? S.WithAmbiFrame;
    private double AmbiOpacity => (_preview?.AmbiOpacity ?? S.OpacityOfAmbiFrame) / 100d;

    public MainWindow() : this(App.SettingsManager) { }

    public MainWindow(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;

        InitializeComponent();

        var fileVersion = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        _version = fileVersion is null ? string.Empty : $"version: {fileVersion}";

        _slideShowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _slideShowTimer.Tick += SlideShowTimer_Tick;

        _hideControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hideControlsTimer.Tick += HideControlsTimer_Tick;

        // Beim Zoomen wird nicht bei jedem Mausrad-Schritt neu dekodiert,
        // sondern erst wenn der Nutzer aufhoert zu drehen.
        _zoomReloadTimer = new DispatcherTimer { Interval = ZoomIdleDelay };
        _zoomReloadTimer.Tick += ZoomReloadTimer_Tick;

        // Drosselt die Vorschau beim Ziehen eines Reglers auf ~25 Bilder/s.
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _previewTimer.Tick += PreviewTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;

        PointerPressed += OnWindowPointerPressed;
        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;
    }

    // ------------------------------------------------------------------ Start

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Gespeicherte Groesse nur uebernehmen, wenn sie plausibel ist. War das
        // Fenster einmal von aussen zusammengeschoben worden (z. B. auf 39 px
        // Hoehe), blieb sonst fuer immer kein Platz mehr fuer das Bild.
        if (IsUsableSize(S.MainWindowWidth, S.MainWindowHeight))
        {
            Width = S.MainWindowWidth;
            Height = S.MainWindowHeight;
        }
        else
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
        }

        VersionLabel.Text = _version;

        // Sicherheitsnetz: Der Eintrag in der Taskleiste wird bereits im AXAML
        // abgeschaltet, also bevor das Fenster existiert. Startet die Anwendung
        // aber gleichzeitig mit der Shell (Autostart), kann die Taskleiste das
        // Fenster erwischen, bevor sie den Zustand ausgewertet hat. Hier noch
        // einmal setzen kostet nichts und raeumt so einen Eintrag wieder ab.
        ShowInTaskbar = false;

        LoadToolbarIcons();
        RestorePosition();
        HideControls();

        LoadImage(WhichImage.LastLoadedFile, rescanFolders: true, allowRandom: true);

        SetSlideShowInterval(S.ImageChangeIntervalSeconds);
        ToggleSlideShowTimer(S.ActivateSlideShow ? TimerToggle.Start : TimerToggle.Stop);

        PlatformServices.Current.SleepInhibitor.SetKeepAwake(S.DisplayAlwaysOn);
    }

    private void LoadToolbarIcons()
    {
        Prev_Button.Source = ImageProcessing.LoadAsset("Images/Prev.png");
        Next_Button.Source = ImageProcessing.LoadAsset("Images/Next.png");
        PlayStopImage.Source = ImageProcessing.LoadAsset("Images/Pause.png");
        ZoomImage.Source = ImageProcessing.LoadAsset("Images/Zoom.png");
        InfoImage.Source = ImageProcessing.LoadAsset("Images/Info.png");
        ChangeWallpaperImageButton.Source = ImageProcessing.LoadAsset("Images/ChangeWallpaper.png");
        DeleteImageButton.Source = ImageProcessing.LoadAsset("Images/Delete.png");
        RotateLeftImage.Source = ImageProcessing.LoadAsset("Images/Rotate.png");
        RotateRightImage.Source = ImageProcessing.LoadAsset("Images/Rotate.png");
        SettingsImageButton.Source = ImageProcessing.LoadAsset("Images/Settings512.png");
        CloseImageButton.Source = ImageProcessing.LoadAsset("Images/Shutdown.png");

        // Funktionen ohne Plattformunterstuetzung ausblenden statt fehlschlagen zu lassen
        ChangeWallpaperImageButton.IsVisible = PlatformServices.Current.Wallpaper.IsSupported;
        DeleteImageButton.IsVisible = PlatformServices.Current.Trash.IsSupported;
    }

    /// <summary>
    /// Stellt die zuletzt genutzte Fensterposition wieder her und korrigiert sie,
    /// falls der damalige Monitor nicht mehr vorhanden ist.
    /// </summary>
    private void RestorePosition()
    {
        var left = (int)S.PositionLeft;
        var top = (int)S.PositionTop;

        var target = new PixelPoint(left, top);
        var fitsOnAnyScreen = Screens.All.Any(screen => screen.Bounds.Contains(target));

        if (!fitsOnAnyScreen)
        {
            var primary = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (primary is not null)
            {
                left = primary.WorkingArea.X + primary.WorkingArea.Width - (int)Width - 20;
                top = primary.WorkingArea.Y + 40;
            }
        }

        Position = new PixelPoint(left, top);
        PersistState();
    }

    public void PersistState()
    {
        // Minimiert oder maximiert liefern Position und Groesse Werte, die
        // beim naechsten Start nicht wiederhergestellt werden duerfen.
        if (WindowState == WindowState.Normal)
        {
            S.PositionLeft = Position.X;
            S.PositionTop = Position.Y;

            if (IsUsableSize(Width, Height))
            {
                S.MainWindowWidth = (int)Width;
                S.MainWindowHeight = (int)Height;
            }
        }

        _settingsManager.Save();
    }

    private const int DefaultWidth = 386;
    private const int DefaultHeight = 315;
    private const int MaxWindowHeight = 4000;

    private bool IsUsableSize(double width, double height) =>
        width >= MinWidth && height >= MinHeight && height <= MaxWindowHeight;

    // ------------------------------------------------------------- Bild laden

    public void LoadImage(WhichImage which, bool rescanFolders, bool allowRandom)
    {
        PersistState();

        var directoryExists = Directory.Exists(S.FolderPath);

        if (directoryExists && rescanFolders)
        {
            var watch = Stopwatch.StartNew();
            _files = GetFilesFrom(S.FolderPath, S.ScanFolderRecursive);
            watch.Stop();
            LoadTimeValue_Label.Text = Math.Round(watch.Elapsed.TotalMilliseconds, 2)
                                           .ToString(System.Globalization.CultureInfo.CurrentCulture);
        }
        else if (!directoryExists)
        {
            _files = [];
        }

        if (_files.Length == 0)
        {
            LoadedFilesLabel.Text = "Datei: 0 / 0";
            ToggleSlideShowTimer(TimerToggle.Stop);
            _currentFile = string.Empty;
            LoadDefaultBackground();
            return;
        }

        Array.Sort(_files, StringComparer.OrdinalIgnoreCase);

        DetermineNextIndex(which, allowRandom);

        LoadedFilesLabel.Text = $"Datei: {_fileIndex + 1} / {_files.Length}";

        var path = _files[_fileIndex];
        if (!File.Exists(path))
        {
            // Datei wurde ausserhalb der Anwendung geloescht: Ordner neu einlesen.
            if (string.IsNullOrWhiteSpace(_currentFile))
                LoadDefaultBackground();
            else
                LoadImage(WhichImage.Next, rescanFolders: true, allowRandom: true);
            return;
        }

        try
        {
            _currentFile = path;
            S.LastLoadedFile = _currentFile;
            _settingsManager.Save();

            var bitmap = ImageProcessing.LoadForDisplay(
                path,
                (int)Width,
                (int)Height,
                S.SharpImages,
                S.SharpenValue,
                S.QuietSharpen ? S.SharpenThreshold : 0);

            ApplyBitmap(bitmap);

            _isFirstFileAtStart = false;
            SetSlideShowInterval(S.ImageChangeIntervalSeconds);

            if (S.SetEveryImageAsWallpaper && PlatformServices.Current.Wallpaper.IsSupported)
                SetFileAsWallpaper(_currentFile, askUser: false);
        }
        catch (Exception ex)
        {
            // Ein einzelnes defektes Bild darf die Diaschau nicht beenden.
            Debug.WriteLine($"Bild konnte nicht geladen werden: {path} ({ex.Message})");
            LoadDefaultBackground();
        }
    }

    private void DetermineNextIndex(WhichImage which, bool allowRandom)
    {
        if (_isFirstFileAtStart && !string.IsNullOrEmpty(S.LastLoadedFile))
        {
            var found = Array.FindIndex(_files,
                x => string.Equals(x, S.LastLoadedFile, StringComparison.OrdinalIgnoreCase));
            if (found >= 0)
            {
                _fileIndex = found;
                which = WhichImage.LastLoadedFile;
            }
        }
        else if (!string.IsNullOrEmpty(_currentFile))
        {
            var found = Array.FindIndex(_files,
                x => string.Equals(x, _currentFile, StringComparison.OrdinalIgnoreCase));
            if (found >= 0) _fileIndex = found;
        }

        if (S.RandomImages && allowRandom)
        {
            _fileIndex = Random.Shared.Next(0, _files.Length);
        }
        else
        {
            _fileIndex = which switch
            {
                WhichImage.Next => _fileIndex + 1,
                WhichImage.Previous => _fileIndex - 1,
                WhichImage.Start => 0,
                _ => _fileIndex
            };
        }

        // Ringpuffer
        if (_fileIndex >= _files.Length) _fileIndex = 0;
        if (_fileIndex < 0) _fileIndex = _files.Length - 1;
    }

    private void ApplyBitmap(Bitmap bitmap)
    {
        var previousShow = ShowImage.Source as Bitmap;
        var previousBackground = BackgroundImageBox.Source as Bitmap;

        ShowImage.Source = bitmap;
        BackgroundImageBox.Source = bitmap;
        BackgroundImageBox.Opacity = AmbiOpacity;
        BackgroundImageBox.IsVisible = AmbiVisible || ControlsBackgroundGrid.Opacity > 0;

        // Speicher der Vorgaengerbilder freigeben - WPF erledigte das ueber den
        // Finalizer der BitmapImage, Avalonia-Bitmaps halten native Ressourcen.
        if (!ReferenceEquals(previousShow, bitmap)) previousShow?.Dispose();
        if (!ReferenceEquals(previousBackground, bitmap)
            && !ReferenceEquals(previousBackground, previousShow)) previousBackground?.Dispose();
    }

    private void LoadDefaultBackground()
    {
        var bitmap = ImageProcessing.LoadAssetProcessed(
            "Images/Buch.png", (int)Width, (int)Height, sharpen: true, sharpenValue: 40);
        ApplyBitmap(bitmap);
    }

    private static string[] GetFilesFrom(string searchFolder, bool isRecursive)
    {
        if (string.IsNullOrWhiteSpace(searchFolder) || !Directory.Exists(searchFolder))
            return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = isRecursive,
            IgnoreInaccessible = true,          // ersetzt die alte isReadable-Pruefung
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        try
        {
            return Directory
                .EnumerateFiles(searchFolder, "*", options)
                .Where(f => ImageProcessing.SupportedExtensions
                                           .Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ordner konnte nicht gelesen werden: {searchFolder} ({ex.Message})");
            return [];
        }
    }

    // ------------------------------------------------------------ Hintergrund

    public async void SetFileAsWallpaper(string fileName, bool askUser)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName)) return;

        var wallpaper = PlatformServices.Current.Wallpaper;
        if (!wallpaper.IsSupported) return;

        ToggleSlideShowTimer(TimerToggle.Stop);

        try
        {
            if (askUser)
            {
                var result = await MyMessageBox.ShowDialogAsync(
                    "Desktophintergrund ändern",
                    "Soll dieses Bild als Desktophintergrund genutzt werden?",
                    this);
                if (!result) return;
            }

            var source = fileName;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Windows akzeptiert als Hintergrund zuverlaessig nur JPG/BMP.
            if (extension is not (".jpg" or ".jpeg" or ".bmp" or ".png"))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "MyLittleSlideShow");
                Directory.CreateDirectory(tempDir);
                foreach (var old in Directory.GetFiles(tempDir))
                {
                    try { File.Delete(old); } catch { /* egal */ }
                }

                source = ImageProcessing.ConvertToJpg(
                    fileName,
                    Path.Combine(tempDir, Path.GetFileNameWithoutExtension(fileName) + ".jpg"));
            }

            wallpaper.SetWallpaper(source, S.WallpaperStyle);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hintergrundbild konnte nicht gesetzt werden: {ex.Message}");
        }
        finally
        {
            ToggleSlideShowTimer(S.ActivateSlideShow ? TimerToggle.Start : TimerToggle.Stop);
        }
    }

    // ----------------------------------------------------------------- Timer

    public void SetSlideShowInterval(int seconds) => _remainingSeconds = seconds;

    public void ToggleSlideShowTimer(TimerToggle toggle)
    {
        var start = toggle switch
        {
            TimerToggle.Start => true,
            TimerToggle.Stop => false,
            _ => !_slideShowTimer.IsEnabled
        };

        if (start && !string.IsNullOrEmpty(_currentFile))
        {
            PlayStopImage.Source = ImageProcessing.LoadAsset("Images/Pause.png");
            RestTimeValue_Label.Text = TimeSpan.FromSeconds(_remainingSeconds).ToString();
            _slideShowTimer.Start();
        }
        else
        {
            PlayStopImage.Source = ImageProcessing.LoadAsset("Images/Play.png");
            RestTimeValue_Label.Text = "angehalten";
            _slideShowTimer.Stop();
        }
    }

    private void SlideShowTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds <= 0)
        {
            _remainingSeconds = S.ImageChangeIntervalSeconds;
            LoadImage(WhichImage.Next, rescanFolders: false, allowRandom: true);
        }

        _remainingSeconds--;

        if (!string.IsNullOrEmpty(_currentFile) && S.ActivateSlideShow)
            RestTimeValue_Label.Text = TimeSpan.FromSeconds(_remainingSeconds).ToString();
        else
            ToggleSlideShowTimer(TimerToggle.Stop);
    }

    // ------------------------------------------------------ Bedienleiste ein/aus

    /// <summary>
    /// Bildet das Verhalten von Window.Clip aus dem WPF-Original nach: bei
    /// ausgeblendeter Bedienleiste wird das Fenster unten um 34 px beschnitten,
    /// damit der weichgezeichnete AmbiFrame nicht nach unten auslaeuft.
    /// </summary>
    private void ApplyClipHeight(bool controlsVisible)
    {
        MainGrid.Width = Width;
        MainGrid.Height = Height;
        RootBorder.Width = Width;

        var clipped = controlsVisible ? Height : Height - 34;
        RootBorder.Height = clipped < 0 ? 0 : clipped;
    }

    private void ShowControls()
    {
        _hideControlsTimer.Stop();
        ControlsBackgroundGrid.Opacity = 0.9;
        BackgroundImageBox.IsVisible = true;
        BackgroundImageBox.Opacity = AmbiOpacity;
        ApplyClipHeight(true);
    }

    private void HideControls()
    {
        ControlsBackgroundGrid.Opacity = 0;
        BackgroundImageBox.Opacity = AmbiOpacity;
        BackgroundImageBox.IsVisible = AmbiVisible;
        ApplyClipHeight(false);
    }

    private void HideControlsTimer_Tick(object? sender, EventArgs e)
    {
        _hideControlsTimer.Stop();
        HideControls();
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e) => ShowControls();

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (_allowWindowDrag)
        {
            // Ersetzt Window.DragMove() aus WPF.
            BeginMoveDrag(e);
            PersistState();
        }
        else
        {
            _allowWindowDrag = true;
        }
    }

    // -------------------------------------------------------------- Zoomen

    private void ZoomWindow(int delta)
    {
        const double aspect = (double)DefaultWidth / DefaultHeight;

        var newHeight = Height + delta;
        if (newHeight * aspect < 357) return;        // Mindestgroesse
        if (newHeight > MaxWindowHeight) return;     // Obergrenze gegen Ausreisser

        Height = newHeight;
        Width = newHeight * aspect;

        // Nur im Speicher merken. Geschrieben wird erst in ReloadAfterZoom -
        // sonst laege bei jedem Radschritt ein Dateizugriff im Weg.
        S.MainWindowHeight = (int)Height;
        S.MainWindowWidth = (int)Width;

        _allowWindowDrag = false;
        ApplyClipHeight(ControlsBackgroundGrid.Opacity > 0);

        if (S.StretchWhileZooming)
        {
            // Die bereits geladene Bitmap wird proportional mitgezogen, damit
            // das Bild beim Aufziehen sofort das Fenster fuellt. Waehrend des
            // Zoomens wird bewusst nicht neu gerechnet - das hat vorher
            // sichtbar geruckelt.
            ShowImage.StretchDirection = StretchDirection.Both;
        }

        // Neu eingelesen und geschaerft wird erst, wenn das Rad steht.
        _zoomReloadTimer.Stop();
        _zoomReloadTimer.Start();
    }

    private void ReloadAfterZoom()
    {
        _zoomReloadTimer.Stop();

        // Nach dem Neuberechnen passt die Bitmap wieder exakt: das Hochskalieren
        // wird abgeschaltet, damit kein Bild ueber seine Aufloesung hinaus
        // aufgeblasen dargestellt wird.
        ShowImage.StretchDirection = StretchDirection.DownOnly;

        // LoadImage ruft PersistState auf und schreibt die neue Fenstergroesse
        // damit ohnehin weg - kein zusaetzliches Save noetig.
        LoadImage(WhichImage.LastLoadedFile, rescanFolders: false, allowRandom: false);
    }

    private void ZoomReloadTimer_Tick(object? sender, EventArgs e) => ReloadAfterZoom();

    // ------------------------------------------------------------- Ereignisse

    private void Prev_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        LoadImage(WhichImage.Previous, rescanFolders: false, allowRandom: true);
    }

    private void Next_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        LoadImage(WhichImage.Next, rescanFolders: false, allowRandom: true);
    }

    private void Prev_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        LoadImage(WhichImage.Previous, rescanFolders: false, allowRandom: true);
    }

    private void Next_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        LoadImage(WhichImage.Next, rescanFolders: false, allowRandom: true);
    }

    private void PlayStop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        ToggleSlideShowTimer(TimerToggle.Auto);
    }

    private void ShowImage_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        var point = e.GetCurrentPoint(this);
        var zoomModifier = point.Properties.IsRightButtonPressed
                           || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (zoomModifier)
        {
            ZoomWindow(e.Delta.Y < 0 ? 10 : -10);
        }
        else
        {
            LoadImage(e.Delta.Y < 0 ? WhichImage.Previous : WhichImage.Next,
                      rescanFolders: false, allowRandom: true);
        }
    }

    private void ShowImage_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed && e.ClickCount == 2)
        {
            e.Handled = true;
            PlatformServices.Current.Shell.OpenFile(_currentFile);
        }
    }

    private void Zoom_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        var point = e.GetCurrentPoint(this);
        ZoomWindow(point.Properties.IsRightButtonPressed ? -10 : 10);
    }

    private void Info_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrEmpty(_currentFile)) return;
        new InfoWindow(_currentFile).ShowDialog(this);
    }

    private void ChangeWallpaper_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        SetFileAsWallpaper(_currentFile, askUser: true);
    }

    private void Delete_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrEmpty(_currentFile)) return;

        if (PlatformServices.Current.Trash.MoveToTrash(_currentFile))
            LoadImage(WhichImage.Next, rescanFolders: true, allowRandom: true);
    }

    private void RotateLeft_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        RotateCurrent(Rotation.Left);
    }

    private void RotateRight_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        RotateCurrent(Rotation.Right);
    }

    private void RotateCurrent(Rotation rotation)
    {
        if (string.IsNullOrEmpty(_currentFile)) return;

        try
        {
            ImageProcessing.RotateAndSaveImageFile(_currentFile, rotation);
            LoadImage(WhichImage.LastLoadedFile, rescanFolders: false, allowRandom: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Drehen fehlgeschlagen: {ex.Message}");
        }
    }

    private async void Settings_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;

        var window = new SettingsWindow(_settingsManager);
        window.PreviewChanged += PreviewDisplay;

        // Waehrend eingestellt wird, bleibt das Bild stehen - sonst wechselt
        // die Diaschau mitten im Vergleichen auf ein anderes Motiv.
        var slideShowWasRunning = _slideShowTimer.IsEnabled;
        _slideShowTimer.Stop();

        try
        {
            await window.ShowDialog(this);
        }
        finally
        {
            window.PreviewChanged -= PreviewDisplay;
        }

        var previewWasActive = EndPreview();

        if (!window.SettingsSaved)
        {
            if (previewWasActive) RestoreAfterPreview();
            if (slideShowWasRunning) _slideShowTimer.Start();
            return;
        }

        SetSlideShowInterval(S.ImageChangeIntervalSeconds);
        LoadImage(WhichImage.LastLoadedFile, rescanFolders: true, allowRandom: false);
        ToggleSlideShowTimer(S.ActivateSlideShow ? TimerToggle.Start : TimerToggle.Stop);
        PlatformServices.Current.SleepInhibitor.SetKeepAwake(S.DisplayAlwaysOn);
    }

    // -------------------------------------------------------- Live-Vorschau

    /// <summary>
    /// Zeigt Darstellungswerte aus dem Einstellungsfenster sofort an, ohne sie
    /// zu speichern. AmbiFrame wird direkt umgesetzt, das Schaerfen gedrosselt
    /// ueber <see cref="_previewTimer"/>.
    /// </summary>
    public void PreviewDisplay(DisplayPreview preview)
    {
        if (_preview is null)
            _renderedSharpening = Sharpening(S.SharpImages, S.SharpenValue, S.QuietSharpen, S.SharpenThreshold);

        _preview = preview;

        BackgroundImageBox.Opacity = AmbiOpacity;
        BackgroundImageBox.IsVisible = AmbiVisible || ControlsBackgroundGrid.Opacity > 0;

        if (PreviewSharpening() != _renderedSharpening && !_previewTimer.IsEnabled)
            _previewTimer.Start();
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        if (_preview is null) return;

        var wanted = PreviewSharpening();
        if (wanted == _renderedSharpening) return;

        // Einmal dekodieren und skalieren, danach nur noch schaerfen.
        if (_previewSource is null && !_previewSourceFailed)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentFile) && File.Exists(_currentFile))
                    _previewSource = ImageProcessing.LoadResized(_currentFile, (int)Width, (int)Height);
                else
                    _previewSourceFailed = true;   // Standardbild: keine Schaerfe-Vorschau
            }
            catch (Exception ex)
            {
                _previewSourceFailed = true;
                Debug.WriteLine($"Vorschau konnte nicht geladen werden: {ex.Message}");
            }
        }

        if (_previewSource is null) return;

        ApplyBitmap(ImageProcessing.Render(_previewSource, wanted.Sharpen, wanted.Value, wanted.Threshold));
        _renderedSharpening = wanted;
    }

    /// <summary>Beendet die Vorschau; liefert true, wenn eine aktiv war.</summary>
    private bool EndPreview()
    {
        _previewTimer.Stop();

        var wasActive = _preview is not null;
        _preview = null;

        _previewSource?.Dispose();
        _previewSource = null;
        _previewSourceFailed = false;

        return wasActive;
    }

    /// <summary>Nach "Abbrechen": wieder so darstellen, wie es gespeichert ist.</summary>
    private void RestoreAfterPreview()
    {
        var saved = Sharpening(S.SharpImages, S.SharpenValue, S.QuietSharpen, S.SharpenThreshold);
        if (saved != _renderedSharpening)
        {
            LoadImage(WhichImage.LastLoadedFile, rescanFolders: false, allowRandom: false);
            return;
        }

        BackgroundImageBox.Opacity = AmbiOpacity;
        BackgroundImageBox.IsVisible = AmbiVisible || ControlsBackgroundGrid.Opacity > 0;
    }

    private (bool Sharpen, int Value, int Threshold) PreviewSharpening() =>
        _preview is { } p
            ? Sharpening(p.Sharpen, p.SharpenValue, p.QuietSharpen, p.SharpenThreshold)
            : Sharpening(S.SharpImages, S.SharpenValue, S.QuietSharpen, S.SharpenThreshold);

    /// <summary>Normalisiert die Schaerfe-Parameter so, wie LoadImage sie anwendet.</summary>
    private static (bool Sharpen, int Value, int Threshold) Sharpening(
        bool sharpen, int value, bool quiet, int threshold) =>
        sharpen && value > 0 ? (true, value, quiet ? threshold : 0) : (false, 0, 0);

    private void Close_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        App.Shutdown();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        PersistState();
        PlatformServices.Current.SleepInhibitor.SetKeepAwake(false);
        _slideShowTimer.Stop();
        _hideControlsTimer.Stop();
    }
}
