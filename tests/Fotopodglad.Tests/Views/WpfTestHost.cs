using System.Windows;

namespace Fotopodglad.Tests.Views;

/// <summary>
/// Uruchamia kod okien WPF na wątku STA z jedną, wspólną instancją <see cref="Application"/>.
/// Aplikacja WPF może istnieć tylko raz na proces, a jej zasoby (styl czcionek, kolory) są potrzebne
/// do rozwiązania StaticResource z okien.
/// </summary>
internal static class WpfTestHost
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Application? _application;

    public static void Run(Action action)
    {
        var host = EnsureApplication();
        Exception? failure = null;
        host.Dispatcher.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });

        if (failure is not null)
        {
            throw new InvalidOperationException("Kod WPF zakończył się błędem na wątku interfejsu.", failure);
        }
    }

    private static Application EnsureApplication()
    {
        lock (Gate)
        {
            if (_application is not null)
            {
                return _application;
            }

            var ready = new ManualResetEventSlim();
            Exception? startupFailure = null;
            _thread = new Thread(() =>
            {
                try
                {
                    // Bezwzględne pack://-URI zamiast App.InitializeComponent(): względne ścieżki z App.xaml
                    // byłyby szukane w assembly testów, a nie w Fotopodglad.
                    var application = new Application
                    {
                        Resources = new ResourceDictionary
                        {
                            Source = new Uri(
                                "pack://application:,,,/Fotopodglad;component/Resources/Styles.xaml",
                                UriKind.Absolute)
                        }
                    };
                    _application = application;
                }
                catch (Exception ex)
                {
                    startupFailure = ex;
                }
                finally
                {
                    ready.Set();
                }

                if (startupFailure is null)
                {
                    System.Windows.Threading.Dispatcher.Run();
                }
            })
            {
                IsBackground = true
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            ready.Wait(TimeSpan.FromSeconds(30));
            if (startupFailure is not null)
            {
                throw new InvalidOperationException("Nie udało się uruchomić hosta WPF dla testów.", startupFailure);
            }

            return _application ?? throw new InvalidOperationException("Host WPF nie wystartował w oczekiwanym czasie.");
        }
    }
}
