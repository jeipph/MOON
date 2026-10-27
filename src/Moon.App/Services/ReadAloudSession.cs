using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Moon.Core.Voice;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace Moon.App.Services;

/// <summary>
/// Lectura en voz alta de un libro: pide frases a la página, genera su audio (en orden y con
/// adelanto para que no haya silencios), las reproduce y avisa a la página qué frase resaltar.
/// Usa el reproductor de Windows: velocidad sin voz de pito y controles multimedia (teclas del
/// teclado, panel de volumen de Windows y NOX).
/// </summary>
public sealed partial class ReadAloudSession : ObservableObject, IDisposable
{
    // Frases preparadas por adelantado: con Chatterbox generar tarda más o menos lo que dura la frase.
    private const int Ahead = 3;

    private sealed record Item(string? Mark, string Text)
    {
        public Task<byte[]?>? Audio { get; set; }
    }

    private readonly VoiceService _voice;
    private readonly Action<object> _sendToPage;
    private readonly Dispatcher _dispatcher;
    private readonly string _bookTitle;
    private readonly LinkedList<Item> _queue = new();
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _sleepTimer = new();
    private CancellationTokenSource _cancel = new();
    private Task _synthesisChain = Task.CompletedTask;
    private bool _blockRequested;
    private bool _bookEnded;
    private bool _userPaused; // pausa pedida por la persona (o el temporizador): no seguir sola
    private string? _lastMark;
    private string _chapter = "";
    private string? _sleepAtChapterEnd;

    public ReadAloudSession(VoiceService voice, Action<object> sendToPage, string bookTitle, string language)
    {
        _voice = voice;
        _sendToPage = sendToPage;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _bookTitle = bookTitle;
        Language = SpeechText.Language(language);
        _player.AutoPlay = false;
        _player.MediaEnded += (_, _) => _dispatcher.BeginInvoke(PlayNext);
        _player.MediaFailed += (_, _) => _dispatcher.BeginInvoke(PlayNext);
        _player.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
        _player.CommandManager.NextReceived += (_, e) => { e.Handled = true; _dispatcher.BeginInvoke(SkipSentence); };
        _player.CommandManager.PlayReceived += (_, e) => { e.Handled = true; _dispatcher.BeginInvoke(Resume); };
        _player.CommandManager.PauseReceived += (_, e) => { e.Handled = true; _dispatcher.BeginInvoke(Pause); };
        _sleepTimer.Tick += (_, _) => { _sleepTimer.Stop(); Pause(); Status = "Temporizador: lectura en pausa."; };
    }

    public string Language { get; }

    public VoiceOption? Voice { get; set; }

    /// <summary>Leyendo (aunque esté en pausa).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    private double _speed = 1.0;

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.5, 2.0);
            _player.PlaybackSession.PlaybackRate = _speed;
        }
    }

    /// <summary>Empieza a leer desde lo que se ve en pantalla.</summary>
    public void Start()
    {
        Stop();
        _cancel = new CancellationTokenSource();
        IsActive = true;
        _bookEnded = false;
        _userPaused = false;
        Status = Voice?.IsWindows == false && !_voice.IsWarm
            ? "Preparando la voz… (la primera vez tarda unos 30 segundos)"
            : "Preparando…";
        RequestBlock("tts-start");
    }

    /// <summary>Frases nuevas de la página (un párrafo).</summary>
    public void OnBlock(IEnumerable<(string? Mark, string Text)> items, string chapter)
    {
        _blockRequested = false;
        if (!IsActive)
            return;
        if (_sleepAtChapterEnd is not null && chapter != _sleepAtChapterEnd && _chapter == _sleepAtChapterEnd)
        {
            // "Al terminar el capítulo": el siguiente bloque ya es de otro capítulo.
            _sleepAtChapterEnd = null;
            Pause();
            Status = "Fin del capítulo: lectura en pausa.";
            _queue.Clear();
            return;
        }
        _chapter = chapter;
        foreach (var (mark, text) in items)
            foreach (string chunk in SpeechText.Chunks(text))
                _queue.AddLast(new Item(mark, chunk));
        PrepareAhead();
        if (IsActive && _current is null)
            PlayNext();
    }

    public void OnBookEnded(string? error = null)
    {
        _blockRequested = false;
        if (error is not null)
        {
            Finish($"No se pudo seguir leyendo: {error}");
            return;
        }
        _bookEnded = true;
        if (_queue.Count == 0 && _current is null)
            Finish("Fin del libro.");
    }

    private Item? _current;

    private async void PlayNext()
    {
        _current = null;
        if (!IsActive)
            return;
        if (_queue.First is not { } first)
        {
            if (_bookEnded)
                Finish("Fin del libro.");
            else
                RequestBlock("tts-next");
            return;
        }
        _queue.RemoveFirst();
        _current = first.Value;
        PrepareAhead();
        if (_queue.Count < Ahead)
            RequestBlock("tts-next");

        CancellationToken cancel = _cancel.Token;
        byte[]? audio = await (first.Value.Audio ?? Synthesize(first.Value));
        if (cancel.IsCancellationRequested || !IsActive || _current != first.Value)
            return;
        if (audio is null)
        {
            Status = "No se pudo leer una frase; se salta.";
            PlayNext();
            return;
        }
        if (first.Value.Mark is { } mark && mark != _lastMark)
        {
            _lastMark = mark;
            _sendToPage(new { type = "tts-mark", mark });
        }
        var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(audio.AsBuffer());
        stream.Seek(0);
        _player.Source = MediaSource.CreateFromStream(stream, "audio/wav");
        _player.PlaybackSession.PlaybackRate = _speed;
        UpdateMediaInfo();
        if (!_userPaused)
        {
            _player.Play();
            IsPlaying = true;
            Status = "";
        }
    }

    // Prepara el audio de las próximas frases, de una en una y en orden (la tarjeta gráfica no se reparte).
    private void PrepareAhead()
    {
        int count = 0;
        foreach (Item item in _queue)
        {
            if (count++ >= Ahead)
                break;
            item.Audio ??= Synthesize(item);
        }
    }

    private Task<byte[]?> Synthesize(Item item)
    {
        CancellationToken cancel = _cancel.Token;
        VoiceOption voice = Voice ?? new VoiceOption(VoiceOption.WindowsName, null);
        Task<byte[]?> task = _synthesisChain.ContinueWith(_ => _voice.SpeakAsync(item.Text, Language, voice, cancel), TaskScheduler.Default).Unwrap();
        _synthesisChain = task;
        return task;
    }

    private void RequestBlock(string type)
    {
        if (_blockRequested || _bookEnded)
            return;
        _blockRequested = true;
        _sendToPage(new { type });
    }

    public void TogglePause()
    {
        if (!IsActive) Start();
        else if (IsPlaying) Pause();
        else Resume();
    }

    public void Pause()
    {
        _userPaused = true;
        _player.Pause();
        IsPlaying = false;
    }

    public void Resume()
    {
        if (!IsActive)
            return;
        _userPaused = false;
        IsPlaying = true;
        Status = "";
        if (_player.Source is not null && _current is not null)
            _player.Play();
        else
            PlayNext();
    }

    /// <summary>Salta a la frase siguiente.</summary>
    public void SkipSentence()
    {
        if (!IsActive)
            return;
        _player.Pause();
        PlayNext();
    }

    /// <summary>Vuelve a empezar la frase que suena.</summary>
    public void RestartSentence()
    {
        if (_player.Source is null)
            return;
        _player.PlaybackSession.Position = TimeSpan.Zero;
        Resume();
    }

    /// <summary>Temporizador para dormir: minutos, 0 = sin temporizador, -1 = al terminar el capítulo.</summary>
    public void SetSleepTimer(int minutes)
    {
        _sleepTimer.Stop();
        _sleepAtChapterEnd = minutes == -1 ? _chapter : null;
        if (minutes > 0)
        {
            _sleepTimer.Interval = TimeSpan.FromMinutes(minutes);
            _sleepTimer.Start();
        }
    }

    public void Stop()
    {
        _cancel.Cancel();
        _player.Pause();
        _player.Source = null;
        _queue.Clear();
        _current = null;
        _blockRequested = false;
        _lastMark = null;
        IsPlaying = false;
        IsActive = false;
        Status = "";
        _sendToPage(new { type = "tts-stop" });
    }

    private void Finish(string message)
    {
        Stop();
        Status = message;
    }

    // Lo que muestran los controles multimedia de Windows (y NOX): el libro y el capítulo.
    private void UpdateMediaInfo()
    {
        SystemMediaTransportControlsDisplayUpdater updater = _player.SystemMediaTransportControls.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = string.IsNullOrWhiteSpace(_chapter) ? _bookTitle : _chapter;
        updater.MusicProperties.Artist = _bookTitle;
        updater.MusicProperties.AlbumTitle = "MOON";
        updater.Update();
    }

    public void Dispose()
    {
        Stop();
        _sleepTimer.Stop();
        _player.Dispose();
        _cancel.Dispose();
    }
}
